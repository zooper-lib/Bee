# Zooper.Bee

<img src="icon.png" alt="Zooper.Bee Logo" width="120" align="right"/>

[![NuGet Version](https://img.shields.io/nuget/v/Zooper.Bee.svg)](https://www.nuget.org/packages/Zooper.Bee/)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](https://opensource.org/licenses/MIT)

A flexible and powerful railway-oriented programming library for .NET that allows you to define complex business processes with a fluent
API.

## Overview

Zooper.Bee lets you create railways that process requests and produce either successful results or meaningful errors.
The library uses a builder pattern to construct railways with various execution patterns including sequential,
conditional, parallel, and detached operations.

## Key Concepts

- **Railway**: A sequence of operations that process a request to produce a result or error
- **Request**: The input data to the railway
- **Payload**: Data that passes through and gets modified by railway activities
- **Success**: The successful result of the railway
- **Error**: The errors result if the railway fails

## Installation

```bash
dotnet add package Zooper.Bee
```

## Getting Started

```csharp
// Define a simple railway
var railway = Railway.Create<Request, Payload, SuccessResult, ErrorResult>(
    // Factory function that creates the initial payload from the request
    factory:  request => new Payload { Data = request.Data },

    // Selector function that creates the success result from the final payload
    selector: payload => new SuccessResult { ProcessedData = payload.Data },

    // Step execution phase
    steps: s => s
        .Validate(request =>
        {
            if (string.IsNullOrEmpty(request.Data))
                return Option<ErrorResult>.Some(new ErrorResult { Message = "Data is required" });
            return Option<ErrorResult>.None;
        })
        .Do(payload =>
        {
            payload.Data = payload.Data.ToUpper();
            return Either<ErrorResult, Payload>.FromRight(payload);
        })
);

// Execute the railway
var result = await railway.Execute(new Request { Data = "hello world" }, CancellationToken.None);
if (result.IsRight)
{
    Console.WriteLine($"Success: {result.Right.ProcessedData}"); // Output: Success: HELLO WORLD
}
else
{
    Console.WriteLine($"Error: {result.Left.Message}");
}
```

## Building Railways

Railways are created with `Railway.Create()`, which takes two separate configuration lambdas:

- **`guards`** — optional; declares guards and validations that run before the payload is created
- **`steps`** — required; declares all activities that transform the payload

This two-phase separation makes it structurally impossible to mix guard registration with step
registration. `Guard()` and `Validate()` are not available inside `steps`, and `Do()`/`Group()`/etc.
are not available inside `guards`.

```csharp
var railway = Railway.Create<Request, Payload, Success, Error>(
    factory:  request => new Payload(request),
    selector: payload => new Success(payload.Result),
    guards: g => g
        .Guard(request => /* auth check */)
        .Validate(request => /* input validation */),
    steps: s => s
        .Do(payload => /* step 1 */)
        .Group(null, g => g
            .Do(payload => /* step 2a */)
            .Do(payload => /* step 2b */))
        .Do(payload => /* step 3 */)
);
```

When no guards are needed, omit the `guards` parameter:

```csharp
var railway = Railway.Create<Request, Payload, Success, Error>(
    factory:  request => new Payload(request),
    selector: payload => new Success(payload.Result),
    steps: s => s
        .Do(payload => /* ... */)
);
```

### Validation

Validations run before any step and reject the request early when invalid.

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

### Guards

Guards check whether the railway is allowed to execute at all — authentication,
authorization, feature flags, etc. They always run before any step, regardless of
where they appear in the `guards` lambda.

```csharp
guards: g => g
    // Asynchronous guard
    .Guard(async (request, cancellationToken) =>
    {
        var isAuthorized = await CheckAuthorizationAsync(request, cancellationToken);
        return isAuthorized
            ? Either<ErrorResult, Unit>.FromRight(Unit.Value)
            : Either<ErrorResult, Unit>.FromLeft(new ErrorResult { Message = "Unauthorized" });
    })
    // Synchronous guard
    .Guard(request =>
    {
        var isAuthorized = CheckAuthorization(request);
        return isAuthorized
            ? Either<ErrorResult, Unit>.FromRight(Unit.Value)
            : Either<ErrorResult, Unit>.FromLeft(new ErrorResult { Message = "Unauthorized" });
    })
```

#### Benefits of Guards

- Guards run before the payload is created, providing the earliest possible short-circuit
- The `guards` phase is structurally separate from the `steps` phase — it is impossible to
  accidentally register a guard after a step
- Common checks like authentication can be standardized and reused

### Activities

Activities are the building blocks of a railway. They process the payload and can produce either a success (with
the modified payload) or an error.

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

### Conditional Activities

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

### Groups

Organize related activities into logical groups. Groups can have conditions and always merge their results back to the
main railway.

```csharp
.Group(
    payload => payload.ShouldProcessGroup, // Optional condition
    group => group
        .Do(payload => FirstActivity(payload))
        .Do(payload => SecondActivity(payload))
        .Do(payload => ThirdActivity(payload))
)
```

### Contexts with Local State

Create a context with the local state that is accessible to all activities within the context. This helps encapsulate
related operations.

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

### Parallel Execution

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

### Detached Execution

Execute activities in the background without waiting for their completion. Results from detached activities are not
merged back into the main railway.

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

### Parallel Detached Execution

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

### Finally Block

Activities that always execute, even if the railway fails.

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

Use conditions to determine which path to take in a railway.

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

## Dependency Injection Integration

Zooper.Bee integrates seamlessly with .NET's dependency injection system. You can register all railway components with
a single extension method:

```csharp
// In Startup.cs or Program.cs
services.AddRailways();
```

This will scan all assemblies and register:

- All railway validations
- All railway activities
- All concrete railway classes (classes ending with "Railway")

You can also register specific components:

```csharp
// Register only validations
services.AddRailwayValidations();

// Register only activities
services.AddRailwayActivities();

// Specify which assemblies to scan
services.AddRailways(new[] { typeof(Program).Assembly });

// Specify service lifetime (Singleton, Scoped, Transient)
services.AddRailways(lifetime: ServiceLifetime.Singleton);
```

## Performance Considerations

- Use `Parallel` for CPU-bound operations that can benefit from parallel execution
- Use `Detach` for I/O operations that don't affect the main railway
- Be mindful of resource contention in parallel operations
- Consider using `WithContext` to maintain state between related activities

## Best Practices

1. Keep activities small and focused on a single responsibility
2. Use descriptive names for your railway methods
3. Group related activities together
4. Handle errors at appropriate levels
5. Use `Finally` for cleanup operations
6. Validate requests early to fail fast
7. Use contextual state to avoid passing too many parameters

## Migration from `RailwayBuilder` to `Railway.Create()`

As of the latest version, `RailwayBuilder` and `RailwayBuilderFactory` are `[Obsolete]`.
Use `Railway.Create()` instead.

### Before

```csharp
var railway = new RailwayBuilder<Request, Payload, Success, Error>(
        request => new Payload(request),
        payload => new Success(payload.Result))
    .Guard(request => /* ... */)
    .Validate(request => /* ... */)
    .Do(payload => /* ... */)
    .Group(null, g => g.Do(payload => /* ... */))
    .Build();
```

### After

```csharp
var railway = Railway.Create<Request, Payload, Success, Error>(
    factory:  request => new Payload(request),
    selector: payload => new Success(payload.Result),
    guards: g => g
        .Guard(request => /* ... */)
        .Validate(request => /* ... */),
    steps: s => s
        .Do(payload => /* ... */)
        .Group(null, g => g.Do(payload => /* ... */)));
```

## Migration from Workflow to Railway

As of the latest version, all `Workflow` classes have been renamed to `Railway` to better reflect the railway-oriented programming pattern used by the library. The old `Workflow` names are preserved as `[Obsolete]` shims for backward compatibility.

### What changed

| Old Name | New Name |
|---|---|
| `Workflow<TRequest, TSuccess, TError>` | `Railway<TRequest, TSuccess, TError>` |
| `WorkflowBuilder<...>` | `RailwayBuilder<...>` |
| `WorkflowBuilderFactory` | `RailwayBuilderFactory` |
| `CreateWorkflow<...>()` | `CreateRailway<...>()` |
| `IWorkflowStep` | `IRailwayStep` |
| `IWorkflowValidation` | `IRailwayValidation` |
| `IWorkflowGuard` | `IRailwayGuard` |
| `AddWorkflows()` | `AddRailways()` |
| `AddWorkflowSteps()` | `AddRailwaySteps()` |

### Backward compatibility

All old type names and extension methods are still available but marked with `[Obsolete]`. Your existing code will continue to compile and work, but you will see deprecation warnings encouraging you to migrate to the new names.

### How to migrate

1. Replace all `Workflow<` type references with `Railway<`
2. Replace `WorkflowBuilder<` with `RailwayBuilder<`
3. Replace `WorkflowBuilderFactory.CreateWorkflow<` with `RailwayBuilderFactory.CreateRailway<`
4. Replace DI registration calls (`AddWorkflows()` -> `AddRailways()`, etc.)
5. Update any interface implementations (`IWorkflowStep` -> `IRailwayStep`, etc.)

## License

MIT License (Copyright details here)