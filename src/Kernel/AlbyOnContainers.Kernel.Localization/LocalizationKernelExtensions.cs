using System;
using AlbyOnContainers.Kernel.Abstraction;
using AlbyOnContainers.Kernel.Localization.Options;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace AlbyOnContainers.Kernel.Localization;

public static class LocalizationKernelExtensions
{
    public static IKernelBuilder WithLocalization(this IKernelBuilder builder, string configurationSection = LocalizationOptions.SectionName)
    {
        builder.Host.Services.AddOptions<LocalizationOptions>()
            .BindConfiguration(configurationSection)
            .ValidateDataAnnotations()
            .ValidateOnStart();

        builder.AddInternalLocalization(typeof(LocalizationKernelExtensions).Assembly);
        return builder;
    }

    public static IKernelBuilder WithLocalization(this IKernelBuilder builder, Action<LocalizationOptions> configureOptions)
    {
        builder.Host.Services.AddOptions<LocalizationOptions>()
            .Configure(configureOptions)
            .ValidateDataAnnotations()
            .ValidateOnStart();

        builder.AddInternalLocalization(typeof(LocalizationKernelExtensions).Assembly);
        return builder;
    }

    public static IKernelBuilder WithLocalization<TMarker>(this IKernelBuilder builder, string configurationSection = LocalizationOptions.SectionName)
    {
        builder.Host.Services.AddOptions<LocalizationOptions>()
            .BindConfiguration(configurationSection)
            .ValidateDataAnnotations()
            .ValidateOnStart();

        builder.AddInternalLocalization(typeof(TMarker).Assembly);
        return builder;
    }

    private static void AddInternalLocalization(this IKernelBuilder builder, System.Reflection.Assembly scanAssembly)
    {
        builder.Host.Services.AddLocalization();
        
        builder.Host.Services.AddOptions<Microsoft.Extensions.Localization.LocalizationOptions>()
            .Configure<IOptions<LocalizationOptions>>((options, customOptions) =>
            {
                options.ResourcesPath = customOptions.Value.ResourcesPath;
            });
    }

    public static WebApplication UseKernelLocalization(this WebApplication app)
    {
        var options = app.Services.GetRequiredService<IOptions<LocalizationOptions>>().Value;

        app.UseRequestLocalization(new RequestLocalizationOptions()
            .AddSupportedCultures(options.SupportedCultures)
            .AddSupportedUICultures(options.SupportedCultures)
            .SetDefaultCulture(options.DefaultCulture));

        return app;
    }
}
