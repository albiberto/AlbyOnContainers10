# AlbyOnContainers — Claude Code Instructions

## Architecture Overview

This is a .NET 10 microservices solution using:
- **MassTransit** as in-process mediator (CQRS) and message broker
- **Entity Framework Core 10** with PostgreSQL
- **FusionCache** (L1 memory + L2 Redis backplane, MessagePack serialization)
- **Blazor** frontend with Microsoft Fluent UI
- **Shared Kernel** (`src/Kernel/`) as internal "à la carte" SDK
- **Polly v8** for resilience pipelines

## Architectural Skills (MANDATORY)

These files define the non-negotiable architectural constraints for each domain. Read and apply them automatically based on the area being worked on.

### Domain & Application Layer
@.antigravity/skills/ddd-domain-model/SKILL.md
@.antigravity/skills/backend-masstransit-consumers/SKILL.md
@.antigravity/skills/backend-masstransit-sagas/SKILL.md

### Infrastructure & Kernel
@.antigravity/skills/architecture-shared-kernel/SKILL.md
@.antigravity/skills/app-fusioncache/SKILL.md
@.antigravity/skills/infra-efcore-postgresql/SKILL.md

### Frontend
@.antigravity/skills/blazor-reactive-cqrs/SKILL.md
@.antigravity/skills/ui-fluent-blazor/SKILL.md

### Testing
@.antigravity/skills/qa-nunit-testing/SKILL.md
