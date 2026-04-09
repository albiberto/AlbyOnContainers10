using AlbyOnContainers.ServiceDefaults;
using AlbyOnContainers.Shared.Security;
using Microsoft.FluentUI.AspNetCore.Components;
using ProductInformationManager.Application;
using ProductInformationManager.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

// Aspire ServiceDefaults
builder.AddServiceDefaults();

// Authentication via Keycloak
builder.Services.AddKeycloakAuthentication(builder.Configuration);

// Application (MassTransit)
builder.Services.AddApplication();

// Infrastructure (EF Core)
var connection = builder.Configuration.GetConnectionString("productdb") ?? throw new InvalidOperationException("Connection string 'productdb' not found.");
builder.Services.AddInfrastructure(connection);

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

app.MapDefaultEndpoints();

app.MapRazorComponents<ProductInformationManager.Web.Components.App>()
    .AddInteractiveServerRenderMode();

app.Run();
