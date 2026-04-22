using AlbyOnContainers.ServiceDefaults;
using AlbyOnContainers.Shared.Application.Infrastructure;
using AlbyOnContainers.Shared.Security;
using MassTransit;
using Microsoft.FluentUI.AspNetCore.Components;
using ProductInformationManager.Application;
using ProductInformationManager.Infrastructure;
using ProductInformationManager.Web.Notifiers;

var builder = WebApplication.CreateBuilder(args);

// Aspire ServiceDefaults
builder.AddServiceDefaults();

// Authentication via Keycloak
builder.Services.AddKeycloakAuthentication(builder.Configuration);

// Shared UI Notifier — registra solo le classi che implementano INotifier
builder.Services.Scan(scan => scan
    .FromAssemblyOf<INotifier>()
    .AddClasses(classes => classes.AssignableTo<INotifier>())
    .AsSelf()
    .WithSingletonLifetime()
);

builder.Services.AddMassTransit(x =>
{
    x.DisableUsageTelemetry();
    x.SetKebabCaseEndpointNameFormatter();

    // Aggiunge i consumer del progetto Web (quelli della UI)
    x.AddConsumers(typeof(Program).Assembly);

    x.UsingRabbitMq((context, cfg) =>
    {
        var connectionString = builder.Configuration.GetConnectionString("messaging") 
            ?? throw new InvalidOperationException("Connection string 'messaging' not found.");
        cfg.Host(connectionString);
        cfg.ConfigurePimConsumePipeline(context);
        cfg.ConfigureEndpoints(context);
    });
});

// Application (MassTransit)
builder.Services.AddApplication(builder.Configuration);

// Infrastructure (EF Core)
builder.Services.AddInfrastructure(builder.Configuration);

// Fluent UI Blazor
builder.Services.AddFluentUIComponents();

// Distributed Lock
builder.Services.AddBlazorDistributedLocks(builder.Configuration);

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
