using AlbyOnContainers.Kernel;
using AlbyOnContainers.Kernel.Caching;
using AlbyOnContainers.Kernel.Localization;
using AlbyOnContainers.Kernel.Messaging;
using AlbyOnContainers.Kernel.Observability;
using AlbyOnContainers.Kernel.Persistence;
using AlbyOnContainers.Kernel.Resilience;
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

var databaseConnection = builder.Configuration.GetConnectionString("productdb") ?? throw new InvalidOperationException("Missing connection string 'productdb'.");
var redisConnection = builder.Configuration.GetConnectionString("cache") ?? throw new InvalidOperationException("Missing connection string 'cache'.");
var rabbitConnection = new Uri(builder.Configuration.GetConnectionString("messaging") ?? throw new InvalidOperationException("Missing connection string 'messaging'."));

builder.AddKernel()
    .WithResilience("Database")
    .WithResilience("Messaging");

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
