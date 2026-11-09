using Wolverine.Attributes;

namespace Studium_Users.Services.DTOs;

public enum UserActivityType
{
    ReservationCreated,
    ReservationCheckedIn,
    ReservationCancelled,
    NoShowRecorded
}

/// <summary>
/// Inbound message payload received from campus services when user activity occurs. Read-only.
/// </summary>
[MessageIdentity("studium.users.user-activity")]
public record UserActivityIntegrationEvent(
    UserActivityType EventType,
    string UserId,
    string? ActivityReferenceId,
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
[MessageIdentity("studium.audit.integration-event")]
public record AuditIntegrationEvent(
    string ServiceSource,
    string ResourceId,
    string ActorId,
    string Action,
    string? MetadataJson = null,
    DateTime? TimestampUtc = null
);
