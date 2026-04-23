using System.Reflection;
using AlbyOnContainers.Kernel.DistributedLocks;
using AlbyOnContainers.Kernel.DistributedLocks.Options;
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
        public IServiceCollection AddDistributedLocks(IConfiguration configuration, Action<DistributedLockOptions> configureOptions)
        {
            services.AddOptions<DistributedLockOptions>()
                .Configure(configureOptions)
                .ValidateDataAnnotations()
                .ValidateOnStart();

            return services.AddCoreDistributedLocks(configuration);
        }

        public IServiceCollection AddDistributedLocks(IConfiguration configuration, string sectionName = "DistributedLock")
        {
            services.AddOptions<DistributedLockOptions>()
                .Bind(configuration.GetSection(sectionName))
                .ValidateDataAnnotations()
                .ValidateOnStart();

            return services.AddCoreDistributedLocks(configuration);
        }

        private IServiceCollection AddCoreDistributedLocks(IConfiguration configuration)
        {
            services.PostConfigure<DistributedLockOptions>(options =>
            {
                if (!string.IsNullOrWhiteSpace(options.RedisChannel)) return;
                var projectName = Assembly.GetEntryAssembly()?.GetName().Name ?? "default-app";
                options.RedisChannel = $"{projectName}-locks".ToLowerInvariant();
            });
            
            var redisConnection = configuration.GetConnectionString("cache") ?? throw new InvalidOperationException("Connection string 'cache' not found.");

            services.AddSingleton<IConnectionMultiplexer>(_ => ConnectionMultiplexer.Connect(redisConnection));

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