using Microsoft.EntityFrameworkCore;
using AlbyOnContainers.Kernel;
using AlbyOnContainers.Kernel.Localization;
using AlbyOnContainers.Kernel.Caching;
using AlbyOnContainers.Kernel.Messaging;
using AlbyOnContainers.Kernel.Observability;
using AlbyOnContainers.Kernel.Persistence;
using AlbyOnContainers.Kernel.Security;
using AlbyOnContainers.Kernel.Security.Abstractions;
using AlbyOnContainers.Plugins.DistributedLocks;
using MassTransit;
using Microsoft.FluentUI.AspNetCore.Components;
using ProductInformationManager.Application;
using ProductInformationManager.Infrastructure;
using ProductInformationManager.Web.DevSpace;
using ProductInformationManager.Web.Notifiers;

var builder = WebApplication.CreateBuilder(args);

// --- ALBY KERNEL SDK ---
// Using the new Fluent API to configure enterprise infrastructure centrally
builder.AddAlbyKernel()
    .WithObservability()
    .WithSecurity()
    .WithEfCorePersistence<ProductContext>((sp, options) => options.UseNpgsql(builder.Configuration.GetConnectionString("productdb")!))
    .WithMessaging(bus =>
    {
        bus.AddConsumers(typeof(Program).Assembly);
    })
    .WithEfCoreOutbox<ProductContext>(o => o.UsePostgres())
    .WithMediator(configurator =>
    {
        configurator.AddConsumers(typeof(ApplicationServiceExtensions).Assembly);
    })
    .WithCaching(typeof(ApplicationServiceExtensions).Assembly)
    .WithDistributedLocks()
    .WithLocalization();

// Shared UI Notifier
builder.Services.Scan(scan => scan
    .FromAssemblyOf<INotifier>()
    .AddClasses(classes => classes.AssignableTo<INotifier>())
    .AsSelf()
    .WithSingletonLifetime()
);

builder.Services.AddScoped<ICurrentUserService, StubCurrentUserService>();

// Application 
builder.Services.AddApplication();

// Infrastructure 
builder.Services.AddInfrastructure(builder.Configuration);

// Fluent UI Blazor
builder.Services.AddFluentUIComponents();

// Blazor
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

builder.Services.AddCascadingAuthenticationState();

var app = builder.Build();

app.UseKernelLocalization();

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
