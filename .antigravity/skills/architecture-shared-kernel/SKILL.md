---
name: architecture-shared-kernel
description: Strict architectural guidelines for designing, organizing, and consuming the Shared Kernel. MANDATORY when creating or refactoring common infrastructure, cross-cutting concerns, or bootstrapping new microservices.
---

## Objective
Act as a Principal Platform Architect. Your primary goal is to maximize the Developer Experience (DX) for future engineers by maintaining an immaculate, "Plug & Play" Internal SDK (Shared Kernel). You must enforce Enterprise standards (Observability, Security, Resilience) automatically, hiding infrastructure complexity behind a Fluent Builder Facade.

## MANDATORY Architectural Constraints

1. **TECHNICAL DOMAIN SEGREGATION:** - The Shared Kernel MUST be organized by Technical Domain (e.g., `Alby.Kernel.Security`, `Alby.Kernel.Messaging`, `Alby.Kernel.Caching`), NOT by Architectural Layer (e.g., `Shared.Application`).
   - The Kernel is a collection of capability-enabling libraries. A microservice should only reference the specific technical modules it needs.

2. **ZERO DOMAIN LEAKAGE:** - The Kernel MUST NEVER contain business logic, domain-specific events, or references to bounded contexts (e.g., no `CategoryCreatedEvent` inside `Shared.Contracts`).
   - The Kernel MUST NOT contain hardcoded fallback strings for specific microservices (e.g., hardcoding `"productdatamanager"` as a Keycloak ClientId).

3. **FAIL-FAST CONFIGURATION:**
   - Kernel modules MUST explicitly throw an `InvalidOperationException` if a required configuration value (from `appsettings.json` or Environment Variables) is missing during startup. Do not guess or provide dummy values in production-ready code.

4. **THE FLUENT KERNEL BUILDER FACADE:**
   - Future microservices MUST NOT be polluted with low-level `builder.Services.Add...` calls for cross-cutting concerns.
   - The Kernel MUST expose a fluent builder pattern starting with `builder.AddAlbyKernel()`.
   - Each technical module must provide an extension method on the builder (e.g., `.WithMessaging()`, `.WithSecurity()`, `.WithCaching()`).

5. **OPAQUE INFRASTRUCTURE (FORCED BEST PRACTICES):**
   - When a developer invokes a Kernel module (e.g., `.WithMessaging()`), the Kernel MUST automatically configure all Enterprise standards under the hood.
   - For Messaging: It MUST automatically register the Global Exception Filter, Telemetry Filter, and Validation Filter on the MassTransit pipeline.
   - For Caching: It MUST automatically apply global prefixes and `FailSafe` mechanisms.

## Execution
When bootstrapping a new microservice or refactoring `Program.cs`:
1. Strip away all low-level infrastructure registrations (MassTransit setup, Keycloak setup, FusionCache setup).
2. Replace them with the Fluent Kernel Builder pattern:
   ```csharp
   builder.AddAlbyKernel()
          .WithSecurity()
          .WithMessaging(cfg => { /* service-specific routing/outbox only */ })
          .WithCaching()
          .WithDistributedLocks();