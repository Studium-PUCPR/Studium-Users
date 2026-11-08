using Studium_Users.Domain.Entities.Users;

namespace Studium_Users.DTOs;

public enum BookingEventType
{
    ReservationCreated,
    ReservationCheckedIn,
    ReservationCancelled,
    NoShowRecorded
}

public enum UserEventType
{
    UserProfileUpdated,
    UserBlocked,
    UserUnblocked
}

/// <summary>
/// Inbound SQS event payload published by Studium-Booking. Read-only.
/// </summary>
/// <param name="EventType">Type of booking event that occurred. Read-only.</param>
/// <param name="UserId">Identifier of the user associated with the reservation. Read-only.</param>
/// <param name="ReservationId">Identifier of the reservation. Read-only.</param>
/// <param name="OccurredAtUtc">UTC timestamp when the booking event occurred. Read-only.</param>
public record BookingIntegrationEvent(
    BookingEventType EventType,
    string UserId,
    string ReservationId,
    DateTime OccurredAtUtc
);

/// <summary>
/// Outbound SQS event payload published by Studium-Users to downstream queues (Booking / Notifications). Read-only.
/// </summary>
/// <param name="EventType">Type of user lifecycle/preference event. Read-only.</param>
/// <param name="Id">Identifier of the user. Read-only.</param>
/// <param name="Email">Institutional email address. Read-only.</param>
/// <param name="FullName">Full name of the user. Read-only.</param>
/// <param name="Role">Access role of the user. Read-only.</param>
/// <param name="IsBlocked">Whether the user is currently blocked. Read-only.</param>
/// <param name="BlockReason">Justification if the user is blocked. Read-only.</param>
/// <param name="NotifyReminders">Whether 15-minute reminders are enabled. Read-only.</param>
/// <param name="NotifyConfirmations">Whether booking confirmation alerts are enabled. Read-only.</param>
/// <param name="OccurredAtUtc">UTC timestamp when the event was published. Read-only.</param>
public record UserStatusEvent(
    UserEventType EventType,
    string Id,
    string Email,
    string FullName,
    UserRole Role,
    bool IsBlocked,
    string? BlockReason,
    bool NotifyReminders,
    bool NotifyConfirmations,
    DateTime OccurredAtUtc
);
