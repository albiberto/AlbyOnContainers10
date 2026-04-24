using AlbyOnContainers.ServiceDefaults;
using AlbyOnContainers.Shared.Application.Abstract;
using AlbyOnContainers.Shared.Application.Infrastructure;
using AlbyOnContainers.Kernel.Security;
using MassTransit;
using Microsoft.FluentUI.AspNetCore.Components;
using ProductInformationManager.Application;
using ProductInformationManager.Infrastructure;
using ProductInformationManager.Web.DevSpace;
using ProductInformationManager.Web.Notifiers;


using AlbyOnContainers.Kernel;
using AlbyOnContainers.Kernel.Modules;
using ProductInformationManager.Infrastructure.Interceptors;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;

var builder = WebApplication.CreateBuilder(args);

// Aspire ServiceDefaults
builder.AddServiceDefaults();

// --- ALBY KERNEL SDK ---
// Using the new Fluent API to configure enterprise infrastructure centrally
builder.AddAlbyKernel()
    .WithSecurity()
    .WithMessaging(bus =>
    {
        bus.AddConsumers(typeof(Program).Assembly);
        bus.AddEntityFrameworkOutbox<ProductContext>(o =>
        {
            o.UsePostgres();
            o.UseBusOutbox(); 
        });
    })
    .WithMediator(configurator =>
    {
        configurator.AddConsumers(typeof(ApplicationServiceExtensions).Assembly);
    })
    .WithCaching()
    .WithDistributedLocks()
    .WithPersistence<ProductContext>("productdb", (sp, options) =>
    {
        // Add PIM-specific interceptors and options
        var environment = sp.GetRequiredService<IHostEnvironment>();
        var telemetry = sp.GetRequiredService<DbCommandTelemetryInterceptor>();
        
        options.AddInterceptors(telemetry);

        if (environment.IsDevelopment())
        {
            options.EnableDetailedErrors();
        }
    });

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

builder.Services.AddLocalization(options => options.ResourcesPath = "Resources");

var app = builder.Build();

app.UseRequestLocalization(new RequestLocalizationOptions()
    .AddSupportedCultures("en", "it")
    .AddSupportedUICultures("en", "it")
    .SetDefaultCulture("it"));

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
}

app.UseStaticFiles();
app.UseAntiforgery();

app.UseAuthentication();
app.UseAuthorization();

app.MapDefaultEndpoints();

app.MapRazorComponents<ProductInformationManager.Web.Components.App>()
    .AddInteractiveServerRenderMode();

app.Run();
