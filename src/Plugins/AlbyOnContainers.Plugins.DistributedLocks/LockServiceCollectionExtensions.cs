using System.Reflection;
using AlbyOnContainers.Kernel.Abstraction;
using AlbyOnContainers.Plugins.DistributedLocks.Options;
using Medallion.Threading;
using Medallion.Threading.Redis;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using StackExchange.Redis;

namespace AlbyOnContainers.Plugins.DistributedLocks;

public static class DistributedLocksPluginExtensions
{
    public static IKernelBuilder WithDistributedLocks(this IKernelBuilder builder, Action<DistributedLockOptions> configureOptions)
    {
        builder.Host.Services.AddOptions<DistributedLockOptions>()
            .Configure(configureOptions)
            .ValidateDataAnnotations()
            .ValidateOnStart();

        builder.Host.Services.AddCoreDistributedLocks(builder.Host.Configuration);
    
        return builder;
    }

    public static IKernelBuilder WithDistributedLocks(this IKernelBuilder builder, string sectionName = "DistributedLock")
    {
        builder.Host.Services.AddOptions<DistributedLockOptions>()
            .Bind(builder.Host.Configuration.GetSection(sectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        builder.Host.Services.AddCoreDistributedLocks(builder.Host.Configuration);

        return builder;
    }

    private static void AddCoreDistributedLocks(this IServiceCollection services, IConfiguration configuration)
    {
        services.PostConfigure<DistributedLockOptions>(options =>
        {
            if (!string.IsNullOrWhiteSpace(options.RedisChannel)) return;
            var projectName = Assembly.GetEntryAssembly()?.GetName().Name ?? "default-app";
            options.RedisChannel = $"{projectName}-locks".ToLowerInvariant();
        });
        
        var redisConnection = configuration.GetConnectionString("cache") ?? throw new InvalidOperationException("Connection string 'cache' not found. Distributed Locks cannot be established.");

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
    }
}