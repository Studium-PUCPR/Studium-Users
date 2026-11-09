namespace Studium_Users.Domain.Entities.Inbox;

/// <summary>
/// Deduplication receipt for the Inbox Pattern, ensuring idempotent consumer event processing.
/// </summary>
public class InboxMessage(Guid id, string handlerName, DateTime receivedAtUtc)
{
    public Guid Id { get; init; } = id;
    public string HandlerName { get; init; } = handlerName;
    public DateTime ReceivedAtUtc { get; init; } = receivedAtUtc;
}
