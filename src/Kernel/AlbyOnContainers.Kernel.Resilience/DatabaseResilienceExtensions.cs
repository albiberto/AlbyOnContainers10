// ReSharper disable once CheckNamespace

namespace Microsoft.Extensions.DependencyInjection;

using AlbyOnContainers.Kernel;
using AlbyOnContainers.Kernel.Resilience.Options;
using Options;
using Polly;
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

                // Polly v8 execution order is outer-to-inner (top-to-bottom).
                // 1. Overall Timeout (encompasses all retries)
                builder.AddTimeout(options.OverallTimeout);

                // 2. Retry Strategy
                builder.AddRetry(new()
                {
                    MaxRetryAttempts = options.MaxRetryAttempts,
                    Delay = options.InitialDelay,
                    BackoffType = options.UseExponentialBackoff ? DelayBackoffType.Exponential : DelayBackoffType.Constant,
                    ShouldHandle = new PredicateBuilder().Handle<Exception>()
                });
            });
    }
}