# AlbyonContainers - Product Information Manager

## Overview

A Product Information Management (PIM) system built with .NET 10, Blazor, and PostgreSQL. Manages product categories, attributes, and descriptions for e-commerce catalogs.

## Tech Stack

- **Framework**: .NET 10.0
- **Frontend**: Blazor Web (Server-Side Rendering) with Microsoft Fluent UI
- **Database**: PostgreSQL with Entity Framework Core (EF Core) and `ltree` extension for hierarchical categories
- **Architecture**: Clean Architecture / Onion Architecture
- **Auth**: Keycloak (OIDC/OAuth2) - configured but Keycloak container not running in dev
- **Mediator**: MassTransit (in-memory)
- **Validation**: FluentValidation
- **Observability**: OpenTelemetry

## Project Structure

```
AlbyonContainers.sln
src/
  AlbyonContainers.AppHost/          # .NET Aspire orchestrator (not used in Replit)
  AlbyonContainers.ServiceDefaults/  # Shared OpenTelemetry/ServiceDiscovery config
  AlbyOnContainers.Shared.Conctracts/ # Shared message contracts
  AlbyOnContainers.Shared.Domain/    # Base domain classes (AuditableEntity)
  AlbyOnContainers.Shared.Security/  # Keycloak auth extensions
  ProductInformationManager.Application/ # CQRS handlers, MassTransit consumers
  ProductInformationManager.Domain/  # Entities, value objects, domain exceptions
  ProductInformationManager.Infrastructure/ # EF Core, PostgreSQL, migrations
  ProductInformationManager.Messages/ # Message contracts
  ProductInformationManager.Web/     # Blazor frontend (entry point)
  keycloak/                          # Keycloak realm config
```

## Running the App

The app runs via the "Start application" workflow:
- Command: `dotnet run --project src/ProductInformationManager.Web/`
- Port: 5000 (0.0.0.0)
- .NET SDK: 10.0 (from `/nix/store/5hfn7q3adjwa8dh4yhhw1ip8njcbs7vs-dotnet-sdk-wrapped-10.0.101/bin`)
- Environment: Development

## Database

- Uses Replit's built-in PostgreSQL database
- `ltree` extension enabled for hierarchical category paths
- EF Core migrations applied automatically on startup via `MigrationHostedService`
- Connection string in `src/ProductInformationManager.Web/appsettings.Development.json`

## Description Types — IsGlobal Feature

The Description Types module was refactored to support:

- **IsGlobal flag**: When `true`, the type applies to every category automatically (no category rules needed). When `false` ("Specialized"), explicit `CategoryDescriptionRule` rows link it to one or more categories.
- **Category multi-select**: The edit/create dialog uses `FluentAutocomplete` backed by the `GetAllCategoriesFlat` query.
- **FluentDataGrid UI**: 6 columns — Name, Description, Scope badge (Global/Specialized), Values tag cloud, Categories tag cloud, Actions.
- **Validation**: `DescriptionTypeCategoriesRequired` enforces at least one category when `IsGlobal = false`.
- **Migration `20260410120000_AddIsGlobalToDescriptionTypes`**: Added `IsGlobal boolean NOT NULL DEFAULT false` to `DescriptionTypes` table. Applied manually via SQL (Designer.cs created for tooling).

### Key files changed
- `src/ProductInformationManager.Domain/DescriptionType.cs` — `IsGlobal` property + `UpdateMetadata()` returning invariant signal
- `src/ProductInformationManager.Messages/DescriptionTypeMessages.cs` — updated commands/DTOs
- `src/ProductInformationManager.Application/DescriptionTypes.Commands.cs` — diff-based sync of category associations
- `src/ProductInformationManager.Application/DescriptionTypes.Queries.cs` — includes `IsGlobal` and `CategoryIds`
- `src/ProductInformationManager.Application/Validators/DescriptionTypeValidators.cs` — category requirement rule
- `src/ProductInformationManager.Application/Resources/ValidationMessages.*` — new `DescriptionTypeCategoriesRequired` key
- `src/ProductInformationManager.Infrastructure/Migrations/20260410120000_*` — new migration + designer
- `src/ProductInformationManager.Web/Components/Pages/DescriptionTypes/DescriptionTypesPage.razor` — full UI rewrite
- `src/ProductInformationManager.Web/Components/Pages/DescriptionTypes/DescriptionTypesPage.razor.css` — new CSS

## Important Notes

### Case Sensitivity Fix
The solution was authored on Windows (case-insensitive). Several path fixes were needed:
- `AlbyonContainers.sln`: Fixed backslashes to forward slashes and corrected `AlbyOnContainers.AppHost` → `AlbyonContainers.AppHost` and `AlbyOnContainers.ServiceDefaults` → `AlbyonContainers.ServiceDefaults`
- `ProductInformationManager.Web.csproj`: Fixed ServiceDefaults reference path casing
- `AlbyOnContainers.Shared.Security.csproj`: Fixed ServiceDefaults reference path casing

### Authentication
Keycloak is configured but not running. The app will show a Login button but authentication is disabled/non-functional in development without the Keycloak container.

### .NET SDK Path
The .NET 10 SDK is available at:
- Wrapped: `/nix/store/5hfn7q3adjwa8dh4yhhw1ip8njcbs7vs-dotnet-sdk-wrapped-10.0.101/bin`
- Direct: `/nix/store/bjzmfa360s8f3n4xqlnkamy13fkywb2x-dotnet-sdk-10.0.101/bin`
