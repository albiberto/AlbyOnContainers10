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
6. **INTERCEPTOR SAFETY:** Rely on `SaveChangesInterceptor` to populate Audit fields. Interceptors MUST be registered as Singletons. If an interceptor needs Scoped services (like `IHttpContextAccessor` to get the Current User), it MUST resolve them safely during the interception event without capturing the scoped dependencies in the Singleton's constructor.

## Execution
When modifying the DbContext or adding new entities:
1. Ensure the entity inherits `AuditableEntity` (if applicable).
2. Create the `ValueConverter` for the Strongly-Typed ID.
3. Write the Fluent API configuration in `OnModelCreating`.
4. Hide private backing fields using `.Metadata.SetValueComparer` if necessary.