# Zooper.Bee Workflow Source Generators

This project contains Roslyn source generators that enhance the Zooper.Bee workflow system by providing type-safe property dependency tracking and validation.

## Features

The source generators provide the following capabilities:

1. **Property Dependency Tracking**: Automatically validates that all required property dependencies are satisfied before a property is set
2. **Type-Safe Property References**: Enables referencing properties via lambda expressions instead of string literals
3. **Workflow Builder Extensions**: Provides fluent extension methods for the workflow builder that integrate with the property dependency system

## Getting Started

### 1. Add Required Attributes

First, add the `WorkflowPayloadAttribute` to your payload class:

```csharp
using Zooper.Bee.Generators;

[WorkflowPayload]
public partial record OrderPayload
{
    // Class must be partial
}
```

### 2. Mark Properties with Dependencies

Use the `WorkflowPropertyAttribute` to define dependencies between properties:

```csharp
// No dependencies
[WorkflowProperty(Description = "Calculated subtotal")]
public decimal Subtotal { get; init; }

// Depends on Subtotal
[WorkflowProperty(DependsOn = new[] { nameof(Subtotal) },
    Description = "Tax based on subtotal")]
public decimal TaxAmount { get; init; }

// Depends on multiple properties
[WorkflowProperty(DependsOn = new[] { nameof(Subtotal), nameof(TaxAmount) },
    Description = "Final amount")]
public decimal TotalAmount { get; init; }
```

### 3. Create Your Workflow Using Type-Safe References

```csharp
var workflow = new WorkflowBuilder<OrderRequest, OrderPayload, OrderResult, OrderError>(
    // Factory and selector functions
    CreateInitialPayload,
    CreateResult)

    // Use lambda expressions to reference properties
    .Do(p => p.Subtotal, CalculateSubtotal)

    // Dependencies are automatically checked
    .Do(p => p.TaxAmount, CalculateTax)

    // Conditional activities with property references
    .DoIf(
        p => !string.IsNullOrEmpty(p.DiscountCode),
        p => p.DiscountAmount,
        ApplyDiscount)

    .Do(p => p.TotalAmount, CalculateTotal)

    .Build();
```

## Generated Code

The source generators produce:

1. **Dependency Checking Methods**: For each property with dependencies, methods to validate those dependencies are met
2. **Has Property Methods**: Methods that check if properties have been set
3. **Type-Safe Property Tokens**: For referencing properties without string literals
4. **Extension Methods**: For the `WorkflowBuilder` class to enable type-safe property referencing

## How It Works

The source generator examines your payload classes marked with `[WorkflowPayload]` and generates:

1. **Partial Class Extensions**: Adding dependency validation logic
2. **Property Tokens**: Static members for type-safe referencing
3. **Builder Extensions**: Methods for the workflow builder that work with the dependency system

When you use these extensions in your workflow definition, the system will:

1. Automatically check dependencies before executing an activity
2. Provide clear error messages when dependencies are not met
3. Allow you to reorder activities freely, as long as dependencies are satisfied

## Example

See the `Zooper.Bee.Examples` project for complete examples of how to use the source generators with realistic workflows.

## Benefits

- **Type Safety**: Property references are checked at compile-time
- **Order Independence**: Activities can be reordered as long as dependencies are met
- **Self-Documenting**: Dependencies are explicitly declared
- **Refactoring Support**: Property renames are caught by the compiler
