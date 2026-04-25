---
name: ddd-domain-model
description: Strict architectural guidelines for Domain layer files. MANDATORY when creating, modifying, or refactoring Entities, Value Objects, Aggregate Roots, Domain Exceptions, or any file within the /Domain directory.
---

## Objective
Act as a Principal Domain-Driven Design (DDD) Architect. Your task is to design and write a pure, rich Domain Model that rigorously protects its invariants, perfectly encapsulates its state, and strictly follows DDD principles in C# 12.

## MANDATORY Architectural Constraints

1. **NO ANEMIC DOMAIN MODELS (STRICT ENCAPSULATION):** - Entities MUST NOT have public setters. Use `private set` or `init` for all properties.
   - Collection navigation properties (e.g., `Children`, `Products`) MUST be exposed exclusively as `IReadOnlyCollection<T>` to the outside world.
   - These public collections MUST be backed by a private field (e.g., `private readonly List<T> _items = [];`). Never allow external code to call `.Add()` or `.Remove()` directly on a property.

2. **STRONGLY TYPED IDs:** - Every entity MUST use a strongly-typed ID implemented as a C# `record` (e.g., `public record ProductId(Guid Value) { public static ProductId New => new(Guid.NewGuid()); }`).
   - NEVER use bare primitives (`Guid`, `int`, or `string`) as primary keys within the domain layer.

3. **UBIQUITOUS LANGUAGE (EXPRESSIVE BEHAVIORS):** - State mutations MUST occur exclusively through explicit, expressive business methods that reflect the Ubiquitous Language of the domain (e.g., `Rename(string name, string? description)`, `Activate()`, `ChangeCategory(CategoryId id)`).
   - Avoid generic `Update(...)` methods that blindly accept every property unless it specifically models a single, cohesive business transaction.

4. **INVARIANT PROTECTION (ALWAYS VALID STATE):** - Every constructor and business mutation method MUST aggressively validate its input parameters before mutating state.
   - If a business rule, validation, or invariant is violated, the method MUST throw a custom `DomainException` (e.g., `if (string.IsNullOrWhiteSpace(name)) throw new DomainException("Name is required.");`).
   - An entity must NEVER exist in an invalid state at any point in its lifecycle.

5. **ZERO INFRASTRUCTURE DEPENDENCIES (PERSISTENCE IGNORANCE):** - Domain classes MUST remain completely ignorant of databases, ORMs, or serialization frameworks.
   - The use of Entity Framework Core data annotations (e.g., `[Table]`, `[Column]`, `[Key]`, `[Required]`) or JSON attributes is STRICTLY FORBIDDEN.
   - All database mapping and configuration MUST be handled externally via the Fluent API inside the `DbContext` (`OnModelCreating`).

6. **EF CORE & AUDITING COMPATIBILITY:**
   - Entities should generally inherit from the `AuditableEntity` base class to automatically track creation and update metadata.
   - Every entity MUST include a private parameterless constructor strictly to satisfy EF Core materialization requirements. Always add the comment `// EF Core requirement` next to it (e.g., `private Product() { } // EF Core requirement`).

7. **PURE DOMAIN EVENT DISPATCHING:**
   - Aggregate Roots MUST maintain a private, ephemeral collection of domain events (e.g., `private readonly List<IDomainEvent> _domainEvents = [];`).
   - Business methods mutating state MUST append events to this list instead of publishing them directly.
   - External infrastructure (like EF Core Interceptors) will read this list and push the events to the MassTransit Outbox during `SaveChangesAsync()`.

## Execution
When asked to create or modify a Domain Entity:
1. Ensure all properties are fully encapsulated.
2. Implement strongly-typed IDs.
3. Expose behavior via intention-revealing methods and append Domain Events.
4. Protect invariants with `DomainException`.
5. Keep the class 100% free of infrastructure attributes.