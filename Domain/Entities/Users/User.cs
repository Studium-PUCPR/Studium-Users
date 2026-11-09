using System.Text.RegularExpressions;
using Studium_Users.Domain.Exceptions;

namespace Studium_Users.Domain.Entities.Users;

public enum UserRole
{
    Student,
    Professor,
    Administrator
}

public partial class User
{
    [GeneratedRegex(@"^(?:\+55\s?)?\(?[1-9]{2}\)?\s?(?:9\d{4}|\d{4})-?\d{4}$", RegexOptions.Compiled)]
    private static partial Regex BrazilianPhoneRegex();

    /// <summary>
    /// Matches the Amazon Cognito 'sub' claim or UUID string. Read-only.
    /// </summary>
    public string Id { get; init; } = null!;

    /// <summary>
    /// Full name of the user. Read-only.
    /// </summary>
    public string FullName { get; init; } = null!;

    /// <summary>
    /// Institutional email address. Read-only.
    /// </summary>
    public string Email { get; init; } = null!;

    /// <summary>
    /// Institutional registration number. Read-only.
    /// </summary>
    public string RegistrationNumber { get; init; } = null!;

    /// <summary>
    /// Access role of the user. Read-only.
    /// </summary>
    public UserRole Role { get; init; }

    /// <summary>
    /// Contact phone number in Brazilian format.
    /// </summary>
    public string? PhoneNumber { get; private set; }

    /// <summary>
    /// Whether the user receives 15-minute reminders before a reservation starts.
    /// </summary>
    public bool NotifyReminders { get; private set; }

    /// <summary>
    /// Whether the user receives reservation creation and update confirmations.
    /// </summary>
    public bool NotifyConfirmations { get; private set; }

    /// <summary>
    /// Whether the user account is currently blocked from making reservations or joining waitlists.
    /// </summary>
    public bool IsBlocked { get; private set; }

    /// <summary>
    /// Justification provided by the administrator when blocking the account.
    /// </summary>
    public string? BlockReason { get; private set; }

    /// <summary>
    /// UTC timestamp of when the user account was blocked.
    /// </summary>
    public DateTime? BlockedAtUtc { get; private set; }

    /// <summary>
    /// Number of no-shows recorded for the user in the last 30 days.
    /// </summary>
    public int NoShowCount30Days { get; private set; }

    /// <summary>
    /// Total reservations created by the user in the last 30 days.
    /// </summary>
    public int TotalReservations30Days { get; private set; }

    /// <summary>
    /// Reservations successfully checked in and completed in the last 30 days.
    /// </summary>
    public int CompletedReservations30Days { get; private set; }

    /// <summary>
    /// Reservations cancelled in the last 30 days.
    /// </summary>
    public int CancelledReservations30Days { get; private set; }

    /// <summary>
    /// UTC timestamp of the start of the monthly window tracked for user activity metrics.
    /// </summary>
    public DateTime? LastActivityResetUtc { get; private set; }

    /// <summary>
    /// UTC timestamp when the user record was first created. Read-only.
    /// </summary>
    public DateTime CreatedAtUtc { get; init; }

    private User() { }

    public User(
        string id,
        string fullName,
        string email,
        string registrationNumber,
        UserRole role,
        string? phoneNumber = null,
        bool notifyReminders = true,
        bool notifyConfirmations = true,
        int totalReservations30Days = 0,
        int completedReservations30Days = 0,
        int cancelledReservations30Days = 0,
        int noShowCount30Days = 0)
    {
        if (string.IsNullOrWhiteSpace(id))
            throw new DomainException("INVALID_USER_ID", "User ID is required.");

        if (string.IsNullOrWhiteSpace(fullName))
            throw new DomainException("INVALID_FULL_NAME", "Full name is required.");

        if (string.IsNullOrWhiteSpace(email))
            throw new DomainException("INVALID_EMAIL", "Institutional email is required.");

        if (string.IsNullOrWhiteSpace(registrationNumber))
            throw new DomainException("INVALID_REGISTRATION", "Institutional registration number is required.");

        Id = id;
        FullName = fullName.Trim();
        Email = email.Trim();
        RegistrationNumber = registrationNumber.Trim();
        Role = role;
        TotalReservations30Days = totalReservations30Days;
        CompletedReservations30Days = completedReservations30Days;
        CancelledReservations30Days = cancelledReservations30Days;
        NoShowCount30Days = noShowCount30Days;
        CreatedAtUtc = DateTime.UtcNow;

        ApplyProfilePreferences(phoneNumber, notifyReminders, notifyConfirmations);
    }

    public void UpdateProfile(string? phoneNumber, bool notifyReminders, bool notifyConfirmations)
    {
        ApplyProfilePreferences(phoneNumber, notifyReminders, notifyConfirmations);
    }

    public void Block(string? reason, string? adminUserId = null)
    {
        if (!string.IsNullOrWhiteSpace(adminUserId) && Id == adminUserId)
        {
            throw new DomainException(
                "CANNOT_BLOCK_SELF",
                "Administrators cannot block their own account.");
        }

        if (string.IsNullOrWhiteSpace(reason))
        {
            throw new DomainException(
                "REASON_REQUIRED",
                "A block reason is required.");
        }

        IsBlocked = true;
        BlockReason = reason.Trim();
        BlockedAtUtc = DateTime.UtcNow;
    }

    public void Unblock()
    {
        IsBlocked = false;
        BlockReason = null;
        BlockedAtUtc = null;
    }

    public bool ToggleBlock(string? reason, string? adminUserId = null)
    {
        if (IsBlocked)
        {
            Unblock();
            return false;
        }

        Block(reason, adminUserId);
        return true;
    }

    public void RecordReservationCreated() => TotalReservations30Days++;
    public void RecordReservationCheckedIn() => CompletedReservations30Days++;
    public void RecordReservationCancelled() => CancelledReservations30Days++;
    public void RecordNoShow() => NoShowCount30Days++;

    public void EnsureMonthlyActivityWindow(DateTime nowUtc)
    {
        var currentMonthStart = new DateTime(nowUtc.Year, nowUtc.Month, 1, 0, 0, 0, DateTimeKind.Utc);
        if (LastActivityResetUtc is null || LastActivityResetUtc.Value < currentMonthStart)
        {
            TotalReservations30Days = 0;
            CompletedReservations30Days = 0;
            CancelledReservations30Days = 0;
            NoShowCount30Days = 0;
            LastActivityResetUtc = currentMonthStart;
        }
    }

    private void ApplyProfilePreferences(string? phoneNumber, bool notifyReminders, bool notifyConfirmations)
    {
        if (!string.IsNullOrWhiteSpace(phoneNumber))
        {
            var trimmedPhone = phoneNumber.Trim();
            if (!BrazilianPhoneRegex().IsMatch(trimmedPhone))
            {
                throw new DomainException(
                    "INVALID_PHONE_FORMAT",
                    "Invalid phone number format. Must follow Brazilian phone format.");
            }

            PhoneNumber = trimmedPhone;
        }
        else
        {
            PhoneNumber = null;
        }

        NotifyReminders = notifyReminders;
        NotifyConfirmations = notifyConfirmations;
    }
}
