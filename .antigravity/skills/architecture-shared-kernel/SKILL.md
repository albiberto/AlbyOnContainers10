---
name: architecture-shared-kernel
description: Strict architectural guidelines for designing, organizing, and consuming the Shared Kernel and Plugins. MANDATORY when creating or refactoring common infrastructure, cross-cutting concerns, or bootstrapping new microservices.
---

## Objective
Act as a Principal Platform Architect. Your primary goal is to maximize the Developer Experience (DX) for future engineers by maintaining an immaculate, "Plug & Play" Internal SDK (Shared Kernel). You must enforce Enterprise standards (Observability, Security, Resilience, Persistence) automatically, hiding infrastructure complexity behind a Fluent Builder Facade.

## MANDATORY Architectural Constraints

1. **TECHNICAL DOMAIN & DEPENDENCY SEGREGATION:** - The Shared Kernel MUST be organized by Technical Domain (e.g., `Alby.Kernel.Security`, `Alby.Kernel.Messaging`), NOT by Architectural Layer.
   - The platform follows an "A la carte" SDK pattern. Modules are independent. A microservice MUST only reference the specific technical `.csproj` modules it strictly needs (Zero Baggage).
   - The `Kernel.Domain` module MUST remain 100% Persistence Ignorant. It cannot contain Entity Framework Core references, attributes, or ORM logic. ORM specific interceptors and mappings belong in `Kernel.Persistence`.

2. **THE DUAL-METHOD OPTIONS PATTERN & FAIL-FAST (CRITICAL):**
   - Modules MUST NEVER read configuration values directly using `builder.Configuration.GetValue` or `GetConnectionString` inside the extension methods.
   - Every module requiring configuration MUST define a strongly-typed `Options` class (e.g., `CachingOptions`, `LocalizationOptions`) with `[Required]` DataAnnotations.
   - The extension method MUST provide TWO overloads for the builder:
     a) **IConfiguration Binding:** Uses `.BindConfiguration(SectionName)`.
     b) **Lambda Configuration:** Uses `.Configure(Action<TOptions>)`.
   - BOTH methods MUST strictly chain `.ValidateDataAnnotations().ValidateOnStart()` to guarantee Fail-Fast at container boot time.
   - Third-party options (e.g., FusionCacheOptions) MUST be mapped dynamically via DI using `.AddOptions<ThirdPartyOptions>().Configure<IOptions<MyOptions>>((target, source) => ...)`.

3. **THE FLUENT KERNEL BUILDER FACADE:**
   - Future microservices MUST NOT be polluted with low-level `builder.Services.Add...` calls for cross-cutting concerns.
   - The Kernel MUST expose a fluent builder pattern starting with `builder.AddAlbyKernel()`.
   - Each technical module must provide an extension method on the builder (e.g., `.WithMessaging()`, `.WithSecurity()`, `.WithEfCorePersistence<TContext>()`).

4. **OPAQUE INFRASTRUCTURE & AUTO-DISCOVERY (FORCED BEST PRACTICES):**
   - The Kernel MUST automatically configure all Enterprise standards under the hood.
   - **Auto-Discovery (Generics):** Use Marker Types (`<TMarker>`) instead of passing `Assembly[]` manually to scan for components (e.g., `.WithCaching<ApplicationMarker>()`). 
   - **Caching:** The Kernel MUST auto-register classes inheriting from `CacheBase<T>`. It MUST force Redis as the Backplane and strictly use **MessagePack** (binary serialization), NEVER JSON.
   - **Persistence:** The Kernel MUST automatically inject cross-cutting interceptors (e.g., `AuditableEntityInterceptor` resolving `ICurrentUserService` dynamically) into the DbContext as Singletons.
   - **Extensibility:** Modules must respect the Open-Closed Principle, providing fluent methods for application-specific extensions (e.g., `.AddMessagingFilter<T>()`) without breaking the core platform pipeline.

5. **PLUGIN ARCHITECTURE:**
   - Standalone features that are not strictly core to every microservice (e.g., Distributed Locks, Blob Storage) MUST be implemented as Plugins (`src/Plugins/AlbyOnContainers.Plugins.*`).
   - Plugins MUST expose an extension method targeting `IKernelBuilder` (e.g., `.WithDistributedLocks()`) to seamlessly integrate into the fluent facade without polluting the core Kernel dependencies.

## Execution
When bootstrapping a new microservice or refactoring `Program.cs`:
1. Strip away all low-level infrastructure registrations (MassTransit setup, Keycloak setup, FusionCache setup, OpenTelemetry, EF Core defaults).
2. Replace them with the Fluent Kernel Builder pattern using the Dual-Method lambda for connection strings and Generic Markers for assemblies:

```csharp
builder.AddAlbyKernel()
       .WithObservability()
       .WithSecurity()
       .WithEfCorePersistence<MyDbContext>((sp, opt) => opt.UseNpgsql(builder.Configuration.GetConnectionString("db")))
       .WithMessaging(cfg => { /* routing only */ })
       .WithEfCoreOutbox<MyDbContext>(o => o.UsePostgres())
       .WithCaching<ApplicationServiceExtensions>(opt => opt.RedisConnectionString = builder.Configuration.GetConnectionString("cache"))
       .WithLocalization()
       .WithDistributedLocks(opt => opt.RedisConnectionString = builder.Configuration.GetConnectionString("locks"));