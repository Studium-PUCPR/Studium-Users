using Mapster;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Studium_Users.Api.Extensions;
using Studium_Users.Data;
using Studium_Users.Domain.Entities.Users;
using Studium_Users.Services.DTOs;
using Wolverine;

namespace Studium_Users.Api.Controllers;

[ApiController]
[Route("api/users")]
public class UsersController(UsersDbContext db, IMessageBus messageBus, ILogger<UsersController> logger) : ControllerBase
{
    /// <summary>
    /// 1. GET /api/users - Admin paginated search and filter of users.
    /// </summary>
    [HttpGet]
    [Authorize(Policy = "AdminOnly")]
    public async Task<ActionResult<PagedUsersResponse<UserListItemDto>>> ListUsers(
        [FromQuery] string? search = null,
        [FromQuery] UserRole? role = null,
        [FromQuery] bool? isBlocked = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);

        var query = db.Users.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(search))
        {
            var pattern = $"%{search.Trim()}%";
            query = query.Where(u =>
                EF.Functions.ILike(u.FullName, pattern) ||
                EF.Functions.ILike(u.RegistrationNumber, pattern) ||
                EF.Functions.ILike(u.Email, pattern));
        }

        if (role.HasValue)
        {
            query = query.Where(u => u.Role == role.Value);
        }

        if (isBlocked.HasValue)
        {
            query = query.Where(u => u.IsBlocked == isBlocked.Value);
        }

        var totalCount = await query.CountAsync(ct);
        var totalPages = (int)Math.Ceiling(totalCount / (double)pageSize);

        var users = await query
            .OrderByDescending(u => u.NoShowCount30Days)
            .ThenBy(u => u.FullName)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ProjectToType<UserListItemDto>()
            .ToListAsync(ct);

        return Ok(new PagedUsersResponse<UserListItemDto>(
            Items: users,
            Page: page,
            PageSize: pageSize,
            TotalCount: totalCount,
            TotalPages: totalPages));
    }

    /// <summary>
    /// 2. GET /api/users/{id} - Returns full user details and 30-day summary counters, auto-provisioning from Cognito claims on first login.
    /// </summary>
    [HttpGet("{id}")]
    [Authorize]
    public async Task<ActionResult<UserDetailDto>> GetUserById(string id, CancellationToken ct)
    {
        if (!User.IsSelfOrAdmin(id))
            return Forbid();

        var user = await db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == id, ct);
        if (user is null && User.GetUserId() == id)
        {
            user = User.ToUserEntity();
            db.Users.Add(user);
            await db.SaveChangesAsync(ct);
        }

        if (user is null)
            return NotFound(new { error = "USER_NOT_FOUND", message = $"User '{id}' was not found." });

        return Ok(user.Adapt<UserDetailDto>());
    }

    /// <summary>
    /// 3. PUT /api/users/{id} - Updates PhoneNumber, NotifyReminders, and NotifyConfirmations for a user.
    /// </summary>
    [HttpPut("{id}")]
    [Authorize]
    public async Task<ActionResult<UserDetailDto>> UpdateProfile(
        string id,
        [FromBody] UpdateProfileRequest request,
        CancellationToken ct)
    {
        if (!User.IsSelfOrAdmin(id))
            return Forbid();

        var user = await db.Users.FirstOrDefaultAsync(u => u.Id == id, ct);
        if (user is null)
            return NotFound(new { error = "USER_NOT_FOUND", message = $"User '{id}' was not found." });

        user.UpdateProfile(request.PhoneNumber, request.NotifyReminders, request.NotifyConfirmations);
        await db.SaveChangesAsync(ct);

        await messageBus.PublishAsync(new AuditIntegrationEvent(
            ServiceSource: "Users",
            ResourceId: user.Id,
            ActorId: User.GetUserId(),
            Action: "ProfileUpdated",
            MetadataJson: $"{{\"phoneNumber\":\"{user.PhoneNumber}\"}}",
            TimestampUtc: DateTime.UtcNow));

        return Ok(user.Adapt<UserDetailDto>());
    }

    /// <summary>
    /// 4. PATCH /api/users/{id}/block - Toggles a user's block status (blocks with mandatory reason if active, unblocks if already blocked).
    /// </summary>
    [HttpPatch("{id}/block")]
    [Authorize(Policy = "AdminOnly")]
    public async Task<ActionResult<UserDetailDto>> ToggleBlockUser(
        string id,
        [FromBody] BlockUserRequest? request,
        CancellationToken ct)
    {
        var user = await db.Users.FirstOrDefaultAsync(u => u.Id == id, ct);
        if (user is null)
            return NotFound(new { error = "USER_NOT_FOUND", message = $"User '{id}' was not found." });

        user.ToggleBlock(request?.Reason, User.GetUserId());
        await db.SaveChangesAsync(ct);

        await messageBus.PublishAsync(new UserStatusChangedEvent(
            user.Id,
            user.IsBlocked,
            user.BlockReason,
            DateTime.UtcNow));

        await messageBus.PublishAsync(new AuditIntegrationEvent(
            ServiceSource: "Users",
            ResourceId: user.Id,
            ActorId: User.GetUserId(),
            Action: user.IsBlocked ? "UserBlocked" : "UserUnblocked",
            MetadataJson: $"{{\"reason\":\"{user.BlockReason}\"}}",
            TimestampUtc: DateTime.UtcNow));

        return Ok(user.Adapt<UserDetailDto>());
    }

    /// <summary>
    /// 5. GET /api/users/{id}/audit-logs - Queries immutable audit logs from Studium-Audit for a specific user.
    /// </summary>
    [HttpGet("{id}/audit-logs")]
    [Authorize(Policy = "AdminOnly")]
    public async Task<ActionResult<IReadOnlyList<AuditLogEntryDto>>> GetUserAuditLogs(
        string id,
        [FromServices] IHttpClientFactory httpClientFactory,
        CancellationToken ct = default)
    {
        var client = httpClientFactory.CreateClient("AuditClient");
        if (Request.Headers.TryGetValue("Authorization", out var authHeader) && !string.IsNullOrWhiteSpace(authHeader))
        {
            client.DefaultRequestHeaders.TryAddWithoutValidation("Authorization", authHeader.ToString());
        }

        const int maxAttempts = 3;
        var delays = new[] { TimeSpan.FromMilliseconds(200), TimeSpan.FromMilliseconds(500), TimeSpan.FromMilliseconds(1000) };

        for (var attempt = 1; attempt <= maxAttempts; attempt++)
        {
            try
            {
                var response = await client.GetAsync($"/api/audit/resources/{id}", ct);
                response.EnsureSuccessStatusCode();

                var logs = await response.Content.ReadFromJsonAsync<List<AuditLogDto>>(cancellationToken: ct);
                var results = logs?.Select(l => new AuditLogEntryDto(
                    l.Id,
                    l.ResourceId,
                    l.ActorId,
                    l.Action,
                    l.MetadataJson,
                    l.TimestampUtc)).ToList() ?? [];

                return Ok(results);
            }
            catch (Exception ex) when (attempt < maxAttempts && !ct.IsCancellationRequested)
            {
                logger.LogWarning(ex, "Attempt {Attempt} to query audit logs for user {UserId} failed. Retrying in {Delay}ms...",
                    attempt, id, delays[attempt - 1].TotalMilliseconds);
                await Task.Delay(delays[attempt - 1], ct);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Final attempt {Attempt} to query audit logs for user {UserId} failed", attempt, id);
            }
        }

        return Ok(Array.Empty<AuditLogEntryDto>());
    }
}
