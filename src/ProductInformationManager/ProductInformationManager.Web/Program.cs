using AlbyOnContainers.Kernel;
using AlbyOnContainers.Kernel.Caching;
using AlbyOnContainers.Kernel.Localization;
using AlbyOnContainers.Kernel.Messaging;
using AlbyOnContainers.Kernel.Observability;
using AlbyOnContainers.Kernel.Persistence;
using AlbyOnContainers.Kernel.Security;
using AlbyOnContainers.Plugins.DistributedLocks;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.FluentUI.AspNetCore.Components;
using Npgsql;
using ProductInformationManager.Application;
using ProductInformationManager.Infrastructure;
using ProductInformationManager.Web.Consumers;
using ProductInformationManager.Web.Notifiers;

var builder = WebApplication.CreateBuilder(args);

// Aspire bootstraps the underlying clients (NpgsqlDataSource, IConnectionMultiplexer)
// from the connection strings injected by the AppHost. The kernel modules below
// resolve them from DI — no connection-string handling lives inside the kernel.
builder.AddNpgsqlDataSource("productdb");
builder.AddRedisClient("cache");

builder.AddKernel()
    .WithObservability()
    .WithSecurity()
    .WithLocalization()
    .WithCaching()
    .WithDistributedLocks()
    .WithPersistence<ProductContext>((sp, opt) =>
        opt.UseNpgsql(
            sp.GetRequiredService<NpgsqlDataSource>(),
            npgsql => npgsql.EnableRetryOnFailure()))
    .WithMessaging<ProductContext>("messaging", o => o.UsePostgres(), typeof(ApplicationServiceExtensions), typeof(CategoryEventsConsumer));

// Shared UI Notifier
builder.Services.Scan(scan => scan
    .FromAssemblyOf<INotifier>()
    .AddClasses(classes => classes.AssignableTo<INotifier>())
    .AsSelf()
    .WithSingletonLifetime()
);

// Application (IDomainEventMapper + validators)
builder.Services.AddApplication();

// Fluent UI Blazor
builder.Services.AddFluentUIComponents();

// Blazor
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

builder.Services.AddCascadingAuthenticationState();

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
}

app.UseStaticFiles();
app.UseAntiforgery();

app.UseKernelLocalization();

app.UseAuthentication();
app.UseAuthorization();

app.MapKernelObservabilityEndpoints();

app.MapRazorComponents<ProductInformationManager.Web.Components.App>()
    .AddInteractiveServerRenderMode();

app.Run();
