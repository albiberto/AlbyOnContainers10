---
name: app-fusioncache
description: Rules for data caching and invalidation using ZiggyCreatures.FusionCache. MANDATORY when fetching read-heavy data, optimizing performance, or writing cache invalidation logic inside MassTransit Consumers.
---

## Objective
Maximize read performance and minimize database load by leveraging a Multi-Level Distributed Cache (Memory L1 + Redis L2 Backplane) using FusionCache, strictly segregating cache invalidation into Event Consumers.

## MANDATORY Architectural Constraints

1. **USE THE CACHEBASE PATTERN:** When creating a new Cache service (e.g., `ProductCache`), you MUST inherit from the abstract `CacheBase<TDto>` class provided in the solution. You must implement the `FetchDataFromDbAsync` abstract method using EF Core with `.AsNoTracking()` and explicit projection (`.Select()`).
2. **BINARY SERIALIZATION ONLY (MESSAGEPACK):** When configuring FusionCache with the Redis Backplane, the use of JSON serialization is STRICTLY FORBIDDEN. You MUST enforce MessagePack (binary serialization) within the Kernel builder to ensure maximum throughput and reduced memory payload across the network.
3. **NO MANUAL CACHE KEYS:** Cache keys are standardized within `CacheBase<T>`. Do not hardcode or invent new cache keys scattered across the application unless dealing with highly specific single-item queries.
4. **EVENT-DRIVEN INVALIDATION:** NEVER invalidate the cache inside a Command Consumer or an HTTP Controller. Cache invalidation MUST occur exclusively inside dedicated Event Consumers (e.g., `IConsumer<EntityUpdatedEvent>`).
   *Example:* When `UpdateCategoryConsumer` finishes via the Outbox, a separate `CategoryEventsConsumer` listens to this event from the bus and calls `await cache.InvalidateAsync(ct);`.
5. **FAIL-SAFE & STALE DATA:** Rely on FusionCache's `FailSafe` mechanism. If the database is down, the cache should return stale data rather than failing. This is configured globally, so do not override it manually per call unless explicitly requested.

## Execution
When asked to implement caching for a new entity:
1. Create a `MyEntityCache : CacheBase<MyEntityDto>` service.
2. Implement the EF Core projection in `FetchDataFromDbAsync`.
3. Ensure the Cache service is injected into the Query Consumers.
4. Ensure Cache Invalidation occurs in Domain Event Consumers.