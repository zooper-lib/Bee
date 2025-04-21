# Zooper.Bee

<img src="icon.png" alt="Zooper.Bee Logo" width="120" align="right"/>

[![NuGet Version](https://img.shields.io/nuget/v/Zooper.Bee.svg)](https://www.nuget.org/packages/Zooper.Bee/)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](https://opensource.org/licenses/MIT)

A flexible and powerful workflow library for .NET that allows you to define complex business processes with a fluent API.

## Overview

Zooper.Bee lets you create workflows that process requests and produce either successful results or meaningful errors. The library uses a builder pattern to construct workflows with various execution patterns including sequential, conditional, parallel, and detached operations.

## Key Concepts

- **Workflow**: A sequence of operations that process a request to produce a result or error
- **Request**: The input data to the workflow
- **Payload**: Data that passes through and gets modified by workflow activities
- **Success**: The successful result of the workflow
- **Error**: The error result if the workflow fails

## Installation

```bash
dotnet add package Zooper.Bee
```

## Getting Started

```csharp
// Define a simple workflow
var workflow = new WorkflowBuilder<Request, Payload, SuccessResult, ErrorResult>(
    // Factory function that creates the initial payload from the request
    request => new Payload { Data = request.Data },

    // Selector function that creates the success result from the final payload
    payload => new SuccessResult { ProcessedData = payload.Data }
)
.Validate(request =>
{
    // Validate the request
    if (string.IsNullOrEmpty(request.Data))
        return Option<ErrorResult>.Some(new ErrorResult { Message = "Data is required" });

    return Option<ErrorResult>.None;
})
.Do(payload =>
{
    // Process the payload
    payload.Data = payload.Data.ToUpper();
    return Either<ErrorResult, Payload>.FromRight(payload);
})
.Build();

// Execute the workflow
var result = await workflow.Execute(new Request { Data = "hello world" }, CancellationToken.None);
if (result.IsRight)
{
    Console.WriteLine($"Success: {result.Right.ProcessedData}"); // Output: Success: HELLO WORLD
}
else
{
    Console.WriteLine($"Error: {result.Left.Message}");
}
```

## Building Workflows

### Basic Operations

#### Validation

Validates the incoming request before processing begins.

```csharp
// Asynchronous validation
.Validate(async (request, cancellationToken) =>
{
    var isValid = await ValidateAsync(request, cancellationToken);
    return isValid ? Option<ErrorResult>.None : Option<ErrorResult>.Some(new ErrorResult());
})

// Synchronous validation
.Validate(request =>
{
    var isValid = Validate(request);
    return isValid ? Option<ErrorResult>.None : Option<ErrorResult>.Some(new ErrorResult());
})
```

#### Activities

Activities are the building blocks of a workflow. They process the payload and can produce either a success (with modified payload) or an error.

```csharp
// Asynchronous activity
.Do(async (payload, cancellationToken) =>
{
    var result = await ProcessAsync(payload, cancellationToken);
    return Either<ErrorResult, Payload>.FromRight(result);
})

// Synchronous activity
.Do(payload =>
{
    var result = Process(payload);
    return Either<ErrorResult, Payload>.FromRight(result);
})

// Multiple activities
.DoAll(
    payload => DoFirstThing(payload),
    payload => DoSecondThing(payload),
    payload => DoThirdThing(payload)
)
```

#### Conditional Activities

Activities that only execute if a condition is met.

```csharp
.DoIf(
    payload => payload.ShouldProcess, // Condition
    payload =>
    {
        // Activity that only executes if the condition is true
        payload.Data = Process(payload.Data);
        return Either<ErrorResult, Payload>.FromRight(payload);
    }
)
```

### Advanced Features

#### Groups

Organize related activities into logical groups. Groups can have conditions and always merge their results back to the main workflow.

```csharp
.Group(
    payload => payload.ShouldProcessGroup, // Optional condition
    group => group
        .Do(payload => FirstActivity(payload))
        .Do(payload => SecondActivity(payload))
        .Do(payload => ThirdActivity(payload))
)
```

#### Contexts with Local State

Create a context with local state that is accessible to all activities within the context. This helps encapsulate related operations.

```csharp
.WithContext(
    null, // No condition, always execute
    payload => new LocalState { Counter = 0 }, // Create local state
    context => context
        .Do((payload, state) =>
        {
            state.Counter++;
            return (payload, state);
        })
        .Do((payload, state) =>
        {
            payload.Result = $"Counted to {state.Counter}";
            return (payload, state);
        })
)
```

#### Parallel Execution

Execute multiple groups of activities in parallel and merge the results.

```csharp
.Parallel(
    null, // No condition, always execute
    parallel => parallel
        .Group(group => group
            .Do(payload => { payload.Result1 = "Result 1"; return payload; })
        )
        .Group(group => group
            .Do(payload => { payload.Result2 = "Result 2"; return payload; })
        )
)
```

#### Detached Execution

Execute activities in the background without waiting for their completion. Results from detached activities are not merged back into the main workflow.

```csharp
.Detach(
    null, // No condition, always execute
    detached => detached
        .Do(payload =>
        {
            // This runs in the background
            LogActivity(payload);
            return payload;
        })
)
```

#### Parallel Detached Execution

Execute multiple groups of detached activities in parallel without waiting for completion.

```csharp
.ParallelDetached(
    null, // No condition, always execute
    parallelDetached => parallelDetached
        .Detached(detached => detached
            .Do(payload => { LogActivity1(payload); return payload; })
        )
        .Detached(detached => detached
            .Do(payload => { LogActivity2(payload); return payload; })
        )
)
```

#### Finally Block

Activities that always execute, even if the workflow fails.

```csharp
.Finally(payload =>
{
    // Cleanup or logging
    CleanupResources(payload);
    return Either<ErrorResult, Payload>.FromRight(payload);
})
```

## Advanced Patterns

### Error Handling

```csharp
.Do(payload =>
{
    try
    {
        var result = RiskyOperation(payload);
        return Either<ErrorResult, Payload>.FromRight(result);
    }
    catch (Exception ex)
    {
        return Either<ErrorResult, Payload>.FromLeft(new ErrorResult { Message = ex.Message });
    }
})
```

### Conditional Branching

Use conditions to determine which path to take in a workflow.

```csharp
.Group(
    payload => payload.Type == "TypeA",
    group => group
        .Do(payload => ProcessTypeA(payload))
)
.Group(
    payload => payload.Type == "TypeB",
    group => group
        .Do(payload => ProcessTypeB(payload))
)
```

## Performance Considerations

- Use `Parallel` for CPU-bound operations that can benefit from parallel execution
- Use `Detach` for I/O operations that don't affect the main workflow
- Be mindful of resource contention in parallel operations
- Consider using `WithContext` to maintain state between related activities

## Best Practices

1. Keep activities small and focused on a single responsibility
2. Use descriptive names for your workflow methods
3. Group related activities together
4. Handle errors at appropriate levels
5. Use `Finally` for cleanup operations
6. Validate requests early to fail fast
7. Use contextual state to avoid passing too many parameters

## License

MIT License (Copyright details here)
