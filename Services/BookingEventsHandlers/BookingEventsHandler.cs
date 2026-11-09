using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Studium_Users.Data;
using Studium_Users.Services.DTOs;
using Wolverine;

namespace Studium_Users.Services.BookingEventsHandlers;

public class BookingEventsHandler(
    UsersDbContext db,
    IMessageBus messageBus,
    ILogger<BookingEventsHandler> logger)
{
    public const int MaxNoShowThreshold = 5;

    public async Task HandleAsync(BookingIntegrationEvent bookingEvent, CancellationToken ct = default)
    {
        var user = await db.Users.FirstOrDefaultAsync(u => u.Id == bookingEvent.UserId, ct);
        if (user is null)
        {
            logger.LogWarning("SQS Event {EventType}: User {UserId} not found in studium_users.",
                bookingEvent.EventType, bookingEvent.UserId);
            throw new KeyNotFoundException($"User '{bookingEvent.UserId}' was not found in studium_users.");
        }

        switch (bookingEvent.EventType)
        {
            case BookingEventType.ReservationCreated:
                user.RecordReservationCreated();
                break;
            case BookingEventType.ReservationCheckedIn:
                user.RecordReservationCheckedIn();
                break;
            case BookingEventType.ReservationCancelled:
                user.RecordReservationCancelled();
                break;
            case BookingEventType.NoShowRecorded:
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
                logger.LogWarning("Unrecognized BookingEventType {EventType} for User {UserId}.",
                    bookingEvent.EventType, bookingEvent.UserId);
                return;
        }

        await db.SaveChangesAsync(ct);
    }
}
