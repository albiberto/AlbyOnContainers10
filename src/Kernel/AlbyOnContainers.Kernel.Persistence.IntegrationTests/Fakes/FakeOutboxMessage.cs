namespace AlbyOnContainers.Kernel.Persistence.IntegrationTests.Fakes;

/// <summary>
/// Entità fittizia che simula un messaggio outbox.
/// Viene usata esclusivamente da <see cref="FakeMessagingPlugin"/> per
/// verificare che il meccanismo di plugin EF Core funzioni correttamente.
/// Non rappresenta un'entità di produzione.
/// </summary>
public sealed record FakeOutboxMessage
{
    private FakeOutboxMessage()
    {
        // EF Core requirement
    }

    /// <summary>Identificatore univoco del messaggio.</summary>
    public Guid Id { get; init; }

    /// <summary>Contenuto serializzato del messaggio (payload).</summary>
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
