## ADDED Requirements

### Requirement: Do Operator Progresses the Right Rail

The `RailwayStepsBuilder.Do(...)` operator SHALL be the sole operator for right-side payload progression. When the rail is currently `Right`, the delegate MUST execute with the current payload and its returned `Either<TError, TPayload>` MUST replace the rail state. When the rail is currently `Left`, the operator MUST pass the `Left` through unchanged without invoking the delegate.

The builder SHALL expose both synchronous and asynchronous overloads:
- `Do(Func<TPayload, Either<TError, TPayload>>)`
- `Do(Func<TPayload, CancellationToken, Task<Either<TError, TPayload>>>)`

#### Scenario: Do transforms the payload on the right rail

- **WHEN** the rail is `Right(payloadA)` and `Do` receives a delegate that returns `Right(payloadB)`
- **THEN** the rail after the step is `Right(payloadB)`
- **AND** the delegate is invoked exactly once

#### Scenario: Do fails the rail when its delegate returns Left

- **WHEN** the rail is `Right(payload)` and `Do` receives a delegate that returns `Left(error)`
- **THEN** the rail after the step is `Left(error)`
- **AND** subsequent non-recovery operators do not invoke their delegates

#### Scenario: Do passes through an incoming Left

- **WHEN** the rail is `Left(error)` and a `Do` step follows
- **THEN** the rail after the step is still `Left(error)`
- **AND** the `Do` delegate is not invoked

#### Scenario: Do executes asynchronously when given an async delegate

- **WHEN** `Do` receives an async delegate and the rail is `Right(payload)`
- **THEN** the executor awaits the delegate before advancing to the next operator
- **AND** the delegate receives the current `CancellationToken`

### Requirement: Branch Runs a Conditional Right-Side Sub-Pipeline

The `RailwayStepsBuilder.Branch(when, branch)` operator SHALL conditionally execute a nested right-side pipeline. The nested builder (`BranchBuilder<TPayload, TError>`) MUST expose `Do`, `Tap`, `Effects`, `TryTap`, `TryEffects`, `Recover`, and `Ensure`, and MUST NOT expose `Branch`, `Detach`, or `Finally`.

When the rail is `Right` and `when(payload)` returns `true`, the branch body MUST execute and its final `Either` MUST become the rail state. When the rail is `Right` and `when(payload)` returns `false`, the rail MUST continue unchanged. When the rail is `Left`, the branch MUST be skipped entirely and `when` MUST NOT be evaluated.

#### Scenario: Branch executes when the condition holds

- **WHEN** the rail is `Right(payloadA)` and `when(payloadA)` returns `true`
- **THEN** the branch body executes
- **AND** the rail after the branch is the final state produced by the branch body

#### Scenario: Branch is skipped when the condition is false

- **WHEN** the rail is `Right(payload)` and `when(payload)` returns `false`
- **THEN** the rail after the branch is still `Right(payload)`
- **AND** none of the branch body's delegates are invoked

#### Scenario: Branch is skipped on an incoming Left

- **WHEN** the rail is `Left(error)` at the start of a `Branch`
- **THEN** the rail after the branch is `Left(error)`
- **AND** `when` is not evaluated
- **AND** none of the branch body's delegates are invoked

#### Scenario: Branch body propagates its own failure

- **WHEN** the branch body's inner `Do` returns `Left(error)`
- **THEN** the rail after the branch is `Left(error)`
- **AND** subsequent operators in the branch body do not execute

#### Scenario: Branch body can recover its own errors

- **WHEN** the branch body contains `Recover<TErr>` after a failing step
- **AND** the failing step produces `Left(err)` where `err` is assignable to `TErr`
- **THEN** the branch completes with a `Right` produced by the recovery handler
- **AND** the rail after the branch is that `Right`

### Requirement: Ensure Converts Right to Left on Rule Failure

The `RailwayStepsBuilder.Ensure(when, failWith)` operator SHALL enforce a business rule against the current payload. `Ensure` MUST NOT mutate the payload and MUST only ever produce `Left`.

When the rail is `Right(payload)` and `when(payload)` returns `true`, the rail MUST transition to `Left(failWith(payload))`. When the rail is `Right(payload)` and `when(payload)` returns `false`, the rail MUST remain `Right(payload)`. When the rail is `Left`, the operator MUST pass through and neither `when` nor `failWith` MUST be evaluated.

#### Scenario: Ensure fails when the rule condition holds

- **WHEN** the rail is `Right(payload)` and `when(payload)` returns `true`
- **THEN** the rail after the step is `Left(failWith(payload))`
- **AND** `failWith` is invoked exactly once

#### Scenario: Ensure leaves the rail unchanged when the rule holds

- **WHEN** the rail is `Right(payload)` and `when(payload)` returns `false`
- **THEN** the rail after the step is still `Right(payload)`
- **AND** `failWith` is not invoked

#### Scenario: Ensure passes through an incoming Left

- **WHEN** the rail is `Left(error)` at the start of an `Ensure`
- **THEN** the rail after the step is `Left(error)`
- **AND** neither `when` nor `failWith` is invoked

### Requirement: Recover Converts Selected Left Cases Back to Right

The `RailwayStepsBuilder.Recover<TErr>(handler)` operator SHALL convert a matching `Left` back into a `Right`. Matching MUST be determined by whether the runtime type of the current `Left` value is assignable to `TErr`. When the rail is `Left(err)` and `err is TErr`, the handler MUST run with both the error and the pre-failure payload and its returned `TPayload` MUST become the rail's new `Right`. When the rail is `Left(err)` and `err is not TErr`, the operator MUST pass the `Left` through unchanged. When the rail is `Right`, the operator MUST pass through and the handler MUST NOT run.

`Recover` MUST only catch errors produced by operators registered *before* it. Errors produced after a `Recover` MUST NOT be handled by that earlier `Recover`.

The handler signature MUST be one of:
- `Func<TErr, TPayload, TPayload>` (synchronous)
- `Func<TErr, TPayload, CancellationToken, Task<TPayload>>` (asynchronous)

The handler MUST NOT return `Either<TError, TPayload>` — recovery cannot itself fail the rail. A caller that needs a fallible recovery MUST follow `Recover` with a `Do` or `Ensure` that expresses the secondary failure.

#### Scenario: Recover catches a matching Left

- **WHEN** a prior `Do` produced `Left(TimeoutError)` and a `Recover<TimeoutError>` follows
- **THEN** the recovery handler is invoked with the `TimeoutError` and the pre-failure payload
- **AND** the rail after `Recover` is `Right` holding the handler's returned payload

#### Scenario: Recover ignores a non-matching Left

- **WHEN** the rail is `Left(NotFoundError)` at the start of a `Recover<TimeoutError>`
- **THEN** the rail after the step is still `Left(NotFoundError)`
- **AND** the recovery handler is not invoked

#### Scenario: Recover passes through on the Right rail

- **WHEN** the rail is `Right(payload)` at the start of a `Recover<TimeoutError>`
- **THEN** the rail after the step is still `Right(payload)`
- **AND** the recovery handler is not invoked

#### Scenario: Recover does not catch errors from later operators

- **WHEN** `Recover<TimeoutError>` is registered before a `Do` that returns `Left(TimeoutError)`
- **THEN** the rail after the pipeline is `Left(TimeoutError)`
- **AND** the earlier `Recover`'s handler is not invoked

#### Scenario: Recover handler receives the pre-failure payload

- **WHEN** a `Do` that receives `Right(payloadA)` returns `Left(err)`
- **AND** a later `Recover` catches the error
- **THEN** the handler receives `payloadA` (the payload as it was before the failing `Do` was invoked)

### Requirement: Step Execution Preserves Registration Order

The `RailwayStepsBuilder` SHALL execute every registered operator in the exact order it was added. This MUST hold uniformly for `Do`, `Tap`, `Effects`, `TryTap`, `TryEffects`, `Detach`, `Branch`, `Ensure`, and `Recover`. No operator category may be reordered relative to another.

#### Scenario: Operators of different categories run in registration order

- **WHEN** a railway registers `Do(a)`, `Tap(b)`, `Branch(…)`, `Ensure(…)`, `Do(c)` in that order
- **THEN** at runtime the operators execute in exactly that order
- **AND** no category is hoisted, deferred, or reordered

### Requirement: Left Semantics — Boundary Failure, Not Domain Branching

A railway SHALL use `Left` only to express that the current boundary could not produce a valid answer (for example, unreachable dependencies, timeouts, or unmet preconditions). `Left` MUST NOT be used as a branching mechanism for valid domain states. Domain outcomes that the business considers valid — including shapes such as `NotFound` or "resource is in pending state" — MUST be represented on the `Right` rail, typically by variant properties on the payload.

This requirement constrains *guidance* rather than *runtime behavior*: the builder MUST NOT reject a `Left(NotFoundError)` at runtime, but the capability's documentation, samples, and design reviews MUST uphold this semantic.

#### Scenario: Valid domain state is represented on Right

- **WHEN** a `Do` loads a resource that is legitimately absent according to the domain
- **THEN** the recommended pattern is to return `Right(payload.WithResourceAbsent())` rather than `Left(NotFoundError)`

#### Scenario: Boundary failure is represented on Left

- **WHEN** a `Do` calls an external service and the call times out
- **THEN** the recommended pattern is to return `Left(TimeoutError)` so that a `Recover<TimeoutError>` (or the rail's terminal failure) can handle it

### Requirement: Removed Legacy Step Operators

The following operators from the 3.5 `RailwayStepsBuilder` SHALL be removed and MUST NOT be present on the vNext builder:

- `Group(...)` — replaced by `Branch(when, branch)` for the conditional case and by an inline sequence of `Do` / `Tap` calls for the unconditional case.
- `DoIf(condition, activity)` — replaced by `Branch(when: condition, branch: b => b.Do(activity))`.
- `DoAll(params activities)` — replaced by multiple `Do(...)` calls.

No `[Obsolete]` shim is provided. Attempting to use any of these operators MUST produce a compile error referencing the replacement.

#### Scenario: Group is no longer callable

- **WHEN** a caller writes `.Group(...)` on `RailwayStepsBuilder`
- **THEN** the code fails to compile
- **AND** the compile error surfaces because the method does not exist on the new builder

#### Scenario: DoIf is no longer callable

- **WHEN** a caller writes `.DoIf(condition, activity)` on `RailwayStepsBuilder`
- **THEN** the code fails to compile
- **AND** the compile error surfaces because the method does not exist on the new builder

#### Scenario: DoAll is no longer callable

- **WHEN** a caller writes `.DoAll(a, b, c)` on `RailwayStepsBuilder`
- **THEN** the code fails to compile
- **AND** the compile error surfaces because the method does not exist on the new builder
