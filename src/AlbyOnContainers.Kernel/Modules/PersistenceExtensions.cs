using System;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace AlbyOnContainers.Kernel.Modules;

public static class PersistenceExtensions
{
    /// <summary>
    /// Configures Entity Framework Core with PostgreSQL.
    /// Automatically registers and attaches the AuditableEntityInterceptor to manage audit trails.
    /// </summary>
    /// <typeparam name="TDbContext">The type of the DbContext.</typeparam>
    /// <param name="builder">The kernel builder.</param>
    /// <param name="connectionStringName">The configuration name for the connection string.</param>
    /// <returns>The kernel builder for chaining.</returns>
    public static IAlbyKernelBuilder WithPersistence<TDbContext>(
        this IAlbyKernelBuilder builder, 
        string connectionStringName,
        Action<IServiceProvider, DbContextOptionsBuilder>? configureOptions = null) 
        where TDbContext : DbContext
    {
        var connectionString = builder.Configuration.GetConnectionString(connectionStringName) 
            ?? throw new InvalidOperationException($"Connection string '{connectionStringName}' not found. Persistence cannot be established.");

        // Register the interceptor as Scoped so it can resolve ICurrentUserService
        builder.Services.AddScoped<AuditableEntityInterceptor>();

        builder.Services.AddDbContext<TDbContext>((sp, options) =>
        {
            options.UseNpgsql(connectionString, npgsql => npgsql.EnableRetryOnFailure());

            // Dynamically resolve and add the interceptor
            var interceptor = sp.GetRequiredService<AuditableEntityInterceptor>();
            options.AddInterceptors(interceptor);

            configureOptions?.Invoke(sp, options);
        });

        // Enforce the DbContext as Scoped (Best Practice)
        builder.Services.AddScoped<DbContext>(sp => sp.GetRequiredService<TDbContext>());

        return builder;
    }
}
