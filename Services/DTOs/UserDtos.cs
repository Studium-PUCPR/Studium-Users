using Studium_Users.Domain.Entities.Users;

namespace Studium_Users.Services.DTOs;

/// <summary>
/// Request payload for updating a user's editable profile fields. Read-only.
/// </summary>
/// <param name="PhoneNumber">Contact phone number in Brazilian format. Read-only.</param>
/// <param name="NotifyReminders">Whether 15-minute reservation reminders are enabled. Read-only.</param>
/// <param name="NotifyConfirmations">Whether booking confirmation alerts are enabled. Read-only.</param>
public record UpdateProfileRequest(
    string? PhoneNumber,
    bool NotifyReminders = true,
    bool NotifyConfirmations = true
);

/// <summary>
/// Summary representation of a user for paginated user management lists. Read-only.
/// </summary>
/// <param name="Id">Unique user identifier. Read-only.</param>
/// <param name="FullName">Full name of the user. Read-only.</param>
/// <param name="Email">Institutional email address. Read-only.</param>
/// <param name="RegistrationNumber">Institutional registration number. Read-only.</param>
/// <param name="Role">Access role of the user. Read-only.</param>
/// <param name="PhoneNumber">Contact phone number in Brazilian format. Read-only.</param>
/// <param name="NoShowCount30Days">Number of no-shows recorded in the last 30 days. Read-only.</param>
/// <param name="IsBlocked">Whether the user account is currently blocked. Read-only.</param>
/// <param name="BlockReason">Justification if the user account is blocked. Read-only.</param>
/// <param name="BlockedAtUtc">UTC timestamp when the user account was blocked. Read-only.</param>
public record UserListItemDto(
    string Id,
    string FullName,
    string Email,
    string RegistrationNumber,
    UserRole Role,
    string? PhoneNumber,
    int NoShowCount30Days,
    bool IsBlocked,
    string? BlockReason,
    DateTime? BlockedAtUtc
);

/// <summary>
/// Generic paginated response wrapper. Read-only.
/// </summary>
/// <typeparam name="T">Type of items in the current page.</typeparam>
/// <param name="Items">Collection of items on the requested page. Read-only.</param>
/// <param name="Page">Current 1-based page number. Read-only.</param>
/// <param name="PageSize">Maximum number of items per page. Read-only.</param>
/// <param name="TotalCount">Total number of matching items across all pages. Read-only.</param>
/// <param name="TotalPages">Total number of available pages. Read-only.</param>
public record PagedUsersResponse<T>(
    IReadOnlyList<T> Items,
    int Page,
    int PageSize,
    int TotalCount,
    int TotalPages
);

/// <summary>
/// Detailed representation of a user including profile preferences and 30-day activity counters. Read-only.
/// </summary>
/// <param name="Id">Unique user identifier. Read-only.</param>
/// <param name="FullName">Full name of the user. Read-only.</param>
/// <param name="Email">Institutional email address. Read-only.</param>
/// <param name="RegistrationNumber">Institutional registration number. Read-only.</param>
/// <param name="Role">Access role of the user. Read-only.</param>
/// <param name="PhoneNumber">Contact phone number in Brazilian format. Read-only.</param>
/// <param name="NotifyReminders">Whether 15-minute reservation reminders are enabled. Read-only.</param>
/// <param name="NotifyConfirmations">Whether booking confirmation alerts are enabled. Read-only.</param>
/// <param name="IsBlocked">Whether the user account is currently blocked. Read-only.</param>
/// <param name="BlockReason">Justification if the user account is blocked. Read-only.</param>
/// <param name="BlockedAtUtc">UTC timestamp when the user account was blocked. Read-only.</param>
/// <param name="TotalReservations30Days">Total reservations created in the last 30 days. Read-only.</param>
/// <param name="CompletedReservations30Days">Reservations checked in and completed in the last 30 days. Read-only.</param>
/// <param name="CancelledReservations30Days">Reservations cancelled in the last 30 days. Read-only.</param>
/// <param name="NoShowCount30Days">No-shows recorded in the last 30 days. Read-only.</param>
/// <param name="CreatedAtUtc">UTC timestamp when the user account was first created. Read-only.</param>
public record UserDetailDto(
    string Id,
    string FullName,
    string Email,
    string RegistrationNumber,
    UserRole Role,
    string? PhoneNumber,
    bool NotifyReminders,
    bool NotifyConfirmations,
    bool IsBlocked,
    string? BlockReason,
    DateTime? BlockedAtUtc,
    int TotalReservations30Days,
    int CompletedReservations30Days,
    int CancelledReservations30Days,
    int NoShowCount30Days,
    DateTime CreatedAtUtc
);

/// <summary>
/// Request payload for blocking a user account. Read-only.
/// </summary>
/// <param name="Reason">Mandatory justification for blocking the user account. Read-only.</param>
public record BlockUserRequest(string? Reason);

public record AuditLogEntryDto(
    Guid Id,
    string UserId,
    string ActorId,
    string Action,
    string? Reason,
    DateTime CreatedAtUtc
);

public record AuditLogDto(
    Guid Id,
    string ServiceSource,
    string ResourceId,
    string ActorId,
    string Action,
    string? MetadataJson,
    DateTime TimestampUtc
);
