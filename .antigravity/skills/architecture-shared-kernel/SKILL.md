---
name: architecture-shared-kernel
description: Strict architectural guidelines for designing, organizing, and consuming the Shared Kernel and Plugins. MANDATORY when creating or refactoring common infrastructure, cross-cutting concerns, or bootstrapping new microservices.
---

## Objective
Act as a Principal Platform Architect. Your primary goal is to maximize the Developer Experience (DX) for future engineers by maintaining an immaculate, "Plug & Play" Internal SDK (Shared Kernel). You must enforce Enterprise standards (Observability, Security, Resilience, Persistence) automatically, hiding infrastructure complexity behind a Fluent Builder Facade.

## MANDATORY Architectural Constraints

1. **TECHNICAL DOMAIN SEGREGATION:** - The Shared Kernel MUST be organized by Technical Domain (e.g., `Alby.Kernel.Security`, `Alby.Kernel.Messaging`, `Alby.Kernel.Persistence`), NOT by Architectural Layer (e.g., `Shared.Application`).
   - The Kernel is a collection of capability-enabling libraries. A microservice should only reference the specific technical modules it needs.

2. **ZERO DOMAIN LEAKAGE (PURE ABSTRACTIONS):** - The Kernel MUST NEVER contain business logic, domain-specific events, or references to bounded contexts.
   - The `Kernel.Domain` module MUST remain 100% Persistence Ignorant. It cannot contain Entity Framework Core references, attributes, or ORM logic. ORM specific interceptors and mappings belong in `Kernel.Persistence`.

3. **FAIL-FAST CONFIGURATION:**
   - Kernel modules MUST explicitly throw an `InvalidOperationException` if a required configuration value (from `appsettings.json` or Environment Variables) is missing during startup. Do not guess or provide dummy values in production-ready code.

4. **THE FLUENT KERNEL BUILDER FACADE:**
   - Future microservices MUST NOT be polluted with low-level `builder.Services.Add...` calls for cross-cutting concerns.
   - The Kernel MUST expose a fluent builder pattern starting with `builder.AddAlbyKernel()`.
   - Each technical module must provide an extension method on the builder (e.g., `.WithMessaging()`, `.WithSecurity()`, `.WithEfCorePersistence<TContext>()`).

5. **OPAQUE INFRASTRUCTURE & AUTO-DISCOVERY (FORCED BEST PRACTICES):**
   - When a developer invokes a Kernel module, the Kernel MUST automatically configure all Enterprise standards under the hood, removing choices that shouldn't belong to the application team.
   - **Caching:** The Kernel MUST use `Scrutor` via `Assembly.GetCallingAssembly()` to auto-register classes inheriting from `CacheBase<T>`. It MUST force Redis as the Backplane.
   - **Persistence:** The Kernel MUST automatically inject cross-cutting interceptors (e.g., `AuditableEntityInterceptor`, `DbCommandTelemetryInterceptor`) into the DbContext.
   - **Extensibility:** Modules must respect the Open-Closed Principle, providing fluent methods for application-specific extensions (e.g., `.AddMessagingFilter<T>()`) without breaking the core platform pipeline.

6. **PLUGIN ARCHITECTURE:**
   - Standalone features that are not strictly core to every microservice (e.g., Distributed Locks using Redis, specialized Document Storage, third-party ERP adapters) MUST be implemented as Plugins (`AlbyOnContainers.Plugins.*`).
   - Plugins MUST expose an extension method targeting `IKernelBuilder` (e.g., `.WithDistributedLocks()`) to seamlessly integrate into the fluent facade without polluting the core Kernel dependencies.

## Execution
When bootstrapping a new microservice or refactoring `Program.cs`:
1. Strip away all low-level infrastructure registrations (MassTransit setup, Keycloak setup, FusionCache setup, OpenTelemetry, EF Core defaults).
2. Replace them with the Fluent Kernel Builder pattern:
   ```csharp
   builder.AddAlbyKernel()
          .WithObservability()
          .WithSecurity()
          .WithEfCorePersistence<MyDbContext>((sp, opt) => opt.UseNpgsql(connString))
          .WithMessaging(cfg => { /* routing only */ })
          .WithEfCoreOutbox<MyDbContext>()
          .WithCaching()
          .WithDistributedLocks(); // Plugin injection