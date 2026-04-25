---
name: backend-masstransit-sagas
description: Strict rules for designing and implementing Distributed Transactions using MassTransit State Machines (Automatonymous). MANDATORY when creating or modifying Sagas, State profiles, Correlation rules, and Compensation logic.
---

## Objective
Act as a Principal Distributed Systems Architect. Your task is to implement robust, resilient, and event-driven Sagas using MassTransit's Automatonymous State Machine to manage long-running distributed transactions.

## MANDATORY Architectural Constraints

1. **NO SYNCHRONOUS BLOCKING CALLS:** Sagas MUST NEVER wait for HTTP responses or synchronous results inside the state machine definition. The Saga must purely react to incoming Events, transition state, send Commands to other services, and go back to sleep.
2. **CORRELATION IS KING:** Every Event consumed by the Saga MUST have a strict correlation rule (`CorrelateById` or `CorrelateBy`). You must explicitly define how incoming messages map to the Saga's CorrelationId.
3. **COMPENSATION & FAULT TOLERANCE:** Whenever the Saga sends a Command to a downstream service, it MUST handle the corresponding `Fault<TCommand>` or a specific Domain Failure Event. Upon failure, the Saga must trigger compensating transactions (e.g., refunding payment if inventory reservation fails) and transition to a `Faulted` or `Canceled` state.
4. **IDEMPOTENCY:** Saga event handlers must be idempotent. If the Saga receives an event for a state transition that has already occurred, it should safely ignore it rather than throwing an exception or duplicating commands.
5. **OPTIMISTIC CONCURRENCY (STRICT REQUIRED):** High-throughput Sagas can process concurrent events. The Saga State class MUST define a `public int RowVersion { get; set; }` property. When configuring EF Core, this property MUST be mapped as a Concurrency Token (e.g., `.IsRowVersion()`). MassTransit must be configured with retry policies (`UseMessageRetry`) to gracefully handle `DbUpdateConcurrencyException`.
6. **STATE PERSISTENCE:** The Saga State class must inherit from `SagaStateMachineInstance`. When configuring EF Core for the Saga, ensure the `CurrentState` is persisted as a string or integer, and map the `CorrelationId` as the primary key.

## Execution
When implementing a SAGA:
1. Define the State class (`SagaStateMachineInstance`) including the `RowVersion`.
2. Map all States (`State`) and Events (`Event<T>`).
3. Define Correlation rules in the constructor.
4. Write the behavior (`Initially`, `During`) using `TransitionTo`, `Send`, and `Publish`.