## ADDED Requirements

### Requirement: Conditional guard group via `When`
The `RailwayGuardBuilder` SHALL expose a `When(condition, configure)` operator that registers a group of guards gated by a condition over the request. The `configure` action receives a nested guard sub-builder exposing the guard surface (`Guard`, sync and async bodies). The condition SHALL be evaluated against `TRequest` during the guard phase, before any step runs.

#### Scenario: Condition true runs the group's guards
- **WHEN** a railway is executed and a `When` group's condition returns `true`
- **THEN** every guard registered inside that group SHALL be evaluated as part of the guard phase

#### Scenario: Condition false skips the entire group
- **WHEN** a railway is executed and a `When` group's condition returns `false`
- **THEN** none of the guards inside that group SHALL be evaluated
- **AND** the rail SHALL continue unchanged as if those guards had passed

#### Scenario: A guard in a true group fails
- **WHEN** a `When` group's condition returns `true` and one of its guards returns `Left(error)`
- **THEN** the railway SHALL short-circuit and return that `error`

### Requirement: Sync and async condition forms
The `When` operator SHALL provide both a synchronous condition overload (`Func<TRequest, bool>`) and an asynchronous condition overload (`Func<TRequest, CancellationToken, Task<bool>>`). The synchronous form SHALL behave identically to the asynchronous form with the predicate result wrapped in a completed task.

#### Scenario: Synchronous condition
- **WHEN** a `When` group is registered with a `Func<TRequest, bool>` condition
- **THEN** the group's guards SHALL run only when that predicate returns `true`

#### Scenario: Asynchronous condition
- **WHEN** a `When` group is registered with a `Func<TRequest, CancellationToken, Task<bool>>` condition
- **THEN** the condition SHALL be awaited
- **AND** the group's guards SHALL run only when the awaited result is `true`

### Requirement: Nested groups compose conditions with logical AND
When a `When` group is nested inside another `When` group, a nested guard's effective condition SHALL be the logical AND of the outer and inner conditions, evaluated with short-circuiting so the inner condition is not evaluated when the outer condition is `false`.

#### Scenario: Outer condition false skips nested guards
- **WHEN** an outer `When` condition returns `false`
- **THEN** guards in a nested `When` group SHALL NOT run
- **AND** the nested group's condition SHALL NOT be evaluated

#### Scenario: Both conditions true runs nested guards
- **WHEN** both the outer and inner `When` conditions return `true`
- **THEN** the guards in the nested group SHALL be evaluated

### Requirement: Unconditional guards are unaffected
Guards registered directly via `Guard(...)` (outside any `When` group) SHALL continue to run unconditionally during the guard phase, with no behavior change.

#### Scenario: Plain guard still runs
- **WHEN** a guard is registered with `Guard(...)` and no enclosing `When` group
- **THEN** that guard SHALL be evaluated on every execution regardless of any `When` conditions elsewhere

### Requirement: Step operator renamed to `When` with deprecated `Branch` alias
The `RailwayStepsBuilder` conditional sub-pipeline operator SHALL be named `When(condition, configure)`. The former name `Branch` SHALL be retained as an `[Obsolete]` alias that forwards to `When`, preserving identical behavior, and SHALL emit a deprecation warning when used.

#### Scenario: `When` runs the step sub-pipeline conditionally
- **WHEN** a step `When` operator is configured with a condition and a sub-pipeline
- **THEN** the sub-pipeline SHALL run only when the condition returns `true`, with its result fed back into the rail

#### Scenario: Deprecated `Branch` alias still works
- **WHEN** existing code calls `Branch(when, configure)` on the step builder
- **THEN** it SHALL behave identically to `When(condition, configure)`
- **AND** the compiler SHALL emit an obsolete/deprecation warning
