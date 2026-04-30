using System.Reflection;
using AlbyOnContainers.Kernel;
using AlbyOnContainers.Plugins.DistributedLocks.Options;
using Medallion.Threading;
using Medallion.Threading.Redis;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using StackExchange.Redis;

namespace AlbyOnContainers.Plugins.DistributedLocks;

using HostedServices;

public static class DistributedLocksPluginExtensions
{
    extension(IKernelBuilder builder)
    {
        public IKernelBuilder WithDistributedLocks(Action<DistributedLockOptions> configureOptions)
        {
            builder.Host.Services.AddOptions<DistributedLockOptions>()
                .Configure(configureOptions)
                .ValidateDataAnnotations()
                .ValidateOnStart();

            builder.Host.Services.AddCoreDistributedLocks(builder.Host.Configuration);
    
            return builder;
        }

        public IKernelBuilder WithDistributedLocks(string? sectionName = null)
        {
            builder.Host.Services.AddOptions<DistributedLockOptions>()
                .Bind(builder.Host.Configuration.GetSection(sectionName ?? DistributedLockOptions.Section))
                .ValidateDataAnnotations()
                .ValidateOnStart();

            builder.Host.Services.AddCoreDistributedLocks(builder.Host.Configuration);

            return builder;
        }
    }

    private static void AddCoreDistributedLocks(this IServiceCollection services, IConfiguration configuration)
    {
        services.PostConfigure<DistributedLockOptions>(options =>
        {
            if (!string.IsNullOrWhiteSpace(options.RedisChannel)) return;
            var projectName = Assembly.GetEntryAssembly()?.GetName().Name ?? "default-app";
            options.RedisChannel = $"{projectName}-locks".ToLowerInvariant();
        });
        
        services.TryAddSingleton<IConnectionMultiplexer>(sp =>
        {
            var options = sp.GetRequiredService<IOptions<DistributedLockOptions>>().Value;
            var redisConnection = configuration.GetConnectionString(options.ConnectionStringName) 
                ?? throw new InvalidOperationException($"Connection string '{options.ConnectionStringName}' not found. Distributed Locks cannot be established.");
            
            return ConnectionMultiplexer.Connect(redisConnection);
        });

        services.AddSingleton<IDistributedLockProvider>(sp =>
        {
            var connection = sp.GetRequiredService<IConnectionMultiplexer>();
            return new RedisDistributedSynchronizationProvider(connection.GetDatabase());
        });

        services.AddSingleton<DistributedLockHostedService>();
        services.AddHostedService(sp => sp.GetRequiredService<DistributedLockHostedService>());
        services.AddSingleton(typeof(LockTracker<>));
        services.AddSingleton(typeof(LockNotifier<>));
    }
}