namespace AlbyOnContainers.Kernel.Caching.UnitTests;

using Caching.Options;

[TestFixture]
public sealed class CachingOptionsTests
{
    [Test]
    public void Defaults_AreSensibleProductionValues()
    {
        var options = new CachingOptions();

        Assert.Multiple(() =>
        {
            Assert.That(options.Duration, Is.EqualTo(TimeSpan.FromMinutes(30)));
            Assert.That(options.FailSafeMaxDuration, Is.EqualTo(TimeSpan.FromHours(2)));
            Assert.That(options.JitterMaxDuration, Is.EqualTo(TimeSpan.FromSeconds(2)));
            Assert.That(options.IsFailSafeEnabled, Is.True);
        });
    }

    [Test]
    public void Section_DerivesFromTypeNameWithoutOptionsSuffix() =>
        Assert.That(CachingOptions.Section, Is.EqualTo("Caching"));
}
