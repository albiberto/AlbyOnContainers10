---
name: kernel-observability
description: Architectural guidelines for the Observability module (OpenTelemetry, Logging, Metrics, Tracing). MANDATORY when configuring telemetry or adding meters/tracers.
---

## Objective
Provide an Enterprise-grade, centralized Observability stack using OpenTelemetry, while keeping the configuration strictly isolated from other kernel modules. Keep it simple, native, and maintainable for a solo-developer platform.

## MANDATORY Architectural Constraints

1. **CENTRALIZED TELEMETRY (NO POLLUTION):** Other modules (like Caching, Messaging, Persistence) MUST NOT configure OpenTelemetry themselves. All `AddOpenTelemetry().WithMetrics(...)` or `.WithTracing(...)` registrations must reside Exclusively within the Observability module.
    - *Example:* The meter `"ZiggyCreatures.Caching.Fusion"` must be registered in the Observability extensions, not in the Caching extensions.

2. **THE DUAL-METHOD OPTIONS PATTERN (MANDATORY):**
   Every registration extension method MUST provide exactly two overloads to guarantee maximum flexibility:
    - **Overload 1:** Accepts `IConfiguration` (reads directly from the bound section, e.g., `appsettings.json`).
    - **Overload 2:** Accepts an `Action<TOptions>` (allows inline lambda configuration).

3. **CHAIN OF BUILD PATTERN:**
    - The entry point must be `AddObservability(...)` which returns an `IObservabilityBuilder`.
    - Subsequent configurations must be chained as extension methods on `IObservabilityBuilder` (e.g., `.WithFusionCacheMetrics()`, `.WithEfCoreTracing()`).
    - Do NOT pollute the root `IServiceCollection` or `IKernelBuilder` with flat registrations.

4. **NATIVE OPENTELEMETRY ONLY:**
   Do not create custom wrappers around the standard OpenTelemetry SDK or `ILogger<T>`. Use native Microsoft and OpenTelemetry abstractions directly.