# Zooper.Bee.Generators.Sample

This project demonstrates how to use the Zooper.Bee workflow system with the source generators for property dependency tracking. It showcases a complete order processing workflow that benefits from automatic dependency validation between properties.

## Key Features Demonstrated

1. **Type-Safe Property References**: Using lambda expressions to reference properties in the workflow builder
2. **Automatic Dependency Validation**: Steps that depend on previous results are automatically validated
3. **Self-Documenting Dependencies**: Property dependencies are explicitly declared using attributes

## Project Structure

- **OrderProcessingPayload.cs**: The payload class with `[WorkflowPayload]` and `[WorkflowProperty]` attributes
- **OrderProcessingResult.cs**: Result and error types for the workflow
- **OrderProcessingWorkflow.cs**: The main workflow implementation using the generated extensions
- **Program.cs**: A simple demonstration program

## How It Works

### 1. Payload Declaration with Dependencies

The `OrderProcessingPayload` class is marked with `[WorkflowPayload]` and has properties marked with `[WorkflowProperty]`, specifying their dependencies:

```csharp
[WorkflowPayload]
public partial record OrderProcessingPayload
{
    // No dependencies
    [WorkflowProperty(Description = "Subtotal of all line items")]
    public decimal Subtotal { get; init; }

    // Depends on Subtotal
    [WorkflowProperty(DependsOn = new[] { nameof(Subtotal) },
        Description = "Amount of discount applied")]
    public decimal DiscountAmount { get; init; }

    // Complex dependencies
    [WorkflowProperty(DependsOn = new[] { nameof(Subtotal), nameof(TaxAmount),
        nameof(DiscountAmount), nameof(ShippingCost) },
        Description = "Final total amount")]
    public decimal TotalAmount { get; init; }
}
```

### 2. Source Generator Output

The source generator creates:

- Property state checking methods (`HasSubtotal`, `HasDiscountAmount`, etc.)
- Validation methods for properties with dependencies
- Type-safe property tokens
- Extension methods for the workflow builder

### 3. Using the Generated Code

The `OrderProcessingWorkflow` uses lambda expressions to reference properties in a type-safe way:

```csharp
return new WorkflowBuilder<OrderRequest, OrderProcessingPayload, OrderProcessingResult, OrderProcessingError>(
    request => new OrderProcessingPayload(request),
    CreateOrderResult)

    // Type-safe property reference
    .Do(p => p.Subtotal, CalculateSubtotal)

    // Dependencies will be automatically checked
    .Do(p => p.TaxAmount, CalculateTaxAmount)

    // Complex dependencies are all validated
    .Do(p => p.TotalAmount, CalculateTotalAmount)

    .Build();
```

## Benefits

1. **Compile-Time Safety**: Property references are checked at compile time
2. **Runtime Validation**: Dependencies are validated at runtime before activities execute
3. **Flexible Ordering**: Activities can be reordered as long as dependencies are still met
4. **Clear Error Messages**: Dependency violations produce helpful, specific error messages

## Running the Demo

Execute the `Program.cs` file to see the workflow in action:

1. A sample order is created
2. The workflow processes it with automatic dependency checking
3. The result is displayed, either success or error

## Generated Code

You can examine the generated source files in your IDE's solution explorer, typically under:

- The "Dependencies" node
- Then "Analyzers"
- Then "Zooper.Bee.Generators"
- Then "Zooper.Bee.Generators.WorkflowPayloadIncrementalGenerator"

This will show you exactly what code was generated for the `OrderProcessingPayload` class.
