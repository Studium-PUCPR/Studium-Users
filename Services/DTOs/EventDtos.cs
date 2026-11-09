using Wolverine.Attributes;

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
public record BookingIntegrationEvent(
    BookingEventType EventType,
    string UserId,
    string? ReservationId,
    DateTime OccurredAtUtc
);

/// <summary>
/// Outbound SQS message payload published to Studium-Booking when user block status changes.
/// </summary>
[MessageIdentity("studium.users.user-status-changed")]
public record UserStatusChangedEvent(
    string UserId,
    bool IsBlocked,
    string? BlockReason,
    DateTime OccurredAtUtc
);

/// <summary>
/// Outbound message payload published to Studium-Audit.
/// </summary>
public record AuditIntegrationEvent(
    string ServiceSource,
    string ResourceId,
    string ActorId,
    string Action,
    string? MetadataJson = null,
    DateTime? TimestampUtc = null
);
