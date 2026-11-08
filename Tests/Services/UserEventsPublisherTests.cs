using System.Reflection;
using Studium_Users.Domain.Entities.Users;
using Studium_Users.Services.DTOs;
using Studium_Users.Services.UserEventsPublishers;
using Wolverine;
using Xunit;

namespace Studium_Users.Tests.Services;

public class UserEventsPublisherTests
{
    [Fact]
    public async Task PublishUserStatusEventAsync_AdaptsUserAndPublishesToWolverineBus()
    {
        var busProxy = DispatchProxy.Create<IMessageBus, RecordingMessageBusProxy>();
        var recorder = (RecordingMessageBusProxy)(object)busProxy;
        var publisher = new UserEventsPublisher(busProxy);

        var user = new User("usr-1", "Ana Beatriz", "ana@studium.edu.br", "202410", UserRole.Student, "(41) 99876-5432");
        user.Block("3 no-shows");

        await publisher.PublishUserStatusEventAsync(UserEventType.UserBlocked, user);

        var published = Assert.Single(recorder.PublishedMessages);
        var statusEvent = Assert.IsType<UserStatusEvent>(published);

        Assert.Equal(UserEventType.UserBlocked, statusEvent.EventType);
        Assert.Equal("usr-1", statusEvent.Id);
        Assert.Equal("Ana Beatriz", statusEvent.FullName);
        Assert.Equal("ana@studium.edu.br", statusEvent.Email);
        Assert.Equal(UserRole.Student, statusEvent.Role);
        Assert.True(statusEvent.IsBlocked);
        Assert.Equal("3 no-shows", statusEvent.BlockReason);
    }

    public class RecordingMessageBusProxy : DispatchProxy
    {
        public List<object> PublishedMessages { get; } = [];

        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
        {
            if (targetMethod?.Name == nameof(IMessageBus.PublishAsync) && args is { Length: > 0 } && args[0] is not null)
            {
                PublishedMessages.Add(args[0]!);
                return ValueTask.CompletedTask;
            }

            throw new NotSupportedException($"Unexpected call to {targetMethod?.Name}");
        }
    }
}
