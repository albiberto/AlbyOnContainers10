---
name: app-fusioncache
description: Rules for data caching using ZiggyCreatures.FusionCache. MANDATORY when fetching read-heavy data. Enforces native library usage without restrictive wrappers.
---

## Objective
Maximize read performance using a Multi-Level Distributed Cache (Memory L1 + Redis L2 Backplane) via FusionCache, keeping the implementation simple, native, and highly maintainable.

## MANDATORY Architectural Constraints

1. **NO WRAPPERS (USE NATIVE ABSTRACTIONS):** It is STRICTLY FORBIDDEN to create generic wrappers (like `CacheBase<T>`) around FusionCache. Wrappers limit the native capabilities of the library and add unnecessary code. Inject and use `IFusionCache` directly.
2. **CHAIN OF BUILD REGISTRATION:** - The registration method MUST be `AddCaching()` returning an `ICachingBuilder` (NOT `IKernelBuilder`).
   - Extensions like `WithRedisBackplane()` MUST extend `ICachingBuilder` to strictly enforce the Open-Closed Principle. Redis connection multiplexers must be supplied externally (e.g., from Aspire) and not tightly coupled to the caching core.
3. **DUAL-METHOD OVERLOADS:** `AddCaching` and `WithRedisBackplane` must offer both `IConfiguration` and `Action<TOptions>` overloads for maximum flexibility.
4. **BINARY SERIALIZATION ONLY:** Enforce MessagePack (binary serialization) to reduce memory payload.
5. **EVENT-DRIVEN INVALIDATION:** Invalidate cache (`cache.RemoveAsync`) exclusively inside dedicated Event Consumers.