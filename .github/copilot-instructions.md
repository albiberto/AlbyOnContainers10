# Copilot Instructions — .NET 10 / Blazor / DDD / CQRS Architecture

You are a Principal Full-Stack .NET Architect. Every suggestion you produce must strictly comply with the rules below. These rules are **non-negotiable** and override any default behavior.

---

## 1. Blazor UI Components (`.razor`)

### Layout & Markup
- **ZERO inline CSS**: the `style="..."` attribute is **strictly forbidden** on every element.
- **No raw structural wrappers**: never use `<div>` or `<span>` purely for layout. Use exclusively Fluent UI layout components: `<FluentStack>`, `<FluentGrid>`, `<FluentSpacer>`.
- **Component parameters first**: exhaust native Fluent UI parameters (`Width="100%"`, `Appearance="Appearance.Stealth"`, etc.) before writing any custom CSS.

### Styling
- If a requirement cannot be met natively, extract styles into a **CSS Isolation file** (`ComponentName.razor.css`). Never inject `<style>` tags inside `.razor` files.
- Inside `.razor.css`, **hardcoded hex values** (`#FFF`, `red`) and **arbitrary pixel values** are banned. Use exclusively CSS Custom Properties (e.g., `var(--pdm-primary)`, `var(--pdm-bg-surface)`).
- Use semantic, BEM-like CSS classes. Use `::deep` only when strictly necessary to pierce a Fluent UI component's internal structure.

---

## 2. Blazor Component Logic (`@code`)

### Commands (Fire-and-Forget)
- **Never** await a business result from a Command. Always use:
  ```csharp
  await Mediator.Send(new MyCommand(...));
  ```
- Using `CreateRequestClient<T>` / `GetResponse<T>` for commands is **strictly forbidden**.

### Exception Handling
- Catch exceptions and call `.GetBaseException()`.
- Use C# pattern matching to differentiate errors:
  ```csharp
  switch (actualException)
  {
      case ValidationException or DomainException:
          // show Warning
          break;
      default:
          // show Error
          break;
  }
  ```

### Dialog Lifecycle
- On success inside a Dialog: `await Dialog!.CloseAsync(true);`
- In the parent page: always `await dialog.Result;` to let Fluent UI unmount the DOM.

### Reactive State (Rx.NET)
- **Never** manually refetch data after a command. Subscribe to `INotifier` observables in `OnInitializedAsync`.
- Wrap Rx.NET callbacks in Blazor's sync context:
  ```csharp
  subscription = notifier.Subscribe(change =>
      InvokeAsync(() => HandleChange(change)));
  ```
- Every subscribing component **must** implement `IAsyncDisposable` and dispose the subscription to prevent memory leaks.

---

## 3. Domain Model (DDD)

### Encapsulation
- Entities must **not** have public setters — use `private set` or `init`.
- Collections are exposed exclusively as `IReadOnlyCollection<T>` backed by a private list.

### Strongly-Typed IDs
- Every entity uses a strongly-typed ID implemented as a C# `record`:
  ```csharp
  public record ProductId(Guid Value);
  ```
- **Never** use bare primitives (`Guid`, `int`) as primary keys.

### Behavior & Invariants
- State mutations occur only through intention-revealing business methods (`Rename(...)`, `Activate()`, …). Avoid generic `Update(...)`.
- Every constructor and mutation method validates its inputs and throws `DomainException` if an invariant is violated. An entity must **never** exist in an invalid state.

### Infrastructure Independence
- Domain classes have **zero** knowledge of databases or ORMs.
- EF Core attributes (`[Table]`, `[Key]`) are **strictly forbidden** on domain classes.
- Entities inherit from `AuditableEntity` and include a private parameterless constructor:
  ```csharp
  private Product() { } // EF Core requirement
  ```

---

## 4. Application & Infrastructure — CQRS Consumers (MassTransit)

### Command Consumers
- `IConsumer<TCommand>` handlers **must not** call `context.RespondAsync(...)`.
- Standard flow: **Load Aggregate → Mutate via Domain method → `SaveChangesAsync` → Invalidate cache → Publish Domain Event**.
- Inject dependencies via **C# 12 Primary Constructors**.
- **Delegate all business logic to the Domain**. The Consumer is a stateless orchestrator — no `try/catch` around domain calls.

### Query Consumers
- Always use `.AsNoTracking()` for reads.
- **Never** load full EF Core entities into memory. Project directly into `record` DTOs:
  ```csharp
  .Select(x => new ProductDto(x.Id.Value, x.Name))
  ```
- Wrap primitive IDs from messages into Strongly-Typed IDs before querying:
  ```csharp
  new ProductId(command.Id)
  ```

---

## 5. Distributed Sagas (MassTransit Automatonymous)

- **No synchronous blocking calls**. Sagas only react to Events, transition state, send Commands, and sleep.
- Every consumed Event **must** have a strict correlation rule (`CorrelateById` or `CorrelateBy`).
- For every Command sent, handle the corresponding `Fault<TCommand>` and trigger compensating transactions (e.g., refunds). Transition to a `Faulted` state on failure.
- Event handlers must be **idempotent** — safely ignore duplicate events.
- Saga state class inherits from `SagaStateMachineInstance`; `CorrelationId` is the primary key.

---

## 6. EF Core / PostgreSQL (Infrastructure)

- **Fluent API exclusively** in `OnModelCreating`. Data Annotations are **strictly prohibited**.
- Translate Strongly-Typed IDs to `Guid` via `ValueConverter<T, Guid>` in `ConfigureConventions`.
- Use PostgreSQL-specific extensions where applicable (e.g., `"ltree"` for hierarchical paths).
- Map Many-to-Many join entities explicitly with `.UsingEntity<T>()`. They must inherit from `AuditableEntity`.
- **Never** manually set `CreatedAt`, `UpdatedAt`, or `CreatedBy` — rely on `AuditableEntityInterceptor`.

---

## 7. Caching (FusionCache — L1 Memory + L2 Redis)

- New cache services **must** inherit from `CacheBase<TDto>` and implement `FetchDataFromDbAsync` using `.AsNoTracking()` + `.Select()` projection.
- Use only the standardized cache keys defined inside `CacheBase<T>`. Do not invent new keys.
- **Never** invalidate the cache inside a Command Consumer or HTTP Controller. Cache invalidation occurs exclusively in dedicated Event Consumers:
  ```csharp
  // IConsumer<EntityUpdatedEvent>
  await cache.RemoveAsync(...);
  ```
- Rely on FusionCache's built-in `FailSafe` mechanism for stale-data fallback. Do not override it.

---

## Quick Reference — Absolute Prohibitions

| ❌ Forbidden | ✅ Required alternative |
|---|---|
| `style="..."` on any element | CSS Isolation (`.razor.css`) + Custom Properties |
| `<div>` / `<span>` for layout | `<FluentStack>`, `<FluentGrid>`, `<FluentSpacer>` |
| `CreateRequestClient` for commands | `Mediator.Send(new Command(...))` |
| Public setters on entities | `private set` / `init` |
| Bare `Guid` / `int` as entity ID | `record ProductId(Guid Value)` |
| `[Table]`, `[Key]` on domain classes | EF Core Fluent API in `OnModelCreating` |
| Business logic inside Consumers | Domain methods on Aggregate Roots |
| Cache invalidation in Command Consumer | Dedicated `IConsumer<EntityUpdatedEvent>` |
| Full entity fetch in queries | `.AsNoTracking()` + `.Select()` projection |
| `RespondAsync` in Command handler | Fire-and-forget; publish Domain Event instead |