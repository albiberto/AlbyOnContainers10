---
name: infra-efcore-postgresql
description: Architectural guidelines for the Infrastructure layer. MANDATORY when working with Entity Framework Core (DbContext, Migrations, Fluent API mappings), PostgreSQL-specific features, and Interceptors.
---

## Objective
Manage the Data Access layer using EF Core 10, PostgreSQL, and strict Fluent API configurations, ensuring Domain encapsulation is maintained at the database level.

## MANDATORY Architectural Constraints

1. **FLUENT API EXCLUSIVELY:** You MUST configure all database mappings inside the `OnModelCreating` method of the `DbContext`. The use of Data Annotations (e.g., `[Table]`, `[Key]`) in Domain entities is STRICTLY PROHIBITED.
2. **STRONGLY-TYPED ID CONVERSION:** Because the Domain uses C# records for IDs (e.g., `ProductId`), you MUST configure EF Core to translate them to `Guid` using `ValueConverter<T, Guid>`. Apply these converters in bulk using `ConfigureConventions` or map them explicitly in `OnModelCreating`.
3. **POSTGRESQL SPECIFICS:** The project utilizes PostgreSQL specific extensions. For hierarchical data (like Categories), you MUST map the path property using the `"ltree"` column type. For custom ID generation, use Sequences (`HasSequence`).
4. **TRANSIENT FAULT RESILIENCE:** Cloud environments suffer from transient network drops. You MUST configure the DbContext with an explicit Execution Strategy. Use `.EnableRetryOnFailure()` inside the Npgsql options action when registering the DbContext.
5. **MANY-TO-MANY WITH AUDITING:** When configuring many-to-many relationships (e.g., Product to Attribute), do NOT rely on EF Core's implicit join tables. You MUST explicitly map the join entity using `.UsingEntity<T>()` and ensure the join entity inherits from `AuditableEntity` to track creation/update timestamps.
6. **INTERCEPTOR SAFETY:** Rely on `SaveChangesInterceptor` to populate Audit fields. Interceptors MUST be registered with a DI lifetime consistent with their dependencies, and MUST be wired into the DbContext via `options.AddInterceptors(sp.GetServices<IInterceptor>())` inside `AddDbContext`, so EF Core resolves them through the active scope:
   - When the interceptor depends on Scoped services (e.g., `ICurrentUserService`, `IHttpContextAccessor`), register it as **Scoped** and inject the dependencies via the constructor — EF Core will materialize a fresh interceptor per scope. This is the modern, recommended pattern.
   - Register the interceptor as **Singleton** ONLY when it has no Scoped dependencies. NEVER capture Scoped dependencies in a Singleton's constructor: if a Singleton interceptor truly needs scoped data, resolve it on-demand inside the interception callback via `eventData.Context!.GetInfrastructure().GetRequiredService<T>()`.

## Execution
When modifying the DbContext or adding new entities:
1. Ensure the entity inherits `AuditableEntity` (if applicable).
2. Create the `ValueConverter` for the Strongly-Typed ID.
3. Write the Fluent API configuration in `OnModelCreating`.
4. Hide private backing fields using `.Metadata.SetValueComparer` if necessary.