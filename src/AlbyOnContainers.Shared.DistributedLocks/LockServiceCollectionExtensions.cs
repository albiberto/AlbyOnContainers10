using System.Reflection;
using AlbyOnContainers.Shared.DistributedLocks;
using AlbyOnContainers.Shared.DistributedLocks.Options;
using Medallion.Threading;
using Medallion.Threading.Redis;
using Microsoft.Extensions.Configuration;
using StackExchange.Redis;

// ReSharper disable once CheckNamespace
namespace Microsoft.Extensions.DependencyInjection;

public static class LockServiceCollectionExtensions
{
    extension(IServiceCollection services)
    {
        public IServiceCollection AddBlazorDistributedLocks(Action<DistributedLockOptions> configureOptions)
        {
            services.AddOptions<DistributedLockOptions>()
                .Configure(configureOptions)
                .ValidateDataAnnotations()
                .ValidateOnStart();

            return services.AddCoreDistributedLocks();
        }

        public IServiceCollection AddBlazorDistributedLocks(IConfiguration configuration, string sectionName = "DistributedLock")
        {
            services.AddOptions<DistributedLockOptions>()
                .Bind(configuration.GetSection(sectionName))
                .ValidateDataAnnotations()
                .ValidateOnStart();

            return services.AddCoreDistributedLocks();
        }

        public IServiceCollection AddBlazorDistributedLocks()
        {
            services.AddOptions<DistributedLockOptions>()
                .ValidateDataAnnotations()
                .ValidateOnStart();

            return services.AddCoreDistributedLocks();
        }

        private IServiceCollection AddCoreDistributedLocks()
        {
            services.PostConfigure<DistributedLockOptions>(options =>
            {
                if (!string.IsNullOrWhiteSpace(options.RedisChannel)) return;
                var projectName = Assembly.GetEntryAssembly()?.GetName().Name ?? "default-app";
                options.RedisChannel = $"{projectName}-locks".ToLowerInvariant();
            });

            services.AddSingleton<IDistributedLockProvider>(sp =>
            {
                var connection = sp.GetRequiredService<IConnectionMultiplexer>();
                return new RedisDistributedSynchronizationProvider(connection.GetDatabase());
            });

            services.AddSingleton<DistributedLockHostedService>();
            services.AddHostedService(sp => sp.GetRequiredService<DistributedLockHostedService>());
            services.AddSingleton(typeof(LockTracker<>));
            services.AddScoped(typeof(LockNotifier<>));

            return services;
        }
    }
}