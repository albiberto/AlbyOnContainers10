using AlbyOnContainers.Kernel;
using AlbyOnContainers.Kernel.Caching;
using AlbyOnContainers.Kernel.Messaging;
using AlbyOnContainers.Kernel.Observability;
using AlbyOnContainers.Kernel.Persistence;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.FluentUI.AspNetCore.Components;
using ProductInformationManager.Application;
using ProductInformationManager.Application.Cache;
using ProductInformationManager.Infrastructure;
using ProductInformationManager.Web.Notifiers;

var builder = WebApplication.CreateBuilder(args);

// --- Connection strings injected by Aspire ---
var pgConn = builder.Configuration.GetConnectionString("productdb")
    ?? throw new InvalidOperationException("Missing connection string 'productdb'.");
var redisConn = builder.Configuration.GetConnectionString("cache")
    ?? throw new InvalidOperationException("Missing connection string 'cache'.");
var amqpConn = builder.Configuration.GetConnectionString("messaging")
    ?? throw new InvalidOperationException("Missing connection string 'messaging'.");
var amqpUri = new Uri(amqpConn);
var amqpUserInfo = amqpUri.UserInfo.Split(':', 2);

// --- ALBY KERNEL SDK ---
// Single fluent chain that wires Observability, Persistence (EF Core + auto-migrations),
// Caching (FusionCache L1+L2) and Messaging (MassTransit + Mediator + Outbox).
builder.AddKernel()
    .WithObservability(opt =>
    {
        opt.ServiceName = "ProductInformationManager.Web";
        opt.Namespace = "AlbyOnContainers";
    })
    .WithPersistence<ProductContext>((_, opt) => opt.UseNpgsql(pgConn))
    .WithCaching<CategoryCache>(opt => opt.RedisConnectionString = redisConn)
    .WithMessaging<ProductContext, CreateCategoryConsumer>(
        opt =>
        {
            opt.Host = amqpUri.Host;
            opt.Username = Uri.UnescapeDataString(amqpUserInfo[0]);
            opt.Password = Uri.UnescapeDataString(amqpUserInfo.Length > 1 ? amqpUserInfo[1] : string.Empty);
        },
        outbox =>
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
