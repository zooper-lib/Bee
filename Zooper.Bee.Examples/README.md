# Zooper.Bee Workflow Examples

This project contains comprehensive examples of how to use the Zooper.Bee workflow system. The examples demonstrate all features of the workflow framework including validations, activities, conditional activities, branches, and finally blocks.

## Running the Example Application

The project includes a console application that demonstrates the workflow in action:

```
dotnet run
```

This will run the basic example. You can also run the example with pattern matching:

```
dotnet run pattern
```

The application has three parts:

1. A basic example showing a successful workflow
2. Examples showing both success and failure cases with pattern matching
3. An interactive section where you can choose different scenarios

## Order Processing Example

The main example is an order processing workflow that demonstrates:

- Input validation (customer info, order items, shipping address)
- Multiple activities (calculating totals, applying discounts, payment processing)
- Conditional activities (international shipping verification)
- Branching logic (express shipping vs. standard shipping)
- Error handling (payment failures, inventory issues)
- Finally blocks (logging)

### Key Files

1. `Program.cs` - Console application entry point
2. `OrderProcessingExample.cs` - Contains the complete workflow definition and implementation
3. `OrderProcessingUsage.cs` - Shows how to use the workflow in application code

### Usage in Code

To use the workflow in your own code:

```csharp
// Create a sample order request
var order = new OrderRequest(/* order details */);

// Run the workflow
var result = await OrderProcessingExample.ProcessOrderAsync(order);

// Handle the result
if (result.IsRight)
{
    // Success case - use the confirmation
    var confirmation = result.Right;
    Console.WriteLine($"Order {confirmation.OrderId} processed successfully");
}
else
{
    // Error case - handle the failure
    var error = result.Left;
    Console.WriteLine($"Order processing failed: {error.Message}");
}
```

## Key Workflow Concepts

The example demonstrates several key concepts:

### 1. Building the Workflow

```csharp
var workflow = new WorkflowBuilder<TRequest, TPayload, TSuccess, TError>(
    contextFactory,  // Function that creates the initial payload from the request
    resultSelector)  // Function that creates the success result from the final payload
    .Validate(...)   // Add validations
    .Do(...)         // Add activities
    .DoIf(...)       // Add conditional activities
    .Branch(...)     // Add branching logic
    .Finally(...)    // Add finally activities
    .Build();        // Build the workflow
```

### 2. Validations

Validations are functions that check if the request is valid before processing begins. They return an `Option<TError>` - `None` if valid or `Some(error)` if invalid.

```csharp
private static Option<OrderError> ValidateCustomerId(OrderRequest request)
{
    if (string.IsNullOrEmpty(request.CustomerId))
    {
        return Option<OrderError>.Some(new OrderError("INVALID_CUSTOMER", "Customer ID is required"));
    }

    return Option<OrderError>.None();
}
```

### 3. Activities

Activities are functions that process the payload and return either a success result with the updated payload or a failure result with an error.

```csharp
private static Either<OrderError, OrderPayload> CalculateOrderTotals(OrderPayload payload)
{
    // Process the payload...

    // Return success with updated payload
    return Either<OrderError, OrderPayload>.FromRight(updatedPayload);

    // Or return failure
    // return Either<OrderError, OrderPayload>.FromLeft(new OrderError(...));
}
```

### 4. Conditional Activities and Branches

Conditional activities and branches let you execute activities only when certain conditions are met.

```csharp
// Conditional activity
.DoIf(
    payload => payload.IsInternationalShipping,  // Condition
    VerifyInternationalShipping)                 // Activity to run if condition is true

// Branch with multiple activities
.Branch(payload => payload.IsExpressShipping)    // Condition
    .Do(CalculateExpressShippingFee)            // Activity 1
    .Do(PrioritizeOrder)                        // Activity 2
    .EndBranch()                                // End the branch
```

### 5. Error Handling

Errors are handled automatically by the workflow. When an activity returns a failure result, the workflow stops and returns the error.

## Best Practices

1. Keep payloads immutable (use records or readonly properties)
2. Use meaningful error codes and messages
3. Separate business logic from workflow definition
4. Use async methods for I/O operations
5. Implement proper error handling at workflow boundaries
