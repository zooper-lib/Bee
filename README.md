# Zooper.Bee

<img src="icon.png" alt="Zooper.Bee Logo" width="120" align="right"/>

[![NuGet Version](https://img.shields.io/nuget/v/Zooper.Bee.svg)](https://www.nuget.org/packages/Zooper.Bee/)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](https://opensource.org/licenses/MIT)

A flexible and powerful railway-oriented programming library for .NET that lets you define complex business processes with a fluent API.

## Overview

Zooper.Bee lets you create railways that process a request and produce either a successful result or a meaningful error. The pipeline is built from composable operators registered on a fluent builder. Operators run in registration order; each one receives the full `Either` state produced by the previous operator.

## Key Concepts

| Term | Description |
|------|-------------|
| **Railway** | The built pipeline. Call `.Execute(request, ct)` to run it. |
| **Request** | The input handed to the railway on each execution. |
| **Payload** | The mutable working object that flows through every step. Created from the request by the `factory` function. |
| **Success** | The final result produced from the payload by the `selector` function when the pipeline completes on the `Right` rail. |
| **Error** | The value returned when the pipeline terminates on the `Left` rail. |
| **Right rail** | The happy path — payload is valid and flows forward. |
| **Left rail** | The error path — payload is replaced by an error value. Most operators skip on `Left`. |

## Installation

```bash
dotnet add package Zooper.Bee
```

## Getting Started

```csharp
var railway = Railway.Create<CreateOrderRequest, OrderPayload, OrderId, OrderError>(
    factory:  request => new OrderPayload(request.CustomerId, request.Items),
    selector: payload => new OrderId(payload.Id),
    steps: s => s
        .Do(payload =>
        {
            payload.TotalPrice = payload.Items.Sum(i => i.Price);
            return payload;  // implicit Right
        })
        .Tap(payload => Console.WriteLine($"Order total: {payload.TotalPrice}"))
);

var result = await railway.Execute(request, CancellationToken.None);

if (result.IsRight)
    Console.WriteLine($"Order created: {result.Right.Id}");
else
    Console.WriteLine($"Error: {result.Left.Message}");
```

## Creating a Railway

```csharp
// With validations and guards
var railway = Railway.Create<TRequest, TPayload, TSuccess, TError>(
    factory:     request => new TPayload(request),
    selector:    payload => new TSuccess(payload.Result),
    validations: v => v.Validate(...).Validate(...),
    guards:      g => g.Guard(...).Guard(...),
    steps:       s => s.Do(...).Tap(...).Finally(...)
);

// Guards only (no validations)
var railway = Railway.Create<TRequest, TPayload, TSuccess, TError>(
    factory:  request => new TPayload(request),
    selector: payload => new TSuccess(payload.Result),
    guards:   g => g.Guard(...),
    steps:    s => s.Do(...).Tap(...)
);

// Steps only (no validations or guards)
var railway = Railway.Create<TRequest, TPayload, TSuccess, TError>(
    factory:  request => new TPayload(request),
    selector: payload => new TSuccess(payload.Result),
    steps:    s => s.Do(...).Tap(...)
);

// Parameterless (no request)
var railway = Railway.Create<TPayload, TSuccess, TError>(
    factory:  () => new TPayload(),
    selector: payload => new TSuccess(payload.Result),
    steps:    s => s.Do(...)
);
```

A railway runs in three structurally separate phases that always execute in this order: **Validation → Guarding → Steps**. Each phase has its own builder — `Validate` is only available in the validation builder, `Guard` only in the guard builder, and pipeline operators only in the steps builder. The phase a check belongs to is determined by which delegate registers it, not by call order.

---

## Validation Phase

Validations run **first**, before guards and before the payload is created. They use `Option<TError>` — return `Some(error)` to reject or `None` to allow. Suited for input validation rules.

```csharp
validations: v => v
    .Validate(request => string.IsNullOrEmpty(request.Name)
        ? Option<Error>.Some(new Error("Name is required"))
        : Option<Error>.None)

    .Validate(async (request, ct) =>
    {
        var exists = await db.ExistsAsync(request.Id, ct);
        return exists ? Option<Error>.None : Option<Error>.Some(new Error("Not found"));
    })
```

**Behavior:**
- Run before guards and before any step (earliest possible short-circuit)
- Execute in registration order; first failing validation short-circuits — subsequent validations, all guards, and all steps do not run
- On failure: returns `Left(error)`, pipeline does not execute

---

## Guarding Phase

Guards run **after** all validations pass and before the payload is created. They check whether the railway is allowed to execute — authentication, authorization, feature flags, etc. Return `Right(Unit)` to allow or `Left(error)` to reject.

```csharp
guards: g => g
    // Async
    .Guard(async (request, ct) =>
    {
        var ok = await authService.IsAuthorizedAsync(request.UserId, ct);
        if (!ok) return new Error("Unauthorized");  // implicit Left
        return Unit.Value;                           // implicit Right
    })
    // Sync
    .Guard(request =>
    {
        if (request.UserId == Guid.Empty) return new Error("Invalid user");  // implicit Left
        return Unit.Value;                                                    // implicit Right
    })
```

**Behavior:**
- Run after all validations pass, before any step and before the payload is created
- Execute in registration order; first failing guard short-circuits — subsequent guards and all steps do not run
- On failure: returns `Left(error)`, pipeline does not execute

### Conditional guards (`When`)

Group guards under a condition over the request so they run only when it holds. When the condition is `false`, the whole group is skipped (treated as a pass). The condition has sync and async forms; groups may nest (conditions compose with logical AND).

```csharp
guards: g => g
    .Guard(request => /* always runs */ Unit.Value)
    .When(
        condition: request => request.PlanTier == PlanTier.Premium,
        configure: grp => grp
            .Guard(request => request.SeatsUsed <= request.SeatLimit
                ? Unit.Value
                : new Error("Seat limit exceeded"))
            // async condition form:
            .Guard(async (request, ct) =>
                await quotaService.HasQuotaAsync(request.AccountId, ct)
                    ? Unit.Value
                    : new Error("Quota exhausted")))
```

- **Condition true:** the group's guards run as normal guards (first failure short-circuits)
- **Condition false:** none of the group's guards run; the rail continues unchanged
- Plain `Guard(...)` registered outside a `When` group always runs

---

## Step Operators

All step operators are registered on `RailwayStepsBuilder` inside the `steps` lambda.

The most important distinction between operators is **whether their result is fed back into the pipeline**:

- **Payload-replacing** (`Do`, `When`, `Loop`, `Recover`) — the operator's return value becomes the new pipeline state. Downstream operators see the updated payload.
- **Pass-through** (`Tap`, `TryTap`, `Effects`, `TryEffects`, `Ensure`) — the operator reads the payload but its return value is **not** fed back. The payload flowing to the next operator is identical to what came in.
- **Fire-and-forget** (`Detach`) — result is discarded and nothing is awaited.
- **Out-of-band** (`Finally`) — runs outside the pipeline; its return value is discarded and does not affect the pipeline result.

### Do

The primary progression operator. Executes a delegate that may transform the payload or return an error.

```csharp
// Async
.Do(async (payload, ct) =>
{
    var result = await externalService.ProcessAsync(payload.Data, ct);
    if (!result.Success) return new Error(result.Message);  // implicit Left
    payload.Result = result.Value;
    return payload;  // implicit Right
})

// Sync
.Do(payload =>
{
    payload.Price = payload.Quantity * payload.UnitPrice;
    return payload;  // implicit Right
})
```

**Behavior:**
- **Result is fed back into the pipeline.** Whatever `Either` the delegate returns becomes the new pipeline state — downstream operators see the new payload on `Right`, or the error on `Left`.
- **On Right:** executes the delegate with the current payload; returns its result as the new state
- **On Left:** skips — the existing error propagates unchanged

---

### Ensure

Asserts that a business rule holds. Fails the pipeline when the predicate returns `false`.

```csharp
.Ensure(
    when:     payload => payload.Items.Count > 0,
    failWith: payload => new Error("Order must have at least one item")
)
```

**Behavior:**
- **Result is NOT fed back.** No new payload is produced. The existing payload either passes through unchanged or the pipeline is switched to the error rail.
- **On Right:** evaluates `when(payload)` — if `false`, transitions to `Left(failWith(payload))`; if `true`, the existing state passes through unchanged
- **On Left:** skips without evaluating the predicate

---

### Tap

A strict pass-through side effect. The payload is never changed. Exceptions propagate to the caller.

Three overloads:

```csharp
// Sync — fire and forget
.Tap(payload => logger.LogInformation("Processing order {Id}", payload.Id))

// Async — fire and forget
.Tap(async (payload, ct) =>
{
    await auditLog.RecordAsync(payload.Id, ct);
})

// Async with error channel — can fail the pipeline
.Tap(async (payload, ct) =>
{
    var ok = await notificationService.SendAsync(payload.Email, ct);
    if (!ok) return new Error("Notification failed");  // implicit Left
    return Unit.Value;                                  // implicit Right
})
```

**Behavior:**
- **Result is NOT fed back.** The payload flowing to the next operator is the same one that came in.
- **On Right:** executes the side effect; the existing state passes through unchanged. The `Either<TError, Unit>` overload can switch the pipeline to the error rail, but the payload is still not replaced.
- **On Left:** skips without invoking the delegate

---

### TryTap

Like `Tap` but swallows all exceptions. Use when the side effect is best-effort and must never fail the pipeline.

```csharp
// Sync
.TryTap(payload => metrics.Increment("orders.created"))

// Async
.TryTap(async (payload, ct) =>
{
    await cache.InvalidateAsync(payload.CacheKey, ct);
})
```

**Behavior:**
- **Result is NOT fed back.** The payload flowing to the next operator is the same one that came in.
- **On Right:** executes the side effect; exceptions are swallowed; the existing state passes through unchanged regardless of failure
- **On Left:** skips without invoking the delegate

---

### Effects

Groups multiple strict side effects. Executes them in registration order using an inner `EffectsBuilder`. The first failure short-circuits the group.

```csharp
.Effects(fx => fx
    .Do(payload => auditLog.Record(payload.Id))
    .Do(async (payload, ct) => await emailService.SendConfirmationAsync(payload.Email, ct))
    .Do(async (payload, ct) =>
    {
        var result = await inventoryService.ReserveAsync(payload.Items, ct);
        if (!result.IsSuccess) return new Error("Insufficient stock");
        return Unit.Value;
    })
)
```

**Behavior:**
- **Result is NOT fed back.** Inner effects signal success or failure via `Either<TError, Unit>` — they cannot produce a new payload. The payload flowing to the next operator is the same one that came in.
- **On Right:** runs each inner effect in order; the first `Left` result short-circuits the group and switches the pipeline to the error rail; on success the existing state passes through unchanged
- **On Left:** skips the entire group

---

### TryEffects

Like `Effects` but best-effort — exceptions and errors from every inner effect are swallowed, and all effects are always attempted regardless of failures.

```csharp
.TryEffects(fx => fx
    .Do(payload => metrics.Increment("orders.processed"))
    .Do(async (payload, ct) => await cache.WarmAsync(payload.Id, ct))
)
```

**Behavior:**
- **Result is NOT fed back.** Like `Effects`, but every inner effect is always attempted and all failures are silently swallowed. The payload flowing to the next operator is the same one that came in.
- **On Right:** runs all inner effects in order; exceptions swallowed; the existing state passes through unchanged
- **On Left:** skips the entire group

---

### Detach

Fires a group of effects in the background (`Task.Run`) without awaiting their completion. The pipeline continues immediately on the `Right` rail. Exceptions inside detached effects are always swallowed.

```csharp
.Detach(fx => fx
    .Do(async (payload, ct) => await analyticsService.TrackAsync(payload.EventData, ct))
    .Do(payload => slowSideEffect.Execute(payload))
)
```

**Behavior:**
- **Result is discarded entirely.** Inner effects run on background threads and are never awaited. Their return values — success or failure — are completely ignored. The pipeline continues immediately with the same state it had before `Detach`.
- **On Right:** schedules each inner effect on `Task.Run`; returns immediately; the existing state passes through unchanged
- **On Left:** skips — nothing is scheduled
- Exceptions inside detached tasks are always swallowed

> Use `Detach` for fire-and-forget work that must never block the pipeline and whose failure is acceptable (analytics, non-critical logging, cache warming).

---

### When

Conditionally enters a sub-pipeline when the predicate returns `true`. The sub-pipeline shares the same `Either<TError, TPayload>` state. When the predicate returns `false`, the operator is a no-op.

> Formerly named `Branch`. `Branch` is still available as a deprecated alias that forwards to `When`, and will be removed in the next major version.

The inner builder (`BranchBuilder`) exposes `Do`, `Tap`, `TryTap`, `Effects`, `TryEffects`, `Recover`, and `Ensure`. `When`, `Detach`, and `Finally` are intentionally excluded.

```csharp
.When(
    condition: payload => payload.CustomerType == CustomerType.Premium,
    configure: b => b
        .Do(payload =>
        {
            payload.Discount = 0.20m;
            return payload;  // implicit Right
        })
        .Tap(payload => logger.LogInformation("Premium discount applied"))
        .Ensure(
            when:     p => p.Discount <= 1.0m,
            failWith: p => new Error("Discount cannot exceed 100%")
        )
)
```

**Behavior:**
- **Result IS fed back into the pipeline.** The sub-pipeline's final `Either` state replaces the main pipeline state — downstream operators see whatever payload (or error) the sub-pipeline produced.
- **On Right, predicate true:** runs the sub-pipeline; its final state becomes the new main pipeline state
- **On Right, predicate false:** no-op, the existing state passes through unchanged
- **On Left:** skips — predicate is not evaluated

---

### Loop

Executes a bounded iteration on the right rail. The body sub-pipeline (`LoopBuilder`) runs repeatedly until `until` returns `true` or `maxAttempts` is exhausted.

**Iteration order** (1-indexed attempt `N`):
1. Run the body. If it returns `Left`, exit the loop immediately with that `Left`.
2. Evaluate `until(payload, N)`. If `true`, exit with `Right(payload)`.
3. If `N == maxAttempts`, exit with `Left(exhausted(payload, N))`.
4. Apply `mutate(payload, N)` (if provided) to produce the starting payload for iteration `N+1`.
5. Repeat.

`mutate` never runs before the first iteration or after the loop exits. `exhausted` is the caller's delegate — the library has no default because `TError` is open. `maxAttempts` must be `>= 1`; the builder throws `ArgumentOutOfRangeException` at registration time if not.

`Recover<TErr>` inside the body catches errors scoped to that iteration only — it does not carry over to subsequent iterations.

The inner builder (`LoopBuilder`) exposes `Do`, `Tap`, `TryTap`, `Effects`, `TryEffects`, `When`, `Ensure`, and `Recover`. `Loop`, `Detach`, and `Finally` are intentionally excluded.

```csharp
// Poll-for-ready: no mutate needed
.Loop(
    body: lb => lb
        .Do(async (payload, ct) =>
        {
            var ready = await jobService.IsReadyAsync(payload.JobId, ct);
            return Either<Error, Payload>.FromRight(payload with { Ready = ready });
        }),
    until: (payload, _) => payload.Ready,
    maxAttempts: 10,
    exhausted: (payload, attempts) => new Error($"Job {payload.JobId} not ready after {attempts} polls"))

// Retry-with-mutation: Recover keeps iterating on transient failures; mutate adjusts for each attempt
.Loop(
    body: lb => lb
        .Do(async (payload, ct) =>
        {
            var result = await externalService.CallAsync(payload.Endpoint, ct);
            if (!result.Success)
                return Either<Error, Payload>.FromLeft(new TransientError(result.StatusCode));
            return Either<Error, Payload>.FromRight(payload with { Response = result.Body });
        })
        .Recover<TransientError>((err, last) => last with { LastError = err }),
    until: (payload, _) => payload.Response != null,
    maxAttempts: 5,
    exhausted: (payload, attempts) => new Error($"Max retries ({attempts}) exceeded"),
    mutate: (payload, attempt) => payload with { DelayMs = attempt * 100 })
```

**Behavior:**
- **Result IS fed back into the pipeline.** The loop's final `Either` state (from `until`, body `Left`, or exhaustion) replaces the main pipeline state.
- **On Right:** runs the body iteratively according to the iteration order above
- **On Left (incoming):** passes through unchanged — `body`, `until`, `mutate`, and `exhausted` are not invoked

---

### Recover

Recovers from a typed error. When the pipeline is on `Left` and the error value is assignable to `TErr`, the handler runs and its returned payload transitions the pipeline back to `Right`. Non-matching errors and `Right` values pass through unchanged.

The handler receives the **pre-failure payload snapshot** — the last `Right` payload before the error occurred.

```csharp
// Sync
.Recover<ValidationError>((err, lastPayload) =>
{
    lastPayload.IsValid = false;
    lastPayload.ValidationMessage = err.Message;
    return lastPayload;
})

// Async
.Recover<TimeoutError>(async (err, lastPayload, ct) =>
{
    var fallback = await fallbackService.GetAsync(lastPayload.Id, ct);
    lastPayload.Result = fallback;
    return lastPayload;
})
```

**Behavior:**
- **Result IS fed back into the pipeline.** The handler's returned payload replaces the pipeline state, transitioning from `Left` back to `Right`. Downstream operators see the recovered payload.
- **On Left, matching `TErr`:** runs the handler with `(error, lastRightSnapshot)` → the returned payload becomes the new `Right` state
- **On Left, non-matching type:** the existing `Left` passes through unchanged
- **On Right:** skips — the existing state passes through unchanged

---

### Finally

Registers one or more cleanup activities that **always execute**, regardless of whether the pipeline succeeded or failed. Multiple `Finally` registrations are all run in order. Exceptions thrown by a `Finally` activity are swallowed so that subsequent `Finally` activities always get a chance to run.

The activity receives the **last known `Right` payload** — if the pipeline never reached `Right`, this is the initial payload.

```csharp
// Sync
.Finally(payload => dbConnection.Close())

// Async
.Finally(async (payload, ct) =>
{
    await tempFileService.DeleteAsync(payload.TempFileId, ct);
})
```

**Behavior:**
- **Result is discarded entirely.** `Finally` callbacks run outside the pipeline state machine. Their return value has no effect on the `Either` result — success or failure inside `Finally` is irrelevant to downstream callers.
- Always executes regardless of whether the pipeline is on `Right` or `Left`
- Receives the last known `Right` payload (the pre-failure snapshot if the pipeline failed)
- Exceptions are swallowed so that subsequent `Finally` callbacks always get a chance to run

---

## Operator Quick Reference

| Operator | Result fed back? | On Right | On Left | Exceptions |
|----------|-----------------|----------|---------|------------|
| `Do` | **Yes** — new payload or error | Executes; result is the new state | Skips | Propagate |
| `Ensure` | No — payload unchanged | Fails pipeline if predicate false | Skips | — |
| `Tap` | No — payload unchanged | Runs side effect; state passes through; `Either<TError,Unit>` overload can switch to error rail | Skips | Propagate |
| `TryTap` | No — payload unchanged | Runs side effect; state passes through | Skips | Swallowed |
| `Effects` | No — payload unchanged | Runs group; first failure switches to error rail | Skips | Propagate |
| `TryEffects` | No — payload unchanged | Runs all; state passes through | Skips | Swallowed |
| `Detach` | No — discarded entirely | Schedules background tasks; returns immediately | Skips | Swallowed |
| `When` | **Yes** — sub-pipeline result | Runs sub-pipeline if predicate true; result is the new state | Skips | Propagate |
| `Loop` | **Yes** — loop's final Either | Runs body iteratively; exits on `until`, body `Left`, or exhaustion | Passes through unchanged | Propagate |
| `Recover<TErr>` | **Yes** — recovered payload | Skips | Matching error: handler result becomes new Right | Propagate |
| `Finally` | No — discarded entirely | Always runs; result ignored | Always runs; result ignored | Swallowed |

---

## MediatR Integration

Install the companion package:

```bash
dotnet add package Zooper.Bee.MediatR
```

Extend `RailwayHandler<TRequest, TPayload, TSuccess, TError>`:

```csharp
public class CreateOrderHandler
    : RailwayHandler<CreateOrderCommand, OrderPayload, OrderId, OrderError>
{
    protected override Func<CreateOrderCommand, OrderPayload> Factory =>
        cmd => new OrderPayload(cmd.CustomerId, cmd.Items);

    protected override Func<OrderPayload, OrderId> Selector =>
        payload => new OrderId(payload.Id);

    // Optional — omit if no guards needed
    protected override void ConfigureGuards(
        RailwayGuardBuilder<CreateOrderCommand, OrderPayload, OrderId, OrderError> g)
    {
        g.Guard(cmd =>
        {
            if (cmd.CustomerId == Guid.Empty) return new OrderError("Invalid customer");  // implicit Left
            return Unit.Value;                                                              // implicit Right
        });
    }

    protected override void ConfigureSteps(
        RailwayStepsBuilder<CreateOrderCommand, OrderPayload, OrderId, OrderError> s)
    {
        s.Do(payload =>
         {
             payload.TotalPrice = payload.Items.Sum(i => i.Price);
             return payload;  // implicit Right
         })
         .Tap(payload => logger.LogInformation("Order {Id} created", payload.Id))
         .Finally(payload => tempStorage.Release(payload.TempKey));
    }
}
```

---

## License

MIT License
