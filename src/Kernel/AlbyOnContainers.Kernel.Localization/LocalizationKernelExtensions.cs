using AlbyOnContainers.Kernel.Localization.Options;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace AlbyOnContainers.Kernel.Localization;

public static class LocalizationKernelExtensions
{
    extension(IKernelBuilder builder)
    {
        // ==============================================================================
        // PUBLIC API (Fluent Builder)
        // ==============================================================================
        
        public IKernelBuilder WithLocalization(string? section = null)
        {
            builder.BindOptions(section);
            builder.AddInternalLocalization();
            return builder;
        }

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
            // Registrazione standard ASP.NET Core
            // Il runtime cercherà automaticamente i file .resx accanto alle classi
            builder.Host.Services.AddLocalization();
        }
    }

    // ==============================================================================
    // MIDDLEWARE SETUP
    // ==============================================================================

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