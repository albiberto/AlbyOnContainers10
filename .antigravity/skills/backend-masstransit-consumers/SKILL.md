---
name: backend-masstransit-consumers
description: Strict architectural guidelines for the Application layer. MANDATORY when creating CQRS Commands, Queries, and MassTransit Consumers.
---

## Objective
Implement the CQRS pattern using MassTransit as an in-process Mediator with the utmost simplicity. Ensure peak database performance while keeping the logic lean.

## MANDATORY Architectural Constraints

1. **CHAIN OF BUILD FOR MESSAGING:** Registration must follow the builder pattern (e.g., `AddMessaging().WithRabbitMq()`), using dual-overloads (`IConfiguration` / `Action<T>`).
2. **FIRE-AND-FORGET COMMAND CONSUMERS:** Handlers for commands must execute the domain logic and save. No `RespondAsync`.
3. **TRANSACTIONAL OUTBOX:** Rely strictly on the EF Core Outbox.
4. **DIRECT SQL PROJECTIONS:** In Queries, use EF Core's `.Select()` projection to map database columns directly into simple C# `record` DTOs. Do not over-complicate mappings.