using Microsoft.EntityFrameworkCore;
using Studium_Users.Data;
using Studium_Users.DTOs;

namespace Studium_Users.Services.BookingEventsHandlers;

public class BookingEventsHandler(
    UsersDbContext db,
    ILogger<BookingEventsHandler> logger)
{
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
                break;
            default:
                logger.LogWarning("Unrecognized BookingEventType {EventType} for User {UserId}.",
                    bookingEvent.EventType, bookingEvent.UserId);
                return;
        }

        await db.SaveChangesAsync(ct);
    }
}
