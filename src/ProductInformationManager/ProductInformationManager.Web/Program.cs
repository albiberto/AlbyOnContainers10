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
    .WithPersistence<ProductContext>((sp, opt) =>
        opt.UseNpgsql(sp.GetRequiredService<NpgsqlDataSource>()))
    .WithMessaging<ProductContext>(
        configureOptions: msg =>
        {
            // RabbitMQ connection string from Aspire is of the form amqp://user:pass@host:port
            var uri = new Uri(builder.Configuration.GetConnectionString("messaging")
                              ?? throw new InvalidOperationException("Missing connection string 'messaging'."));
            msg.Host = uri.Host;
            msg.Port = uri.IsDefaultPort ? 5672 : uri.Port;
            var userInfo = uri.UserInfo.Split(':', 2);
            msg.Username = Uri.UnescapeDataString(userInfo[0]);
            msg.Password = userInfo.Length > 1 ? Uri.UnescapeDataString(userInfo[1]) : string.Empty;
        },
        configureOutbox: o => o.UsePostgres(),
        // Scan both the Application assembly (mediator/event consumers) and the Web assembly
        // (UI-side event consumers like CategoryEventsConsumer).
        assemblyMarkers: new[] { typeof(ApplicationServiceExtensions), typeof(CategoryEventsConsumer) });

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
