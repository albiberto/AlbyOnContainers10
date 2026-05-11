using AlbyOnContainers.Kernel.Localization.Options;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace AlbyOnContainers.Kernel.Localization;

/// <summary>
///     Fluent extensions to register ASP.NET Core request localization on the Kernel builder
///     using the strongly-typed <see cref="LocalizationOptions" /> contract.
/// </summary>
public static class LocalizationKernelExtensions
{
    extension(IKernelBuilder builder)
    {
        // ==============================================================================
        // PUBLIC API (Fluent Builder)
        // ==============================================================================

        /// <summary>Registers localization, binding <see cref="LocalizationOptions" /> from configuration.</summary>
        public IKernelBuilder WithLocalization(string? section = null)
        {
            builder.BindOptions(section);
            builder.AddInternalLocalization();
            return builder;
        }

        /// <summary>Registers localization, configuring <see cref="LocalizationOptions" /> via a lambda.</summary>
        public IKernelBuilder WithLocalization(Action<LocalizationOptions> configureOptions)
        {
            builder.ConfigureOptions(configureOptions);
            builder.AddInternalLocalization();
            return builder;
        }

        // ==============================================================================
        // PRIVATE BOILERPLATE HELPERS
        // ==============================================================================

        private void BindOptions(string? section)
        {
            builder.Host.Services
                .AddOptions<LocalizationOptions>()
                .BindConfiguration(section ?? LocalizationOptions.Section)
                .ValidateDataAnnotations()
                .ValidateOnStart();
        }

        private void ConfigureOptions(Action<LocalizationOptions> configure)
        {
            builder.Host.Services
                .AddOptions<LocalizationOptions>()
                .Configure(configure)
                .ValidateDataAnnotations()
                .ValidateOnStart();
        }

        private void AddInternalLocalization()
        {
            // Standard ASP.NET Core registration. The runtime will look for .resx files
            // next to the marker classes (e.g. Resources/SharedResources.{culture}.resx).
            builder.Host.Services.AddLocalization();
        }
    }

    // ==============================================================================
    // MIDDLEWARE SETUP
    // ==============================================================================

    /// <summary>
    ///     Configures <see cref="RequestLocalizationOptions" /> from the bound <see cref="LocalizationOptions" />
    ///     and inserts the request localization middleware. Should be called early in the pipeline,
    ///     before any middleware that reads <c>CurrentCulture</c>.
    /// </summary>
    public static WebApplication UseKernelLocalization(this WebApplication app)
    {
        var options = app.Services.GetRequiredService<IOptions<LocalizationOptions>>().Value;

        var localizationOptions = new RequestLocalizationOptions()
            .AddSupportedCultures(options.SupportedCultures)
            .AddSupportedUICultures(options.SupportedCultures)
            .SetDefaultCulture(options.DefaultCulture);

        app.UseRequestLocalization(localizationOptions);

        return app;
    }
}
