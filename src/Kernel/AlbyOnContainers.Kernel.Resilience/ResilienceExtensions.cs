// ReSharper disable once CheckNamespace

namespace Microsoft.Extensions.DependencyInjection;

using AlbyOnContainers.Kernel;
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

            var section = configurationSection ?? $"{ResilienceOptions.Section}:{key}";

            builder.Services.BindOptions(key, section);
            builder.Services.AddResiliencePipelineInternal(key);
            builder.Services.AddKeyedBridge(key);

            return builder;
        }

        public IKernelBuilder WithResilience(string key, Action<ResilienceOptions> configureOptions)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(key);
            ArgumentNullException.ThrowIfNull(configureOptions);

            builder.Services.ConfigureOptions(key, configureOptions);
            builder.Services.AddResiliencePipelineInternal(key);
            builder.Services.AddKeyedBridge(key);

            return builder;
        }
    }

    // --- PRIVATE INFRASTRUCTURE HELPERS ---

    extension(IServiceCollection services)
    {
        private void BindOptions(string key, string section) =>
            services.AddOptions<ResilienceOptions>(key)
                .BindConfiguration(section)
                .ValidateDataAnnotations()
                .ValidateOnStart();

        private void ConfigureOptions(string key, Action<ResilienceOptions> configureOptions) =>
            services.AddOptions<ResilienceOptions>(key)
                .Configure(configureOptions)
                .ValidateDataAnnotations()
                .ValidateOnStart();

        private void AddKeyedBridge(string key) =>
            services.AddKeyedSingleton<ResiliencePipeline>(key, (provider, injectionKey) =>
                provider
                    .GetRequiredService<ResiliencePipelineProvider<string>>()
                    .GetPipeline((string)injectionKey!));

        private void AddResiliencePipelineInternal(string key) =>
            services.AddResiliencePipeline(key, (builder, context) =>
            {
                var optionsMonitor = context.ServiceProvider.GetRequiredService<IOptionsMonitor<ResilienceOptions>>();
                var options = optionsMonitor.Get(key);

                // Polly v8 execution order is outer-to-inner (top-to-bottom):
                // Timeout (outer) -> Retry -> Circuit Breaker (inner, opt-in).
                builder.AddTimeout(options.OverallTimeout);

                builder.AddRetry(new()
                {
                    MaxRetryAttempts = options.MaxRetryAttempts,
                    Delay = options.Delay,
                    BackoffType = options.UseExponentialBackoff ? DelayBackoffType.Exponential : DelayBackoffType.Constant,
                    // Skip retry on cancellation (cooperative cancellation must propagate).
                    // Skip retry on a broken circuit: the breaker decides when traffic resumes.
                    ShouldHandle = new PredicateBuilder()
                        .Handle<Exception>(ex => ex is not (OperationCanceledException or BrokenCircuitException or IsolatedCircuitException))
                });

                if (options.CircuitBreaker is { } circuit)
                {
                    builder.AddCircuitBreaker(new()
                    {
                        FailureRatio = circuit.FailureRatio,
                        MinimumThroughput = circuit.MinimumThroughput,
                        BreakDuration = circuit.BreakDuration,
                        SamplingDuration = circuit.SamplingDuration,
                        // The breaker should not count cancellations against the failure ratio.
                        ShouldHandle = new PredicateBuilder()
                            .Handle<Exception>(ex => ex is not OperationCanceledException)
                    });
                }
            });
    }
}
