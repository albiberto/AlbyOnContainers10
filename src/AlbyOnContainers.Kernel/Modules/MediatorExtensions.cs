using System;
using AlbyOnContainers.Shared.Application.Infrastructure;
using MassTransit;
using Microsoft.Extensions.DependencyInjection;

namespace AlbyOnContainers.Kernel.Modules;

public static class MediatorExtensions
{
    /// <summary>
    /// Configures MassTransit Mediator for In-Process CQRS.
    /// Automatically applies enterprise-grade telemetry, exception handling, and validation filters.
    /// </summary>
    /// <param name="builder">The kernel builder.</param>
    /// <param name="configureConsumers">Action to configure consumers.</param>
    /// <returns>The kernel builder for chaining.</returns>
    public static IAlbyKernelBuilder WithMediator(
        this IAlbyKernelBuilder builder,
        Action<IMediatorRegistrationConfigurator> configureConsumers)
    {
        builder.Services.AddMediator(configurator =>
        {
            // Register developers' specific consumers
            configureConsumers(configurator);

            // OBBLIGATORIO: Apply the global Kernel filters (Telemetry, Exceptions, Validation)
            configurator.ConfigureMediatorPipeline();
        });

        return builder;
    }
}
