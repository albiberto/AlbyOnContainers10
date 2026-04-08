using Ecommerce.Shared.Security;
using Microsoft.FluentUI.AspNetCore.Components;
using ProductDataManager.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

// Aspire ServiceDefaults
builder.AddServiceDefaults();

// Authentication via Keycloak
builder.Services.AddKeycloakAuthentication(builder.Configuration);

// Infrastructure (EF Core + MassTransit Mediator)
builder.Services.AddInfrastructure(
    builder.Configuration.GetConnectionString("productdb")!);

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

app.MapRazorComponents<ProductDataManager.Web.Components.App>()
    .AddInteractiveServerRenderMode();

app.Run();
