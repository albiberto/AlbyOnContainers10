using AlbyOnContainers.Kernel;
using AlbyOnContainers.Kernel.Caching;
using AlbyOnContainers.Kernel.Messaging;
using AlbyOnContainers.Kernel.Observability;
using AlbyOnContainers.Kernel.Persistence;
using AlbyOnContainers.Kernel.Security;
using AlbyOnContainers.Plugins.DistributedLocks;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.FluentUI.AspNetCore.Components;
using ProductInformationManager.Application;
using ProductInformationManager.Application.Cache;
using ProductInformationManager.Application.Consumers;
using ProductInformationManager.Infrastructure;
using ProductInformationManager.Web.Notifiers;

var builder = WebApplication.CreateBuilder(args);

// --- Bridge Aspire connection strings to kernel option sections ---
// Aspire injects ConnectionStrings:{name}; the kernel binds typed options from sections
// (Caching, Messaging, ...). We materialize the bridge ONCE here, so the rest of the
// kernel chain stays 100% declarative (no per-module lambdas for plain config).
var pgConn = builder.Configuration.GetConnectionString("productdb")
    ?? throw new InvalidOperationException("Missing connection string 'productdb'.");
var redisConn = builder.Configuration.GetConnectionString("cache")
    ?? throw new InvalidOperationException("Missing connection string 'cache'.");
var amqpUri = new Uri(builder.Configuration.GetConnectionString("messaging")
    ?? throw new InvalidOperationException("Missing connection string 'messaging'."));
var amqpUserInfo = amqpUri.UserInfo.Split(':', 2);

builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
{
    ["Caching:RedisConnectionString"] = redisConn,
    ["Messaging:Host"]                = amqpUri.Host,
    ["Messaging:Port"]                = amqpUri.Port.ToString(),
    ["Messaging:Username"]            = Uri.UnescapeDataString(amqpUserInfo[0]),
    ["Messaging:Password"]            = Uri.UnescapeDataString(amqpUserInfo.Length > 1 ? amqpUserInfo[1] : string.Empty),
});

// --- ALBY KERNEL SDK ---
// Pure declarative chain. Lambdas appear ONLY where a runtime-only behavior is required:
//  - Persistence: choosing the EF Core provider (UseNpgsql).
//  - Messaging:   wiring the MassTransit Outbox.
// Everything else is bound from appsettings.json sections (Observability, Keycloak,
// Persistence, Caching, DistributedLock, Messaging).
builder.AddKernel()
    .WithObservability()
    .WithSecurity()
    .WithPersistence<ProductContext>((_, opt) => opt.UseNpgsql(pgConn))
    .WithCaching<CategoryCache>()
    .WithDistributedLocks()
    .WithMessaging<ProductContext, CreateCategoryConsumer>(outbox =>
    {
        outbox.UsePostgres();
        outbox.UseBusOutbox();
    });

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

app.UseAuthentication();
app.UseAuthorization();

app.MapKernelObservabilityEndpoints();

app.MapRazorComponents<ProductInformationManager.Web.Components.App>()
    .AddInteractiveServerRenderMode();

app.Run();
