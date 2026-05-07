namespace AlbyOnContainers.Kernel.Persistence.IntegrationTests.Fakes;

/// <summary>
/// Dummy entity that simulates an outbox message.
/// It is used exclusively by <see cref="FakeMessagingPlugin"/> to
/// verify that the EF Core plugin mechanism works correctly.
/// It does not represent a production entity.
/// </summary>
public sealed record FakeOutboxMessage
{
    private FakeOutboxMessage()
    {
        // EF Core requirement
    }

    /// <summary>Unique identifier of the message.</summary>
    public Guid Id { get; init; }

    /// <summary>Serialized content of the message (payload).</summary>
    public string Payload { get; private set; } = string.Empty;

    public static FakeOutboxMessage Create(string payload)
    {
        var message = new FakeOutboxMessage
        {
            Id = Guid.NewGuid()
        };
        message.Payload = payload;
        return message;
    }
}
