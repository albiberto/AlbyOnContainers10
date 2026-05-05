// ReSharper disable once CheckNamespace

namespace Microsoft.Extensions.DependencyInjection;

using AlbyOnContainers.Kernel;
using AlbyOnContainers.Kernel.Resilience.Enums;
using AlbyOnContainers.Kernel.Resilience.Options;
using Options;
using Polly;
using Polly.Registry;

public static class ResilienceExtensions
{
    extension(IKernelBuilder builder)
    {
        // --- DATABASE RESILIENCE ---
        public IKernelBuilder WithDatabaseResilience(string? configurationSection = null) => builder.ApplyResilienceCore(ResilienceKey.Database, configurationSection);

        public IKernelBuilder WithDatabaseResilience(Action<ResilienceOptions> configureOptions) => builder.ApplyResilienceCore(ResilienceKey.Database, configureOptions);

        // --- MESSAGING RESILIENCE ---
        public IKernelBuilder WithMessagingResilience(string? configurationSection = null) => builder.ApplyResilienceCore(ResilienceKey.Messaging, configurationSection);

        public IKernelBuilder WithMessagingResilience(Action<ResilienceOptions> configureOptions) => builder.ApplyResilienceCore(ResilienceKey.Messaging, configureOptions);

        // --- PRIVATE CORE LOGIC ---
        private IKernelBuilder ApplyResilienceCore(ResilienceKey key, string? configurationSection)
        {
            var section = configurationSection ?? $"{ResilienceOptions.Section}:{key}";

            builder.Services.BindOptions(key, section);
            builder.Services.AddResiliencePipeline(key);
            builder.Services.AddKeyedBridge(key);

            return builder;
        }

        private IKernelBuilder ApplyResilienceCore(ResilienceKey key, Action<ResilienceOptions> configureOptions)
        {
            builder.Services.ConfigureOptions(key, configureOptions);
            builder.Services.AddResiliencePipeline(key);
            builder.Services.AddKeyedBridge(key);

            return builder;
        }
    }

    // --- PRIVATE INFRASTRUCTURE HELPERS ---
    extension(IServiceCollection services)
    {
        private void BindOptions(ResilienceKey key, string section) =>
            services.AddOptions<ResilienceOptions>($"{key}")
                .BindConfiguration(section)
                .ValidateDataAnnotations()
                .ValidateOnStart();

        private void ConfigureOptions(ResilienceKey key, Action<ResilienceOptions> configureOptions) =>
            services.AddOptions<ResilienceOptions>($"{key}")
                .Configure(configureOptions)
                .ValidateDataAnnotations()
                .ValidateOnStart();

        private void AddKeyedBridge(ResilienceKey key) =>
            services.AddKeyedSingleton<ResiliencePipeline>(key, (provider, injectionKey) =>
                provider
                    .GetRequiredService<ResiliencePipelineProvider<ResilienceKey>>()
                    .GetPipeline((ResilienceKey)injectionKey!));

        private void AddResiliencePipeline(ResilienceKey key)
        {
            var keyName = $"{key}";

            services.AddResiliencePipeline(key, (builder, context) =>
            {
                var options = context.ServiceProvider.GetRequiredService<IOptionsMonitor<ResilienceOptions>>().Get(keyName);

                builder.AddTimeout(options.OverallTimeout);

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
}