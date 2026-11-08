using Mapster;
using Studium_Users.Domain.Entities.Users;
using Studium_Users.Services.DTOs;
using Xunit;

namespace Studium_Users.Tests.DTOs;

public class DtoMappingTests
{
    [Fact]
    public void Mapster_AdaptsUserToAllUserDtosCorrectly()
    {
        var user = new User(
            id: "usr-1",
            fullName: "Pedro Henrique Alves",
            email: "pedro.alves@studium.edu.br",
            registrationNumber: "2022115630",
            role: UserRole.Student,
            phoneNumber: "(41) 98765-1234",
            notifyReminders: true,
            notifyConfirmations: false,
            totalReservations30Days: 5,
            completedReservations30Days: 1,
            cancelledReservations30Days: 1,
            noShowCount30Days: 3);
        user.Block("3 no-shows recorded.");

        var listItemDto = user.Adapt<UserListItemDto>();
        Assert.Equal(user.Id, listItemDto.Id);
        Assert.Equal(user.FullName, listItemDto.FullName);
        Assert.Equal(user.Email, listItemDto.Email);
        Assert.Equal(user.RegistrationNumber, listItemDto.RegistrationNumber);
        Assert.Equal(user.Role, listItemDto.Role);
        Assert.Equal(3, listItemDto.NoShowCount30Days);
        Assert.True(listItemDto.IsBlocked);
        Assert.Equal("3 no-shows recorded.", listItemDto.BlockReason);

        var detailDto = user.Adapt<UserDetailDto>();
        Assert.Equal(user.Id, detailDto.Id);
        Assert.True(detailDto.NotifyReminders);
        Assert.False(detailDto.NotifyConfirmations);
        Assert.Equal(5, detailDto.TotalReservations30Days);
        Assert.Equal(1, detailDto.CompletedReservations30Days);
        Assert.Equal(1, detailDto.CancelledReservations30Days);
        Assert.Equal(3, detailDto.NoShowCount30Days);

        var statusEvent = user.Adapt<UserStatusEvent>() with
        {
            EventType = UserEventType.UserBlocked,
            OccurredAtUtc = DateTime.UtcNow
        };
        Assert.Equal(UserEventType.UserBlocked, statusEvent.EventType);
        Assert.Equal(user.Id, statusEvent.Id);
        Assert.Equal(user.Email, statusEvent.Email);
        Assert.Equal(user.FullName, statusEvent.FullName);
        Assert.Equal(user.Role, statusEvent.Role);
        Assert.True(statusEvent.IsBlocked);
        Assert.Equal("3 no-shows recorded.", statusEvent.BlockReason);
    }
}
