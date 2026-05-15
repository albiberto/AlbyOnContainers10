// ReSharper disable once CheckNamespace
namespace Microsoft.Extensions.DependencyInjection;

using System;
using AlbyOnContainers.Kernel;
using AlbyOnContainers.Kernel.Domain.Exceptions;
using AlbyOnContainers.Kernel.Resilience.Options;
using Options;
using Polly;
using Polly.CircuitBreaker;
using Polly.Registry;

public static class ResilienceExtensions
{
    // --- PUBLIC FACADE LOGIC ---

    extension(IKernelBuilder builder)
    {
        public IKernelBuilder WithResilience(string key, string? configurationSection = null)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(key);
            
            builder.Services.AddKeyedOptions<ResilienceOptions>(key, configurationSection);

            var section = configurationSection ?? $"{ResilienceOptions.Section}:{key}";

            builder.Services.AddOptions<ResilienceOptions>(key)
                .BindConfiguration(section)
                .ValidateDataAnnotations()
                .ValidateOnStart();
            
            builder.Services.AddResiliencePipelineInternal(key);
            builder.Services.AddKeyedBridge(key);

            return builder;
        }

        public IKernelBuilder WithResilience(string key, Action<ResilienceOptions> configureOptions)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(key);
            ArgumentNullException.ThrowIfNull(configureOptions);

            builder.Services.AddOptions<ResilienceOptions>(key)
                .Configure(configureOptions)
                .ValidateDataAnnotations()
                .ValidateOnStart();
            
            builder.Services.AddResiliencePipelineInternal(key);
            builder.Services.AddKeyedBridge(key);

            return builder;
        }
    }

    // --- PRIVATE INFRASTRUCTURE HELPERS ---

    extension(IServiceCollection services)
    {
        private void AddKeyedBridge(string key) =>
            services.AddKeyedSingleton<ResiliencePipeline>(key, (provider, injectionKey) =>
                provider
                    .GetRequiredService<ResiliencePipelineProvider<string>>()
                    .GetPipeline((string)injectionKey!));

        private void AddResiliencePipelineInternal(string key) =>
            services.AddResiliencePipeline(key, (pipelineBuilder, context) =>
            {
                context.EnableReloads<ResilienceOptions>(key);

                var optionsMonitor = context.ServiceProvider.GetRequiredService<IOptionsMonitor<ResilienceOptions>>();
                var options = optionsMonitor.Get(key);

                pipelineBuilder.AddTimeout(options.OverallTimeout);

                pipelineBuilder.AddRetry(new()
                {
                    MaxRetryAttempts = options.MaxRetryAttempts,
                    Delay = options.Delay,
                    BackoffType = options.UseExponentialBackoff ? DelayBackoffType.Exponential : DelayBackoffType.Constant,
                    ShouldHandle = new PredicateBuilder().Handle<Exception>(ex => ex is not (OperationCanceledException or BrokenCircuitException or IsolatedCircuitException or DomainException))
                });

                pipelineBuilder.AddCircuitBreaker(new()
                {
                    FailureRatio = options.CircuitBreaker.FailureRatio,
                    MinimumThroughput = options.CircuitBreaker.MinimumThroughput,
                    BreakDuration = options.CircuitBreaker.BreakDuration,
                    SamplingDuration = options.CircuitBreaker.SamplingDuration,
                    ShouldHandle = new PredicateBuilder().Handle<Exception>(ex => ex is not OperationCanceledException)
                });
            });
    }
}