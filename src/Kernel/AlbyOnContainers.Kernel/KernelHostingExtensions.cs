using AlbyOnContainers.Kernel.Abstraction;
using AlbyOnContainers.Kernel.Security;
using AlbyOnContainers.Kernel.Security.Options;
using MassTransit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace AlbyOnContainers.Kernel;

public static class KernelHostingExtensions
{
    public static IKernelBuilder AddKernel(this IHostApplicationBuilder builder)
    {
        return new KernelBuilder(builder);
    }
    
    // --- SECURITY ---
    public static IKernelBuilder WithSecurity(this IKernelBuilder builder, string sectionName = "Keycloak")
    {
        builder.Host.Services.AddKeycloakAuthentication(builder.Host.Configuration, sectionName);
        return builder;
    }

    public static IKernelBuilder WithSecurity(this IKernelBuilder builder, Action<KeycloakOptions> configureOptions)
    {
        builder.Host.Services.AddKeycloakAuthentication(configureOptions);
        return builder;
    }

    // --- MESSAGING ---
    public static IKernelBuilder WithMessaging(this IKernelBuilder builder, Action<IBusRegistrationConfigurator>? configure = null)
    {
        builder.Host.Services.AddMassTransitDefaults(builder.Host.Configuration, configure);
        return builder;
    }

    // --- CACHING ---
    public static IKernelBuilder WithCaching(this IKernelBuilder builder)
    {
        builder.Host.Services.AddAlbyCachingDefaults(builder.Host.Configuration); 
        return builder;
    }
}