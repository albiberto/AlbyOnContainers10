namespace AlbyOnContainers.Kernel.Localization.UnitTests;

using AlbyOnContainers.Kernel.Localization;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Options;
using LocalizationOptions = AlbyOnContainers.Kernel.Localization.Options.LocalizationOptions;

[TestFixture]
public sealed class LocalizationExtensionsTests
{
    private static HostApplicationBuilder NewBuilder() => Host.CreateApplicationBuilder();

    // ---------- DI registration: lambda overload ----------

    [Test]
    public async Task WithLocalization_Lambda_BindsOptionsAndRegistersStringLocalizerFactory()
    {
        var builder = NewBuilder();

        builder.AddKernel().WithLocalization(opt =>
        {
            opt.DefaultCulture = "it";
            opt.SupportedCultures = ["it", "en"];
        });

        using var host = builder.Build();
        await host.StartAsync();

        var bound = host.Services.GetRequiredService<IOptions<LocalizationOptions>>().Value;
        var factory = host.Services.GetService<IStringLocalizerFactory>();

        Assert.Multiple(() =>
        {
            Assert.That(bound.DefaultCulture, Is.EqualTo("it"));
            Assert.That(bound.SupportedCultures, Is.EqualTo(new[] { "it", "en" }));
            Assert.That(factory, Is.Not.Null, "AddLocalization() must register an IStringLocalizerFactory.");
        });

        await host.StopAsync();
    }

    // ---------- DI registration: configuration overload ----------

    [Test]
    public async Task WithLocalization_FromConfiguration_BindsDefaultCultureFromSection()
    {
        // We only assert DefaultCulture here: ConfigurationBinder merges array entries by
        // index with the property defaults rather than replacing them, which is a binder
        // quirk outside this module's contract. The SupportedCultures binding behavior is
        // covered by integration with a real appsettings.json in PIM Web.
        const string json = """{ "Localization": { "DefaultCulture": "it" } }""";

        var builder = NewBuilder();
        builder.Configuration.AddJsonStream(new System.IO.MemoryStream(System.Text.Encoding.UTF8.GetBytes(json)));

        builder.AddKernel().WithLocalization();

        using var host = builder.Build();
        await host.StartAsync();

        var bound = host.Services.GetRequiredService<IOptions<LocalizationOptions>>().Value;

        Assert.That(bound.DefaultCulture, Is.EqualTo("it"));

        await host.StopAsync();
    }

    // ---------- ValidateOnStart: fail-fast ----------

    [Test]
    public void WithLocalization_WithDefaultCultureNotInSupported_FailsAtStartup()
    {
        var builder = NewBuilder();

        builder.AddKernel().WithLocalization(opt =>
        {
            opt.DefaultCulture = "ja";
            opt.SupportedCultures = ["it", "en"];
        });

        using var host = builder.Build();

        var ex = Assert.ThrowsAsync<OptionsValidationException>(() => host.StartAsync());

        Assert.That(ex!.Message, Does.Contain("DefaultCulture"));
    }

    [Test]
    public void WithLocalization_WithInvalidSupportedCulture_FailsAtStartup()
    {
        var builder = NewBuilder();

        builder.AddKernel().WithLocalization(opt =>
        {
            opt.DefaultCulture = "en";
            opt.SupportedCultures = ["en", "totally-bogus"];
        });

        using var host = builder.Build();

        var ex = Assert.ThrowsAsync<OptionsValidationException>(() => host.StartAsync());

        Assert.That(ex!.Message, Does.Contain("totally-bogus"));
    }

    [Test]
    public void WithLocalization_WithEmptyDefaultCulture_FailsAtStartup()
    {
        var builder = NewBuilder();

        builder.AddKernel().WithLocalization(opt =>
        {
            opt.DefaultCulture = string.Empty;
            opt.SupportedCultures = ["en"];
        });

        using var host = builder.Build();

        Assert.ThrowsAsync<OptionsValidationException>(() => host.StartAsync());
    }

    [Test]
    public void WithLocalization_WithEmptySupportedCultures_FailsAtStartup()
    {
        var builder = NewBuilder();

        builder.AddKernel().WithLocalization(opt =>
        {
            opt.DefaultCulture = "en";
            opt.SupportedCultures = [];
        });

        using var host = builder.Build();

        Assert.ThrowsAsync<OptionsValidationException>(() => host.StartAsync());
    }

    // ---------- Resolved IStringLocalizer ----------

    private sealed class DummyResources;

    [Test]
    public async Task WithLocalization_Lambda_AllowsResolvingTypedStringLocalizer()
    {
        var builder = NewBuilder();
        builder.AddKernel().WithLocalization(opt =>
        {
            opt.DefaultCulture = "en";
            opt.SupportedCultures = ["en"];
        });

        using var host = builder.Build();
        await host.StartAsync();

        var localizer = host.Services.GetService<IStringLocalizer<DummyResources>>();

        Assert.That(localizer, Is.Not.Null, "Typed IStringLocalizer<T> must be resolvable after WithLocalization().");

        await host.StopAsync();
    }
}
