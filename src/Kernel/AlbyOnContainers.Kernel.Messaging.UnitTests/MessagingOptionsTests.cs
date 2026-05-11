namespace AlbyOnContainers.Kernel.Messaging.UnitTests;

using System.ComponentModel.DataAnnotations;
using AlbyOnContainers.Kernel.Messaging.Options;

[TestFixture]
public sealed class MessagingOptionsTests
{
    private static IList<ValidationResult> Validate(MessagingOptions options)
    {
        var ctx = new ValidationContext(options);
        var results = new List<ValidationResult>();
        Validator.TryValidateObject(options, ctx, results, validateAllProperties: true);
        return results;
    }

    private static MessagingOptions ValidOptions() => new()
    {
        Host = "rabbit.example.com",
        Username = "guest",
        Password = "guest"
    };

    // ---------- Defaults & convention ----------

    [Test]
    public void Defaults_AreSensibleProductionValues()
    {
        var options = ValidOptions();

        Assert.Multiple(() =>
        {
            Assert.That(options.Port, Is.EqualTo(5672));
            Assert.That(options.UseSsl, Is.False);
            Assert.That(options.RetryCount, Is.EqualTo(3));
            Assert.That(options.RetryInitialInterval, Is.EqualTo(TimeSpan.FromSeconds(2)));
            Assert.That(options.RetryMaxInterval, Is.EqualTo(TimeSpan.FromSeconds(30)));
            Assert.That(options.RetryDeltaInterval, Is.EqualTo(TimeSpan.FromSeconds(5)));
        });
    }

    [Test]
    public void Section_DerivesFromTypeNameWithoutOptionsSuffix() =>
        Assert.That(MessagingOptions.Section, Is.EqualTo("Messaging"));

    // ---------- DataAnnotations ----------

    [Test]
    public void Validate_WhenHostIsMissing_FailsRequiredCheck()
    {
        // Arrange
        var options = ValidOptions() with { Host = null! };

        // Act
        var results = Validate(options);

        // Assert
        Assert.That(results.Any(r => r.MemberNames.Contains(nameof(MessagingOptions.Host))), Is.True);
    }

    [Test]
    public void Validate_WhenConnectionStringNameIsConfigured_DoesNotRequireExpandedRabbitMqFields()
    {
        // Arrange
        var options = new MessagingOptions
        {
            ConnectionStringName = "messaging"
        };

        // Act
        var results = Validate(options);

        // Assert
        Assert.That(results, Is.Empty);
    }

    [Test]
    public void ParseConnectionString_WhenAmqpUriProvided_ExpandsRabbitMqFields()
    {
        // Arrange
        const string connectionString = "amqp://guest:p%40ss@rabbitmq.local:5673/my-vhost";

        // Act
        var connection = RabbitMqConnection.Parse(connectionString);

        // Assert
        Assert.Multiple(() =>
        {
            Assert.That(connection.Host, Is.EqualTo("rabbitmq.local"));
            Assert.That(connection.Port, Is.EqualTo(5673));
            Assert.That(connection.VirtualHost, Is.EqualTo("my-vhost"));
            Assert.That(connection.Username, Is.EqualTo("guest"));
            Assert.That(connection.Password, Is.EqualTo("p@ss"));
            Assert.That(connection.UseSsl, Is.False);
        });
    }

    [Test]
    public void ParseConnectionString_WhenAmqpsUriOmitsPort_UsesTlsDefaultPort()
    {
        // Arrange
        const string connectionString = "amqps://guest:guest@rabbitmq.local";

        // Act
        var connection = RabbitMqConnection.Parse(connectionString);

        // Assert
        Assert.Multiple(() =>
        {
            Assert.That(connection.Port, Is.EqualTo(5671));
            Assert.That(connection.VirtualHost, Is.EqualTo("/"));
            Assert.That(connection.UseSsl, Is.True);
        });
    }

    [Test]
    public void ParseConnectionString_WhenSchemeIsNotAmqp_ThrowsOptionsValidationException()
    {
        // Arrange
        const string connectionString = "http://guest:guest@rabbitmq.local";

        // Act & Assert
        Assert.Throws<Microsoft.Extensions.Options.OptionsValidationException>(() =>
            RabbitMqConnection.Parse(connectionString));
    }

    [Test]
    public void Validate_WhenPortIsOutOfRange_FailsRangeCheck()
    {
        var options = ValidOptions() with { Port = 70_000 };

        var results = Validate(options);

        Assert.That(results.Any(r => r.MemberNames.Contains(nameof(MessagingOptions.Port))), Is.True);
    }

    // ---------- Semantic (IValidatableObject) ----------

    [Test]
    public void Validate_WhenInitialIntervalGreaterThanMax_Fails()
    {
        var options = ValidOptions() with
        {
            RetryInitialInterval = TimeSpan.FromSeconds(60),
            RetryMaxInterval = TimeSpan.FromSeconds(10),
            RetryDeltaInterval = TimeSpan.FromSeconds(1)
        };

        var results = Validate(options);

        Assert.That(
            results.Any(r =>
                r.MemberNames.Contains(nameof(MessagingOptions.RetryInitialInterval)) &&
                r.ErrorMessage!.Contains("cannot be greater than RetryMaxInterval")),
            Is.True);
    }

    [Test]
    public void Validate_WhenDeltaIntervalExceedsBackoffWindow_Fails()
    {
        var options = ValidOptions() with
        {
            RetryInitialInterval = TimeSpan.FromSeconds(2),
            RetryMaxInterval = TimeSpan.FromSeconds(10),
            // gap is 8s; delta 30s is meaningless
            RetryDeltaInterval = TimeSpan.FromSeconds(30)
        };

        var results = Validate(options);

        Assert.That(
            results.Any(r =>
                r.MemberNames.Contains(nameof(MessagingOptions.RetryDeltaInterval)) &&
                r.ErrorMessage!.Contains("cannot exceed the gap")),
            Is.True);
    }

    [Test]
    public void Validate_WhenRetryCountIsZero_SkipsRetryIntervalCoherenceChecks()
    {
        // With RetryCount=0 the retry pipeline is disabled entirely, so the kernel does not
        // care about coherence between Initial/Max/Delta — those values are simply ignored.
        var options = ValidOptions() with
        {
            RetryCount = 0,
            RetryInitialInterval = TimeSpan.FromSeconds(60),
            RetryMaxInterval = TimeSpan.FromSeconds(10),
            RetryDeltaInterval = TimeSpan.FromSeconds(30)
        };

        var results = Validate(options);

        Assert.That(results, Is.Empty);
    }

    [Test]
    public void Validate_WhenAllValuesAreSensible_Succeeds()
    {
        var results = Validate(ValidOptions());

        Assert.That(results, Is.Empty);
    }
}
