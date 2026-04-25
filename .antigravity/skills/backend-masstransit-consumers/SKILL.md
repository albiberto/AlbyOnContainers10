---
name: backend-masstransit-consumers
description: Strict architectural guidelines for the Application and Infrastructure layers. MANDATORY when creating, modifying, or refactoring CQRS Commands, Queries, MassTransit Consumers, and Entity Framework Core data access logic.
---

## Objective
Act as a Principal .NET Backend Architect. Your task is to implement the CQRS pattern using MassTransit as an in-process Mediator. You must ensure peak database performance, strict separation of concerns, and rigid adherence to the Fire-and-Forget state mutation pattern.

## MANDATORY Architectural Constraints

1. **FIRE-AND-FORGET COMMAND CONSUMERS:** - Handlers for commands (`IConsumer<MyCommand>`) MUST NOT return data to the caller.
   - The use of `await context.RespondAsync(...)` is STRICTLY FORBIDDEN for commands.
   - A Command Consumer's execution flow must strictly follow this sequence: Load the aggregate -> Mutate it via Domain method -> Invalidate cache (`cache.InvalidateAsync()`) -> `await db.SaveChangesAsync()`.

2. **TRANSACTIONAL OUTBOX MANDATORY (NO DUAL-WRITES):** - You MUST NEVER publish events directly to the message broker using `await bus.Publish(...)` after saving to the database.
   - The system strictly relies on the MassTransit Entity Framework Core Outbox. Domain events collected from the Aggregate Root must be seamlessly inserted into the Outbox table within the SAME SQL transaction as the state mutation during `SaveChangesAsync()`.

3. **PRIMARY CONSTRUCTOR INJECTION:** - Consumers MUST inject their dependencies exclusively using C# 12 Primary Constructors.
   - Standard dependencies include the EF Core `DbContext` (e.g., `ProductContext`) and local Cache wrappers (e.g., `CategoryCache`).
   - *Example:* `public class UpdateCategoryConsumer(ProductContext db, CategoryCache cache) : IConsumer<UpdateCategory>`

4. **DELEGATE BUSINESS LOGIC TO DOMAIN (NO TRANSACTION SCRIPTS):** - NEVER write business rules, condition checks, or state mutation logic directly inside the Consumer code.
   - The Consumer acts solely as a stateless orchestrator: It retrieves the Aggregate Root via EF Core, invokes the rich domain method on the entity (e.g., `entity.Rename(...)`), and calls save.
   - Do NOT wrap domain calls in `try/catch` blocks to handle business errors; let `DomainException` bubble up to the `GlobalExceptionFilter` pipeline.

5. **HIGH-PERFORMANCE QUERY CONSUMERS:** - Consumers handling Queries (`IConsumer<GetMyEntity>`) MUST ALWAYS use `.AsNoTracking()` when reading from Entity Framework Core to completely bypass the change tracker and optimize read performance.

6. **DIRECT SQL PROJECTIONS (NO IN-MEMORY MAPPING):** - In Queries, NEVER fetch full EF Core entities into memory (e.g., via `ToListAsync()`) only to map them to DTOs in a loop afterward.
   - You MUST use EF Core's `.Select()` projection to map database columns directly into the target C# `record` DTOs within the SQL translation.

7. **STRONGLY-TYPED ID MARSHALING:** - When a Command or Query message carries primitive identity types (e.g., a standard `Guid`), you MUST immediately wrap them into their respective Strongly-Typed IDs (e.g., `new ProductId(command.Id)`) before interacting with the Domain or querying Entity Framework Core.

## Execution
When asked to write or refactor a MassTransit Consumer:
1. Identify if it is a Command or a Query.
2. If Command: Ensure no `RespondAsync`, execute domain logic, let the Outbox handle events, and save.
3. If Query: Ensure `AsNoTracking` and direct `.Select()` projection to DTOs.
4. Ensure C# 12 Primary Constructors and Strongly-Typed IDs are utilized.