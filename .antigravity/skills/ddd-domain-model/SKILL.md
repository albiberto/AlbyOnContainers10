---
name: ddd-domain-model
description: Architectural guidelines for Domain layer files. MANDATORY for Entities, Value Objects, Aggregate Roots. Focuses on pragmatic DDD for a solo-developer e-commerce.
---

## Objective
Design a pure, rich Domain Model that protects invariants and encapsulates state, but remains highly pragmatic and simple. Avoid "astronaut architecture" and write clear, minimal code.

## MANDATORY Architectural Constraints

1. **PRAGMATIC ENCAPSULATION:** Entities must not have public setters (`private set` or `init`). Expose collections as `IReadOnlyCollection<T>`. Keep methods simple, intention-revealing, and strictly focused on business value.
2. **STRONGLY TYPED IDs:** Use C# `record` for strongly-typed IDs (e.g., `ProductId`) to guarantee compile-time safety without bloated code.
3. **ZERO INFRASTRUCTURE DEPENDENCIES:** The Domain layer must be 100% Persistence Ignorant. No EF Core attributes (`[Table]`, `[Key]`).
4. **AVOID OVER-ENGINEERING:** Do not create complex domain services or abstractions if a simple method on the Aggregate Root suffices. Less code is better code.