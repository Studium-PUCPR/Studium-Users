namespace Studium_Users.Domain.Entities.AuditLogs;

public enum AuditAction
{
    UserLogin,
    ProfileUpdated,
    UserBlocked,
    UserUnblocked
}

/// <summary>
/// Immutable security audit record. Read-only.
/// </summary>
/// <param name="Id">Unique identifier of the audit entry. Read-only.</param>
/// <param name="UserId">Identifier of the user associated with the action. Read-only.</param>
/// <param name="Action">Audit action enum. Read-only.</param>
/// <param name="TimestampUtc">UTC timestamp when the event occurred. Read-only.</param>
public record AuditLog(
    Guid Id,
    string UserId,
    AuditAction Action,
    DateTime TimestampUtc
);
