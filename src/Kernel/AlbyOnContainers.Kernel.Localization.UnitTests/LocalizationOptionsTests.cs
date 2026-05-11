namespace AlbyOnContainers.Kernel.Localization.UnitTests;

using System.ComponentModel.DataAnnotations;
using AlbyOnContainers.Kernel.Localization.Options;

[TestFixture]
public sealed class LocalizationOptionsTests
{
    private static IList<ValidationResult> ValidateRecursively(LocalizationOptions options)
    {
        // Replicates the behavior of OptionsBuilder.ValidateDataAnnotations() + IValidatableObject:
        // both DataAnnotations and the custom Validate() are exercised.
        var ctx = new ValidationContext(options);
        var results = new List<ValidationResult>();
        Validator.TryValidateObject(options, ctx, results, validateAllProperties: true);
        return results;
    }

    // ---------- Defaults & convention ----------

    [Test]
    public void Defaults_AreSensibleProductionValues()
    {
        var options = new LocalizationOptions();

        Assert.Multiple(() =>
        {
            Assert.That(options.DefaultCulture, Is.EqualTo("en"));
            Assert.That(options.SupportedCultures, Is.EqualTo(new[] { "it", "en" }));
        });
    }

    [Test]
    public void Section_DerivesFromTypeNameWithoutOptionsSuffix() =>
        Assert.That(LocalizationOptions.Section, Is.EqualTo("Localization"));

    // ---------- Happy paths ----------

    [Test]
    public void Validate_WhenDefaultsAreUsed_ProducesNoErrors()
    {
        var results = ValidateRecursively(new LocalizationOptions());

        Assert.That(results, Is.Empty);
    }

    [Test]
    public void Validate_WhenDefaultCultureIsListedExactly_Succeeds()
    {
        var results = ValidateRecursively(new LocalizationOptions
        {
            DefaultCulture = "it",
            SupportedCultures = ["it", "en", "fr"]
        });

        Assert.That(results, Is.Empty);
    }

    [Test]
    public void Validate_WhenDefaultCultureIsListedCaseInsensitively_Succeeds()
    {
        // The LocalizationOptions comparator is OrdinalIgnoreCase so an "EN" default still
        // matches an "en" entry — culture names are case-insensitive in CLR.
        var results = ValidateRecursively(new LocalizationOptions
        {
            DefaultCulture = "EN",
            SupportedCultures = ["it", "en"]
        });

        Assert.That(results, Is.Empty);
    }

    // ---------- DataAnnotations failures ----------

    [Test]
    public void Validate_WhenDefaultCultureIsEmpty_FailsRequiredCheck()
    {
        var results = ValidateRecursively(new LocalizationOptions
        {
            DefaultCulture = string.Empty
        });

        Assert.That(results.Any(r => r.MemberNames.Contains(nameof(LocalizationOptions.DefaultCulture))), Is.True);
    }

    [Test]
    public void Validate_WhenSupportedCulturesIsEmpty_FailsMinLengthCheck()
    {
        var results = ValidateRecursively(new LocalizationOptions
        {
            DefaultCulture = "en",
            SupportedCultures = []
        });

        Assert.That(results.Any(r => r.MemberNames.Contains(nameof(LocalizationOptions.SupportedCultures))), Is.True);
    }

    // ---------- Semantic (IValidatableObject) failures ----------

    [Test]
    public void Validate_WhenDefaultCultureIsNotInSupportedCultures_Fails()
    {
        var results = ValidateRecursively(new LocalizationOptions
        {
            DefaultCulture = "fr",
            SupportedCultures = ["it", "en"]
        });

        Assert.Multiple(() =>
        {
            Assert.That(results, Is.Not.Empty);
            Assert.That(
                results.Any(r =>
                    r.MemberNames.Contains(nameof(LocalizationOptions.DefaultCulture)) &&
                    r.ErrorMessage!.Contains("must be one of the SupportedCultures")),
                Is.True);
        });
    }

    [Test]
    public void Validate_WhenDefaultCultureIsInvalidName_Fails()
    {
        var results = ValidateRecursively(new LocalizationOptions
        {
            DefaultCulture = "definitely-not-a-culture",
            SupportedCultures = ["definitely-not-a-culture"]
        });

        Assert.That(
            results.Any(r =>
                r.MemberNames.Contains(nameof(LocalizationOptions.DefaultCulture)) &&
                r.ErrorMessage!.Contains("not a valid culture name")),
            Is.True);
    }

    [Test]
    public void Validate_WhenAnySupportedCultureIsInvalid_Fails()
    {
        var results = ValidateRecursively(new LocalizationOptions
        {
            DefaultCulture = "en",
            SupportedCultures = ["en", "garbage-culture"]
        });

        Assert.Multiple(() =>
        {
            Assert.That(results, Is.Not.Empty);
            Assert.That(
                results.Any(r =>
                    r.MemberNames.Contains(nameof(LocalizationOptions.SupportedCultures)) &&
                    r.ErrorMessage!.Contains("garbage-culture")),
                Is.True);
        });
    }
}
