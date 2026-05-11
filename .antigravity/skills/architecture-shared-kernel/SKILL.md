---
name: architecture-shared-kernel
description: Strict architectural guidelines for the Shared Kernel. MANDATORY when creating or refactoring common infrastructure. Focuses on Enterprise-grade simplicity for a solo-developer e-commerce.
---

## Objective
Act as a Principal Platform Architect for a solo-developer e-commerce platform. Your primary goal is to achieve an Enterprise level of robustness while maintaining extreme simplicity. Code must be easy to read, easy to manage, and free of over-engineering. Maximize Developer Experience (DX) with a "Plug & Play" Internal SDK.

## MANDATORY Architectural Constraints

1. **ENTERPRISE SIMPLICITY:** Do not over-engineer. Avoid useless abstractions and wrappers over third-party libraries. Less code means faster understanding and easier maintenance.
2. **THE DUAL-METHOD OPTIONS PATTERN:**
   - Every module requiring configuration MUST define a strongly-typed `Options` class.
   - Extension methods MUST provide TWO overloads:
     a) `IConfiguration` binding.
     b) `Action<TOptions>` lambda.
3. **CHAIN OF BUILD & OPEN-CLOSED PRINCIPLE:**
   - Do not pollute `IKernelBuilder` with monolithic registrations.
   - Use dedicated builders to create a chain. For example, `AddCaching()` must return an `ICachingBuilder` interface, not `IKernelBuilder`.
   - Respect the Open-Closed Principle. External dependencies (like Redis provided by Aspire) must not be hardcoded into the core module but provided via extensions (e.g., `.WithRedisBackplane()`).
4. **TECHNICAL DOMAIN SEGREGATION:** Organize the Shared Kernel by Technical Domain (`Alby.Kernel.Security`, `Alby.Kernel.Messaging`), not by layer.