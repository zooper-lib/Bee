# Zooper.Bee

<img src="icon.png" alt="Zooper.Bee Logo" width="120" align="right"/>

[![NuGet Version](https://img.shields.io/nuget/v/Zooper.Bee.svg)](https://www.nuget.org/packages/Zooper.Bee/)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](https://opensource.org/licenses/MIT)

Zooper.Bee is a fluent, lightweight workflow framework for C# that enables you to build type-safe, declarative business workflows with robust error handling.

## Key Features

- **Fluent Builder API**: Create workflows with an intuitive, chainable syntax
- **Type-safe**: Leverage C#'s static typing for error-resistant workflows
- **Functional Style**: Uses an Either monad pattern for clear success/failure handling
- **Composable**: Build complex workflows from simple, reusable components
- **Comprehensive**: Support for validations, conditional activities, branches, and finally blocks
- **Isolated Branches**: Create branches with their own isolated local payload types
- **Async-first**: First-class support for async/await operations
- **Testable**: Workflows built with Zooper.Bee are easy to unit test
- **No Dependencies**: Minimal external dependencies (only uses Zooper.Fox)

## Installation

```bash
dotnet add package Zooper.Bee
```

## Quick Start

Here's a basic example of creating and executing a workflow:

```csharp
// Define your models
public record OrderRequest(string CustomerId, decimal Amount);
public record OrderPayload(OrderRequest Request, Guid OrderId, bool IsProcessed);
public record OrderConfirmation(Guid OrderId, DateTime ProcessedAt);
public record OrderError(string Code, string Message);

// Create a workflow
var workflow = new WorkflowBuilder<OrderRequest, OrderPayload, OrderConfirmation, OrderError>(
    // Initial payload factory
    request => new OrderPayload(request, Guid.NewGuid(), false),
    // Result selector
    payload => new OrderConfirmation(payload.OrderId, DateTime.UtcNow))

    // Add validations
    .Validate(request =>
        string.IsNullOrEmpty(request.CustomerId)
            ? Option<OrderError>.Some(new OrderError("INVALID_CUSTOMER", "Customer ID required"))
            : Option<OrderError>.None())

    // Add activities
    .Do(ProcessPayment)
    .Do(UpdateInventory)

    // Add conditional activities
    .DoIf(
        payload => payload.Request.Amount > 1000,
        ApplyHighValueDiscount)

    // Add finally activities
    .Finally(LogOrderProcessing)

    // Build the workflow
    .Build();

// Execute the workflow
var result = await workflow.Execute(new OrderRequest("CUST123", 299.99));

// Handle the result
if (result.IsRight)
{
    var confirmation = result.Right;
    Console.WriteLine($"Order {confirmation.OrderId} processed at {confirmation.ProcessedAt}");
}
else
{
    var error = result.Left;
    Console.WriteLine($"Error: {error.Code} - {error.Message}");
}
```

## Core Concepts

### Workflow

A workflow represents a sequence of operations that processes a request and produces either a success result or an error. It's created using the `WorkflowBuilder`.

### WorkflowBuilder

The builder provides a fluent API for constructing workflows:

```csharp
var workflow = new WorkflowBuilder<TRequest, TPayload, TSuccess, TError>(
    contextFactory,  // Function that creates the initial payload from the request
    resultSelector)  // Function that creates the success result from the final payload
    .Validate(...)   // Add validations
    .Do(...)         // Add activities
    .DoIf(...)       // Add conditional activities
    .Branch(...)     // Add branching logic
    .BranchWithLocalPayload(...) // Add branch with its own isolated payload type
    .Finally(...)    // Add finally activities
    .Build();        // Build the workflow
```

### Validations

Validations check if the request is valid before processing begins. They return an `Option<TError>`:

```csharp
.Validate(request =>
    string.IsNullOrEmpty(request.CustomerId)
        ? Option<OrderError>.Some(new OrderError("INVALID_CUSTOMER", "Customer ID required"))
        : Option<OrderError>.None())
```

### Activities

Activities are the primary building blocks of workflows. They process the payload and return either a success or failure result:

```csharp
private static Either<OrderError, OrderPayload> ProcessPayment(OrderPayload payload)
{
    // Process payment logic

    if (successful)
    {
        var updatedPayload = payload with { IsProcessed = true };
        return Either<OrderError, OrderPayload>.FromRight(updatedPayload);
    }
    else
    {
        return Either<OrderError, OrderPayload>.FromLeft(
            new OrderError("PAYMENT_FAILED", "Failed to process payment"));
    }
}
```

### Conditional Activities

Execute activities only when specific conditions are met:

```csharp
.DoIf(
    payload => payload.Request.Amount > 1000,  // Condition
    ApplyHighValueDiscount)                    // Activity
```

### Branches

Create branches for more complex conditional logic:

```csharp
.Branch(payload => payload.IsExpressShipping)
    .Do(CalculateExpressShippingFee)
    .Do(PrioritizeOrder)
    .EndBranch()
.Branch(payload => !payload.IsExpressShipping)
    .Do(CalculateStandardShippingFee)
    .EndBranch()
```

### Branches with Local Payload

Create isolated branches with their own local payload type that doesn't affect the main workflow payload:

```csharp
.BranchWithLocalPayload(
    // Condition
    payload => payload.NeedsCustomization,

    // Local payload factory
    mainPayload => new CustomizationPayload(
        AvailableOptions: new[] { "Engraving", "Gift Wrap" },
        SelectedOptions: new string[0],
        CustomizationCost: 0m
    ),

    // Branch configuration
    branch => branch
        .Do((mainPayload, localPayload) => {
            // Activity can access and modify both payloads
            var selectedOption = "Engraving";

            var updatedLocalPayload = localPayload with {
                SelectedOptions = new[] { selectedOption },
                CustomizationCost = 10.00m
            };

            var updatedMainPayload = mainPayload with {
                FinalPrice = mainPayload.Price + updatedLocalPayload.CustomizationCost
            };

            return Either<OrderError, (OrderPayload, CustomizationPayload)>.FromRight(
                (updatedMainPayload, updatedLocalPayload));
        })
)
```

### Finally Blocks

Activities that execute regardless of workflow success or failure:

```

```
