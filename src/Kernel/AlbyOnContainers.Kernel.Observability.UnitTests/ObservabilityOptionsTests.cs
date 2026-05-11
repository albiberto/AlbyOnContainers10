namespace AlbyOnContainers.Kernel.Observability.UnitTests;

using System.ComponentModel.DataAnnotations;
using AlbyOnContainers.Kernel.Observability.Options;

[TestFixture]
public sealed class ObservabilityOptionsTests
{
    private static IList<ValidationResult> Validate(ObservabilityOptions options)
    {
        var ctx = new ValidationContext(options);
        var results = new List<ValidationResult>();
        Validator.TryValidateObject(options, ctx, results, validateAllProperties: true);
        return results;
    }

    private static ObservabilityOptions Valid() => new();

    [Test]
    public void Defaults_AreProductionSensible()
    {
        // Arrange
        var options = Valid();

        // Act & Assert
        Assert.Multiple(() =>
        {
            Assert.That(options.EnableHttpClientTracing, Is.True);
            Assert.That(options.EnableEntityFrameworkTracing, Is.True);
            Assert.That(options.EnableMassTransitTracing, Is.True);
            Assert.That(options.EnableOtlpExporter, Is.False);
            Assert.That(options.TraceSamplingProbability, Is.EqualTo(1.0));
            Assert.That(options.CustomMeters, Is.Empty);
            Assert.That(options.CustomTracingSources, Is.Empty);
        });
    }

    [Test]
    public void Section_DerivesFromTypeNameWithoutOptionsSuffix() =>
        Assert.That(ObservabilityOptions.Section, Is.EqualTo("Observability"));

    [Test]
    public void Validate_WithSensibleDefaults_Succeeds()
    {
        // Arrange
        var options = Valid();

        // Act
        var results = Validate(options);

        // Assert
        Assert.That(results, Is.Empty);
    }

    [Test]
    public void Validate_WithSubzeroSamplingProbability_FailsRange()
    {
        // Arrange
        var options = Valid() with { TraceSamplingProbability = -0.1 };

        // Act
        var results = Validate(options);

        // Assert
        Assert.That(results.Any(r => r.MemberNames.Contains(nameof(ObservabilityOptions.TraceSamplingProbability))), Is.True);
    }

    [Test]
    public void Validate_WithSamplingProbabilityAboveOne_FailsRange()
    {
        // Arrange
        var options = Valid() with { TraceSamplingProbability = 1.5 };

        // Act
        var results = Validate(options);

        // Assert
        Assert.That(results.Any(r => r.MemberNames.Contains(nameof(ObservabilityOptions.TraceSamplingProbability))), Is.True);
    }

    [Test]
    public void Validate_WithEmptyEntryInCustomMeters_Fails()
    {
        // Arrange
        var options = Valid() with { CustomMeters = ["good", "  "] };

        // Act
        var results = Validate(options);

        // Assert
        Assert.That(results.Any(r => r.MemberNames.Contains(nameof(ObservabilityOptions.CustomMeters))), Is.True);
    }

    [Test]
    public void Validate_WithDuplicateCustomTracingSources_Fails()
    {
        // Arrange
        var options = Valid() with { CustomTracingSources = ["A", "B", "A"] };

        // Act
        var results = Validate(options);

        // Assert
        Assert.That(
            results.Any(r =>
                r.MemberNames.Contains(nameof(ObservabilityOptions.CustomTracingSources)) &&
                r.ErrorMessage!.Contains("duplicate")),
            Is.True);
    }
}
