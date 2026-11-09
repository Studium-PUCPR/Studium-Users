using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Studium_Users.Data;
using Studium_Users.Domain.Entities.Inbox;
using Studium_Users.Services.DTOs;
using Studium_Users.Services.Options;
using Wolverine;

namespace Studium_Users.Services.UserActivityHandlers;

/// <summary>
/// Subscribes to campus activity events to update monthly user activity counters and enforce account standing policies.
/// Implements idempotent consumer processing via the Inbox Pattern with throttled lazy pruning.
/// </summary>
public class UserActivityHandler(
    UsersDbContext db,
    IMessageBus messageBus,
    IOptions<UserOptions> options,
    ILogger<UserActivityHandler> logger)
{
    private static DateTime _lastPrunedUtc = DateTime.MinValue;
    private static readonly object _pruneLock = new();

    public async Task HandleAsync(UserActivityIntegrationEvent activityEvent, CancellationToken ct = default)
    {
        var deduplicationId = activityEvent.EventId
            ?? (Guid.TryParse(activityEvent.ActivityReferenceId, out var parsedGuid) ? parsedGuid : Guid.NewGuid());

        var alreadyProcessed = await db.InboxMessages
            .AnyAsync(m => m.Id == deduplicationId && m.HandlerName == nameof(UserActivityHandler), ct);

        if (alreadyProcessed)
        {
            logger.LogInformation("Event {EventId} already processed by {Handler}. Skipping duplicate.",
                deduplicationId, nameof(UserActivityHandler));
            return;
        }

        var user = await db.Users.FirstOrDefaultAsync(u => u.Id == activityEvent.UserId, ct);
        if (user is null)
        {
            logger.LogWarning("Event {EventType}: User {UserId} not found in studium_users.",
                activityEvent.EventType, activityEvent.UserId);
            throw new KeyNotFoundException($"User '{activityEvent.UserId}' was not found in studium_users.");
        }

        user.EnsureMonthlyActivityWindow(activityEvent.OccurredAtUtc);

        switch (activityEvent.EventType)
        {
            case UserActivityType.ReservationCreated:
                user.RecordReservationCreated();
                break;
            case UserActivityType.ReservationCheckedIn:
                user.RecordReservationCheckedIn();
                break;
            case UserActivityType.ReservationCancelled:
                user.RecordReservationCancelled();
                break;
            case UserActivityType.NoShowRecorded:
                user.RecordNoShow();
                var maxNoShows = options.Value.MaxNoShowsBeforeSuspension;
                if (user.NoShowCount30Days >= maxNoShows && !user.IsBlocked)
                {
                    var suspensionDays = options.Value.SuspensionDays;
                    var blockReason = $"Exceeded maximum allowed no-shows ({maxNoShows} in current monthly window). Account suspended for {suspensionDays} days.";
                    user.Block(blockReason, "System", suspensionDays);

                    await messageBus.PublishAsync(new UserStatusChangedEvent(
                        user.Id,
                        true,
                        blockReason,
                        DateTime.UtcNow));

                    logger.LogWarning("User {UserId} reached {Threshold} no-shows and was suspended for {Days} days.",
                        user.Id, maxNoShows, suspensionDays);
                }
                break;
            default:
                logger.LogWarning("Unrecognized UserActivityType {EventType} for User {UserId}.",
                    activityEvent.EventType, activityEvent.UserId);
                return;
        }

        db.InboxMessages.Add(new InboxMessage(deduplicationId, nameof(UserActivityHandler), DateTime.UtcNow));

        // Throttled lazy cleanup of inbox records older than 7 days (runs once every 6 hours)
        if (DateTime.UtcNow - _lastPrunedUtc > TimeSpan.FromHours(6))
        {
            lock (_pruneLock)
            {
                if (DateTime.UtcNow - _lastPrunedUtc > TimeSpan.FromHours(6))
                {
                    _lastPrunedUtc = DateTime.UtcNow;
                    var cutoff = DateTime.UtcNow.AddDays(-7);
                    _ = db.InboxMessages
                        .Where(m => m.ReceivedAtUtc < cutoff)
                        .ExecuteDeleteAsync(CancellationToken.None);
                }
            }
        }

        await db.SaveChangesAsync(ct);
    }
}
