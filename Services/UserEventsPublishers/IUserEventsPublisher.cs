using Studium_Users.Domain.Entities.Users;
using Studium_Users.DTOs;

namespace Studium_Users.Services.UserEventsPublishers;

public interface IUserEventsPublisher
{
    ValueTask PublishUserStatusEventAsync(UserEventType eventType, User user, CancellationToken ct = default);
}
