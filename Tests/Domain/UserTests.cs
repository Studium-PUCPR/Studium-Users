using Studium_Users.Domain.Entities.Users;
using Studium_Users.Domain.Exceptions;
using Xunit;

namespace Studium_Users.Tests.Domain;

public class UserTests
{
    [Fact]
    public void Constructor_WithValidArguments_CreatesUser()
    {
        var user = new User(
            id: "usr-123",
            fullName: "  Ana Beatriz Souza  ",
            email: "  ana.souza@studium.edu.br  ",
            registrationNumber: "  2024103587  ",
            role: UserRole.Student,
            phoneNumber: "(41) 99876-5432",
            notifyReminders: true,
            notifyConfirmations: false);

        Assert.Equal("usr-123", user.Id);
        Assert.Equal("Ana Beatriz Souza", user.FullName);
        Assert.Equal("ana.souza@studium.edu.br", user.Email);
        Assert.Equal("2024103587", user.RegistrationNumber);
        Assert.Equal(UserRole.Student, user.Role);
        Assert.Equal("(41) 99876-5432", user.PhoneNumber);
        Assert.True(user.NotifyReminders);
        Assert.False(user.NotifyConfirmations);
        Assert.False(user.IsBlocked);
        Assert.Null(user.BlockReason);
        Assert.Null(user.BlockedAtUtc);
    }

    [Theory]
    [InlineData("", "Ana", "ana@studium.edu.br", "2024", "INVALID_USER_ID")]
    [InlineData("   ", "Ana", "ana@studium.edu.br", "2024", "INVALID_USER_ID")]
    [InlineData("usr-1", "", "ana@studium.edu.br", "2024", "INVALID_FULL_NAME")]
    [InlineData("usr-1", "Ana", "", "2024", "INVALID_EMAIL")]
    [InlineData("usr-1", "Ana", "ana@studium.edu.br", "", "INVALID_REGISTRATION")]
    public void Constructor_WithMissingRequiredFields_ThrowsDomainException(
        string id,
        string fullName,
        string email,
        string registrationNumber,
        string expectedErrorCode)
    {
        var ex = Assert.Throws<DomainException>(() => new User(
            id: id,
            fullName: fullName,
            email: email,
            registrationNumber: registrationNumber,
            role: UserRole.Student));

        Assert.Equal(expectedErrorCode, ex.ErrorCode);
    }

    [Theory]
    [InlineData("(41) 99876-5432")]
    [InlineData("+55 (41) 99876-5432")]
    [InlineData("41998765432")]
    [InlineData("(41) 3333-4444")]
    [InlineData(null)]
    [InlineData("")]
    public void UpdateProfile_WithValidPhoneFormat_UpdatesFields(string? phone)
    {
        var user = CreateSampleUser();

        user.UpdateProfile(phone, notifyReminders: false, notifyConfirmations: false);

        Assert.Equal(string.IsNullOrWhiteSpace(phone) ? null : phone, user.PhoneNumber);
        Assert.False(user.NotifyReminders);
        Assert.False(user.NotifyConfirmations);
    }

    [Theory]
    [InlineData("12345")]
    [InlineData("abc-defg")]
    [InlineData("(00) 99876-5432")]
    [InlineData("+1 555-234-5678")]
    public void UpdateProfile_WithInvalidPhoneFormat_ThrowsDomainException(string invalidPhone)
    {
        var user = CreateSampleUser();

        var ex = Assert.Throws<DomainException>(() =>
            user.UpdateProfile(invalidPhone, notifyReminders: true, notifyConfirmations: true));

        Assert.Equal("INVALID_PHONE_FORMAT", ex.ErrorCode);
    }

    [Fact]
    public void Block_WithValidReason_BlocksUser()
    {
        var user = CreateSampleUser();

        user.Block("  3 no-shows in 30 days.  ", adminUserId: "usr-admin");

        Assert.True(user.IsBlocked);
        Assert.Equal("3 no-shows in 30 days.", user.BlockReason);
        Assert.NotNull(user.BlockedAtUtc);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Block_WithEmptyReason_ThrowsDomainException(string? emptyReason)
    {
        var user = CreateSampleUser();

        var ex = Assert.Throws<DomainException>(() => user.Block(emptyReason, adminUserId: "usr-admin"));

        Assert.Equal("REASON_REQUIRED", ex.ErrorCode);
        Assert.False(user.IsBlocked);
    }

    [Fact]
    public void Block_WhenAdminBlocksSelf_ThrowsDomainException()
    {
        var user = CreateSampleUser();

        var ex = Assert.Throws<DomainException>(() =>
            user.Block("Self block test", adminUserId: user.Id));

        Assert.Equal("CANNOT_BLOCK_SELF", ex.ErrorCode);
        Assert.False(user.IsBlocked);
    }

    [Fact]
    public void Unblock_ClearsBlockState()
    {
        var user = CreateSampleUser();
        user.Block("Repeated no-shows", adminUserId: "usr-admin");

        user.Unblock();

        Assert.False(user.IsBlocked);
        Assert.Null(user.BlockReason);
        Assert.Null(user.BlockedAtUtc);
    }

    [Fact]
    public void ToggleBlock_BlocksWhenActive_AndUnblocksWhenAlreadyBlocked()
    {
        var user = CreateSampleUser();

        var isBlockedFirst = user.ToggleBlock("3 no-shows", adminUserId: "usr-admin");
        Assert.True(isBlockedFirst);
        Assert.True(user.IsBlocked);

        var isBlockedSecond = user.ToggleBlock(null, adminUserId: "usr-admin");
        Assert.False(isBlockedSecond);
        Assert.False(user.IsBlocked);
    }

    [Fact]
    public void ActivityCounters_IncrementCorrectly()
    {
        var user = CreateSampleUser();

        user.RecordReservationCreated();
        user.RecordReservationCreated();
        user.RecordReservationCheckedIn();
        user.RecordReservationCancelled();
        user.RecordNoShow();

        Assert.Equal(2, user.TotalReservations30Days);
        Assert.Equal(1, user.CompletedReservations30Days);
        Assert.Equal(1, user.CancelledReservations30Days);
        Assert.Equal(1, user.NoShowCount30Days);
    }

    [Fact]
    public void EnsureMonthlyActivityWindow_ShouldResetCountersWhenMonthChanges()
    {
        var user = CreateSampleUser();
        var octDate = new DateTime(2026, 10, 15, 12, 0, 0, DateTimeKind.Utc);
        user.EnsureMonthlyActivityWindow(octDate);

        user.RecordReservationCreated();
        user.RecordNoShow();
        Assert.Equal(1, user.TotalReservations30Days);
        Assert.Equal(1, user.NoShowCount30Days);

        var novDate = new DateTime(2026, 11, 1, 0, 0, 0, DateTimeKind.Utc);
        user.EnsureMonthlyActivityWindow(novDate);

        Assert.Equal(0, user.TotalReservations30Days);
        Assert.Equal(0, user.NoShowCount30Days);
        Assert.Equal(new DateTime(2026, 11, 1, 0, 0, 0, DateTimeKind.Utc), user.LastActivityResetUtc);
    }

    private static User CreateSampleUser() => new(
        id: "usr-student-1",
        fullName: "Rafael Lima",
        email: "rafael.lima@studium.edu.br",
        registrationNumber: "2023208841",
        role: UserRole.Student);
}
