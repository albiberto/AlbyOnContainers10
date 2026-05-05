---
name: qa-nunit-testing
description: Strict guidelines for writing Unit Tests using NUnit framework. MANDATORY when creating, refactoring, or reviewing unit tests for Domain models, Application layers, or Shared Kernel components.
---

## Objective
Ensure absolute reliability and maximum coverage of the system's behavior through pristine, deterministic, and highly readable Unit Tests using NUnit. We prioritize full failure visibility over early exit by mandating the use of `Assert.Multiple`.

## MANDATORY Architectural Constraints

1. **NUNIT EXCLUSIVELY:** All unit tests MUST use the NUnit framework (`[TestFixture]`, `[Test]`, `[SetUp]`). The use of xUnit or MSTest attributes is STRICTLY FORBIDDEN.
2. **THE ASSERT.MULTIPLE MANDATE:** Whenever a test contains more than one assertion, they MUST be wrapped inside an `Assert.Multiple(() => { ... });` block. This guarantees that all assertions are evaluated, providing a complete failure report in the CI/CD pipeline rather than halting at the first failure.
3. **STRICT AAA PATTERN:** Every test MUST be visually separated into `Arrange`, `Act`, and `Assert` phases using comments. Do not mix setup logic with execution or assertion logic.
4. **CLEAR NAMING CONVENTION:** Test methods MUST clearly describe the scenario and expected outcome. Use the standard convention: `MethodName_StateUnderTest_ExpectedBehavior` (e.g., `Rename_WhenNameIsWhitespace_ThrowsDomainException`).
5. **DOMAIN ENCAPSULATION TESTING:** When testing Domain Entities, you must respect their strict encapsulation. Use public factory methods or static `New` properties for instantiation. Ensure that invariant violations correctly throw `DomainException`.
6. **MODERN C# FEATURES:** Test classes must utilize modern C# 12/13 features, including file-scoped namespaces, target-typed `new()`, and collection expressions (`[]`) for mocks or test data.

## Execution
When asked to write or refactor unit tests:
1. Identify the component to test (Domain Entity, CQRS Consumer, Kernel Builder, etc.).
2. Setup the test class with the `[TestFixture]` attribute and file-scoped namespace.
3. Structure the test method using the AAA pattern.
4. If testing multiple properties or states, wrap all assertions in `Assert.Multiple`.

### Example
```csharp
namespace AlbyOnContainers.ProductInformationManager.Domain.UnitTests;

using System;
using Kernel.Domain.Exceptions;
using NUnit.Framework;
using ProductInformationManager.Domain;
using ProductInformationManager.Domain.ValueObjects;

[TestFixture]
public sealed class CategoryTests
{
    [Test]
    public void Rename_WhenValidNameProvided_UpdatesNameAndEmitsEvent()
    {
        // Arrange
        var categoryId = CategoryId.New;
        var category = Category.Create(categoryId, "Electronics", "All electronic items", null);
        var newName = "Digital Devices";
        var newDescription = "Smartphones, laptops, and more";

        // Act
        category.Rename(newName, newDescription);

        // Assert
        Assert.Multiple(() =>
        {
            Assert.That(category.Name, Is.EqualTo(newName));
            Assert.That(category.Description, Is.EqualTo(newDescription));
            Assert.That(category.DomainEvents, Is.Not.Empty);
        });
    }

    [Test]
    public void Rename_WhenNameIsWhitespace_ThrowsDomainException()
    {
        // Arrange
        var category = Category.Create(CategoryId.New, "Electronics", null, null);
        var invalidName = "   ";

        // Act & Assert
        var exception = Assert.Throws<DomainException>(() => category.Rename(invalidName, "Description"));
        
        Assert.Multiple(() =>
        {
            Assert.That(exception, Is.Not.Null);
            Assert.That(exception!.Message, Does.Contain("Name is required"));
        });
    }
}