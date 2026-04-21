## ADDED Requirements

### Requirement: Tap Performs a Strict Pass-Through Side Effect

The `RailwayStepsBuilder.Tap(...)` operator SHALL execute a side-effect delegate against the current payload without changing the payload. `Tap` MUST NOT return a new payload.

When the rail is `Right(payload)`, the delegate MUST execute and the rail MUST remain `Right(payload)` on success. If the delegate fails — by throwing, or by returning `Left(err)` in the overload that supports an error channel — the rail MUST transition to `Left(err)`. When the rail is `Left`, the operator MUST pass the `Left` through unchanged and MUST NOT invoke the delegate.

The builder SHALL expose at least these overloads:
- `Tap(Action<TPayload>)` — synchronous, failure = thrown exception wrapped as `Left` per the builder's exception policy, or propagated per the builder's configured failure strategy (decided in implementation).
- `Tap(Func<TPayload, CancellationToken, Task>)` — asynchronous side effect, no error channel.
- `Tap(Func<TPayload, CancellationToken, Task<Either<TError, Unit>>>)` — asynchronous side effect with explicit error channel; `Left(err)` fails the rail.

#### Scenario: Tap runs without changing the payload

- **WHEN** the rail is `Right(payload)` and `Tap` receives a delegate that completes successfully
- **THEN** the rail after the step is still `Right(payload)`
- **AND** the delegate is invoked exactly once

#### Scenario: Tap with an error-channel delegate fails the rail on Left

- **WHEN** the rail is `Right(payload)` and `Tap`'s delegate returns `Left(err)`
- **THEN** the rail after the step is `Left(err)`
- **AND** subsequent non-recovery operators do not invoke their delegates

#### Scenario: Tap passes through an incoming Left

- **WHEN** the rail is `Left(error)` at the start of a `Tap`
- **THEN** the rail after the step is `Left(error)`
- **AND** the delegate is not invoked

### Requirement: Effects Groups Strict Pass-Through Side Effects

The `RailwayStepsBuilder.Effects(configure)` operator SHALL group multiple strict side effects into a single step. The nested builder (`EffectsBuilder<TPayload, TError>`) MUST expose `Do(...)` and MUST NOT expose `Tap`, `Branch`, `Recover`, `Ensure`, `Effects`, `TryTap`, `TryEffects`, `Detach`, or `Finally`. Inside `Effects`, each inner `Do` is a side-effect delegate with the same shape as `Tap` — it MUST NOT return a new payload.

When the rail is `Right(payload)`, each inner effect MUST execute in registration order. If an inner effect fails, the rail MUST transition to `Left(err)` and the remaining inner effects MUST NOT execute. If all inner effects succeed, the rail MUST remain `Right(payload)`. When the rail is `Left`, the operator MUST pass through and none of the inner effects MUST execute.

#### Scenario: Effects runs every inner effect in order on success

- **WHEN** the rail is `Right(payload)` and `Effects` contains three successful inner `Do` effects
- **THEN** all three effects execute in registration order
- **AND** the rail after the step is still `Right(payload)`

#### Scenario: Effects fails the rail on the first inner failure

- **WHEN** the second of three inner effects returns `Left(err)`
- **THEN** the rail after the step is `Left(err)`
- **AND** the third effect does not execute

#### Scenario: Effects passes through an incoming Left

- **WHEN** the rail is `Left(error)` at the start of `Effects`
- **THEN** the rail after the step is `Left(error)`
- **AND** none of the inner effects execute

### Requirement: TryTap Is a Best-Effort Single Side Effect

The `RailwayStepsBuilder.TryTap(...)` operator SHALL execute a side-effect delegate whose failure MUST NOT fail the rail. When the rail is `Right(payload)`, the delegate MUST execute; any exception thrown or `Left(err)` returned MUST be swallowed and the rail MUST remain `Right(payload)`. When the rail is `Left`, the operator MUST pass the `Left` through unchanged and MUST NOT invoke the delegate.

#### Scenario: TryTap keeps the rail on Right when its delegate throws

- **WHEN** the rail is `Right(payload)` and `TryTap`'s delegate throws
- **THEN** the rail after the step is still `Right(payload)`
- **AND** no error reaches downstream operators

#### Scenario: TryTap keeps the rail on Right when its delegate returns Left

- **WHEN** the rail is `Right(payload)` and `TryTap`'s delegate returns `Left(err)`
- **THEN** the rail after the step is still `Right(payload)`
- **AND** the swallowed error does not reach downstream operators

#### Scenario: TryTap passes through an incoming Left

- **WHEN** the rail is `Left(error)` at the start of a `TryTap`
- **THEN** the rail after the step is `Left(error)`
- **AND** the delegate is not invoked

### Requirement: TryEffects Is a Best-Effort Grouped Side Effect

The `RailwayStepsBuilder.TryEffects(configure)` operator SHALL group multiple best-effort side effects. The nested builder is the same `EffectsBuilder` shape used by `Effects`, exposing only `Do`. Failures in an inner effect MUST NOT fail the rail and MUST NOT stop subsequent inner effects from running. When the rail is `Left`, the operator MUST pass through and no inner effects MUST execute.

#### Scenario: TryEffects runs every inner effect even when one fails

- **WHEN** the rail is `Right(payload)` and the second of three inner effects throws
- **THEN** all three effects are attempted in registration order
- **AND** the rail after the step is still `Right(payload)`

#### Scenario: TryEffects passes through an incoming Left

- **WHEN** the rail is `Left(error)` at the start of a `TryEffects`
- **THEN** the rail after the step is `Left(error)`
- **AND** none of the inner effects execute

### Requirement: Detach Fires Background Side Effects That Are Not Awaited

The `RailwayStepsBuilder.Detach(configure)` operator SHALL schedule one or more background side effects that run without blocking the rail. The nested builder MUST expose only `Do`. The operator MUST return to the next step immediately after scheduling the background work — the rail MUST NOT await the detached delegates. Exceptions thrown by a detached delegate MUST be swallowed. The rail's outcome MUST NOT depend on whether the detached work completed or failed.

When the rail is `Left` at the point `Detach` is reached, the detached delegates MUST NOT be scheduled.

The vNext `Detach` MUST NOT accept a `when:` condition parameter. Conditional detach MUST be expressed by wrapping `Detach` inside a `Branch`.

#### Scenario: Detach schedules background work and advances immediately

- **WHEN** the rail is `Right(payload)` and `Detach` is reached
- **THEN** the detached delegates are scheduled as background tasks
- **AND** the next operator runs before the detached tasks necessarily complete

#### Scenario: Detach swallows background exceptions

- **WHEN** a detached delegate throws
- **THEN** the rail's outcome is unaffected
- **AND** no exception propagates to the awaiting caller of the railway

#### Scenario: Detach is skipped on an incoming Left

- **WHEN** the rail is `Left(error)` at the start of a `Detach`
- **THEN** no detached delegates are scheduled
- **AND** the rail after the step is `Left(error)`

### Requirement: Finally Always Runs as a Lifecycle Effect

The `RailwayStepsBuilder.Finally(activity)` operator SHALL register a lifecycle effect that runs after the step-phase completes — on `Right`, on unrecovered `Left`, and on cancellation — with the last observed payload. `Finally` MUST NOT change the rail's outcome. Multiple `Finally` registrations MUST all run, in registration order, and a failure in one `Finally` activity MUST NOT prevent subsequent `Finally` activities from running.

`Finally` activities MUST NOT be treated as operators in the Either-flowing pipeline — they MUST run from a `try/finally` wrapper around the entire step-phase execution.

#### Scenario: Finally runs when the rail ends on Right

- **WHEN** the step-phase completes with `Right(payload)`
- **THEN** every registered `Finally` activity runs with `payload` in registration order
- **AND** the rail's final result is still `Right(payload)`

#### Scenario: Finally runs when the rail ends on Left

- **WHEN** the step-phase completes with `Left(err)`
- **THEN** every registered `Finally` activity runs with the last observed payload in registration order
- **AND** the rail's final result is still `Left(err)`

#### Scenario: Finally runs even when a prior Finally activity fails

- **WHEN** two `Finally` activities are registered and the first throws
- **THEN** the second `Finally` activity still runs
- **AND** the thrown exception does not propagate to the caller

### Requirement: Side-Effect Failure Policies Are Named at the Call Site

Each side-effect operator SHALL make its failure policy visible from the operator name alone. The reader MUST NOT need to inspect the delegate's body to learn whether a failure fails the rail.

| Operator      | Policy                                      |
|---------------|---------------------------------------------|
| `Tap`         | strict — failure fails the rail             |
| `Effects`     | strict — first inner failure fails the rail |
| `TryTap`      | best-effort — failure swallowed             |
| `TryEffects`  | best-effort — failures swallowed            |
| `Detach`      | best-effort, background — failure swallowed, not awaited |
| `Finally`     | lifecycle — always runs, never changes rail |

#### Scenario: Reader can distinguish policies without reading the delegate

- **WHEN** a railway contains `Tap(A)` followed by `TryTap(B)`
- **THEN** the reader can determine that a failure in `A` fails the rail and a failure in `B` does not
- **AND** no delegate inspection is required to make that distinction

### Requirement: Removed Legacy Side-Effect-Adjacent Operators

The following operators from the 3.5 `RailwayStepsBuilder` SHALL be removed and MUST NOT be present on the vNext builder:

- `Parallel(...)` — removed without a direct replacement. Callers that need fan-out SHOULD use `Task.WhenAll` inside a single `Do`.
- `ParallelDetached(...)` — removed without a direct replacement. Callers that need background fan-out SHOULD use a `Detach` that schedules parallel work inside its delegates.
- `WithContext(...)` — removed. Callers that relied on local state SHOULD extend `TPayload` with the state.

No `[Obsolete]` shims are provided for any of the above.

#### Scenario: Parallel is no longer callable

- **WHEN** a caller writes `.Parallel(...)` on `RailwayStepsBuilder`
- **THEN** the code fails to compile
- **AND** the compile error surfaces because the method does not exist on the new builder

#### Scenario: ParallelDetached is no longer callable

- **WHEN** a caller writes `.ParallelDetached(...)` on `RailwayStepsBuilder`
- **THEN** the code fails to compile
- **AND** the compile error surfaces because the method does not exist on the new builder

#### Scenario: WithContext is no longer callable

- **WHEN** a caller writes `.WithContext(...)` on `RailwayStepsBuilder`
- **THEN** the code fails to compile
- **AND** the compile error surfaces because the method does not exist on the new builder
