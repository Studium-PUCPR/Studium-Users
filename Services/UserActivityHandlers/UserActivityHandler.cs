using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Studium_Users.Data;
using Studium_Users.Services.DTOs;
using Wolverine;

namespace Studium_Users.Services.UserActivityHandlers;

/// <summary>
/// Subscribes to campus activity events to update 30-day user activity counters and enforce account standing policies.
/// </summary>
public class UserActivityHandler(
    UsersDbContext db,
    IMessageBus messageBus,
    ILogger<UserActivityHandler> logger)
{
    public const int MaxNoShowThreshold = 5;

    public async Task HandleAsync(UserActivityIntegrationEvent activityEvent, CancellationToken ct = default)
    {
        var user = await db.Users.FirstOrDefaultAsync(u => u.Id == activityEvent.UserId, ct);
        if (user is null)
        {
            logger.LogWarning("Event {EventType}: User {UserId} not found in studium_users.",
                activityEvent.EventType, activityEvent.UserId);
            throw new KeyNotFoundException($"User '{activityEvent.UserId}' was not found in studium_users.");
        }

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
                if (user.NoShowCount30Days >= MaxNoShowThreshold && !user.IsBlocked)
                {
                    var blockReason = $"Exceeded maximum allowed no-shows ({MaxNoShowThreshold} in last 30 days).";
                    user.Block(blockReason, "System");

                    await messageBus.PublishAsync(new UserStatusChangedEvent(
                        user.Id,
                        true,
                        blockReason,
                        DateTime.UtcNow));

                    logger.LogWarning("User {UserId} reached {Threshold} no-shows and was automatically blocked.",
                        user.Id, MaxNoShowThreshold);
                }
                break;
            default:
                logger.LogWarning("Unrecognized UserActivityType {EventType} for User {UserId}.",
                    activityEvent.EventType, activityEvent.UserId);
                return;
        }

        await db.SaveChangesAsync(ct);
    }
}
