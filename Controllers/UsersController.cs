using Mapster;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Studium_Users.Data;
using Studium_Users.Domain.Entities.AuditLogs;
using Studium_Users.Domain.Entities.Users;
using Studium_Users.DTOs;
using Studium_Users.Extensions;
using Studium_Users.Services.UserEventsPublishers;

namespace Studium_Users.Controllers;

[ApiController]
[Route("api/users")]
public class UsersController(
    UsersDbContext db,
    IUserEventsPublisher eventsPublisher) : ControllerBase
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

        await eventsPublisher.PublishUserStatusEventAsync(UserEventType.UserProfileUpdated, user, ct);

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

        var eventType = user.ToggleBlock(request?.Reason, User.GetUserId());
        await db.SaveChangesAsync(ct);

        await eventsPublisher.PublishUserStatusEventAsync(eventType, user, ct);

        return Ok(user.Adapt<UserDetailDto>());
    }

    /// <summary>
    /// 5. GET /api/users/{id}/audit-logs - Admin endpoint to inspect immutable security audit logs for a specific user.
    /// </summary>
    [HttpGet("{id}/audit-logs")]
    [Authorize(Policy = "AdminOnly")]
    public async Task<ActionResult<IReadOnlyList<AuditLog>>> GetUserAuditLogs(
        string id,
        [FromQuery] AuditAction? action = null,
        CancellationToken ct = default)
    {
        var query = db.AuditLogs
            .AsNoTracking()
            .Where(a => a.UserId == id);

        if (action.HasValue)
            query = query.Where(a => a.Action == action.Value);

        var logs = await query
            .OrderByDescending(a => a.TimestampUtc)
            .ToListAsync(ct);

        return Ok(logs);
    }
}
