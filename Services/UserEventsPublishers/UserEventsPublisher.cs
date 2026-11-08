using Mapster;
using Studium_Users.Domain.Entities.Users;
using Studium_Users.Services.DTOs;
using Wolverine;

namespace Studium_Users.Services.UserEventsPublishers;

public class UserEventsPublisher(IMessageBus bus) : IUserEventsPublisher
{
    public ValueTask PublishUserStatusEventAsync(
        UserEventType eventType,
        User user,
        CancellationToken ct = default) =>
        bus.PublishAsync(user.Adapt<UserStatusEvent>() with
        {
            EventType = eventType,
            OccurredAtUtc = DateTime.UtcNow
        });
}
