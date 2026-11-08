namespace Studium_Users.Services.DTOs;

public enum BookingEventType
{
    ReservationCreated,
    ReservationCheckedIn,
    ReservationCancelled,
    NoShowRecorded
}

/// <summary>
/// Inbound SQS message payload received from Studium-Booking when reservation events occur. Read-only.
/// </summary>
/// <param name="EventType">Type of reservation event. Read-only.</param>
/// <param name="UserId">Identifier of the user associated with the reservation. Read-only.</param>
/// <param name="ReservationId">Optional reservation identifier. Read-only.</param>
/// <param name="OccurredAtUtc">UTC timestamp when the reservation event occurred. Read-only.</param>
public record BookingIntegrationEvent(
    BookingEventType EventType,
    string UserId,
    string? ReservationId,
    DateTime OccurredAtUtc
);
