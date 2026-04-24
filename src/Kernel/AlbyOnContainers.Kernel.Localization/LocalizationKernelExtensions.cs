using AlbyOnContainers.Kernel.Abstraction;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;

namespace AlbyOnContainers.Kernel.Localization;

public static class LocalizationKernelExtensions
{
    public static IKernelBuilder WithLocalization(this IKernelBuilder builder, string resourcesPath = "Resources")
    {
        builder.Host.Services.AddLocalization(options => options.ResourcesPath = resourcesPath);
        return builder;
    }

    public static WebApplication UseKernelLocalization(this WebApplication app, string defaultCulture = "it", params string[] supportedCultures)
    {
        var cultures = supportedCultures.Length > 0 ? supportedCultures : ["en", "it"];

        app.UseRequestLocalization(new RequestLocalizationOptions()
            .AddSupportedCultures(cultures)
            .AddSupportedUICultures(cultures)
            .SetDefaultCulture(defaultCulture));

        return app;
    }
}
