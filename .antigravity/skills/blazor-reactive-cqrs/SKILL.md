---
name: blazor-reactive-cqrs
description: Strict rules for writing C# code-behind logic (@code) in Blazor components. MANDATORY when implementing page behaviors, sending CQRS commands, handling modal dialog lifecycles, and subscribing to Rx.NET events (Notifiers).
---

## Objective
Act as a Principal .NET Frontend Architect. Your task is to write Blazor component logic using a strictly Reactive, Asynchronous, and Fire-and-Forget architecture powered by MassTransit Mediator (In-Process) and Rx.NET.

## MANDATORY Architectural Constraints

1. **FIRE-AND-FORGET CQRS COMMANDS:** NEVER wait for a business result or an updated entity from a Command. You MUST use `await Mediator.Send(new MyCommand(...))`. 
   The use of MassTransit's Request/Response pattern for commands (e.g., `CreateRequestClient<T>` and `await client.GetResponse<T>(...)`) is STRICTLY FORBIDDEN. We rely entirely on reactive backpropagation for UI updates.

2. **GLOBAL EXCEPTION HANDLING & PATTERN MATCHING:** When dispatching commands inside a `try` block, the `catch` block must gracefully unwrap MassTransit's exceptions to find the root cause using `.GetBaseException()`. You MUST use modern C# logical pattern matching (`switch`) to differentiate between domain/validation warnings and critical system errors:
   ```csharp
   catch (Exception ex) 
   {
       var actualException = ex.GetBaseException();
       switch (actualException) 
       {
           case ValidationException or DomainException:
               // Clean up the message and show as a Warning (Expected business rule violation)
               var cleanMessage = actualException.Message.Replace("Validation failed: ", "").Trim();
               ToastService.ShowWarning(cleanMessage);
               break;
           default:
               // Unhandled technical failure
               ToastService.ShowError(Loc["Toast_Error"].Value);
               break;
       }
   }