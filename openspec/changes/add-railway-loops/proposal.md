## Why

Bee railways currently express linear progression and conditional branches, but have no first-class operator for iterative work — retrying a step until a condition holds, polling for readiness, or attempting a fallible operation with mutated inputs across attempts. Authors today fake this with recursion, external `while` loops outside the railway, or by wrapping `Do` in custom retry helpers, all of which leave the iteration intent invisible to the railway DSL and break the "railway reads like a narrative" property that `railway-vnext` is establishing.

## What Changes

- Add `Loop(body, until:, maxAttempts:, mutate:)` operator to `RailwayStepsBuilder` for first-class bounded iteration on the right rail.
- Loop body is a nested right-side sub-pipeline (`LoopBuilder<TPayload, TError>`) that supports `Do`, `Tap`, `Effects`, `TryTap`, `TryEffects`, `Branch`, `Ensure`, `Recover` — the same operators allowed inside `Branch`. `Detach`, `Finally`, and nested `Loop` are out of scope for the first iteration.
- `until` is the break condition evaluated against the payload after each iteration; when `true`, the loop exits successfully on the `Right` rail.
- `maxAttempts` is a required safety bound (positive integer). When exhausted without `until` becoming `true`, the loop produces a `Left` with a `LoopExhaustedError` (or library-defined equivalent) carrying the last payload and attempt count.
- `mutate` is an optional payload transform applied between iterations to change inputs for the next attempt ("when still not working, change something in hopes it works now"). It runs after the body produces a `Right` and `until` is `false`, before the next iteration starts. `mutate` MUST NOT fail the rail.
- A `Left` produced inside the loop body short-circuits the loop (consistent with `Branch`); the loop returns that `Left`. Recovery inside the body via `Recover<TErr>` can convert it back to `Right` and continue iterating.
- Sync + async overloads for `until` and `mutate`, mirroring existing operators.
- Update `Zooper.Bee.Example` with a representative loop use case (poll-for-ready or retry-with-mutation).
- Update `CHANGELOG.md` `## [Unreleased]` entry.

## Capabilities

### New Capabilities

- `railway-loops`: The `Loop` operator and its semantics — break condition (`until`), bounded attempts (`maxAttempts`), inter-iteration mutation (`mutate`), allowed nested operators, and exhaustion / short-circuit behavior.

### Modified Capabilities

- `railway-step-operators`: Add `Loop` to the registered operator set and the "registration order is preserved" guarantee. (Capability is introduced by the pending `railway-vnext` change; this delta layers on top.)

## Impact

- **Package `Zooper.Bee`**: New `LoopBuilder<TPayload, TError>` type. New `Loop(...)` overloads on `RailwayStepsBuilder<...>`. New `LoopExhaustedError` (or analogous) surfaced when `maxAttempts` is hit. Loop execution wired into the step executor in the same registration-order regime as `Branch`.
- **Package `Zooper.Bee.Tests`**: New test suite covering: body executes once when `until` is immediately true; iterates up to `maxAttempts` and exits with `Left` on exhaustion; `mutate` runs between iterations only; `Left` inside body short-circuits; `Recover` inside body keeps the loop iterating; async overloads await correctly.
- **Package `Zooper.Bee.Example`**: New sample railway using `Loop` (poll-until-ready or retry-with-mutation).
- **Docs**: `CHANGELOG.md` (`Unreleased`) entry. `README.md` operator table updated to include `Loop`.
- **Dependency**: this change layers on `railway-vnext`. If `railway-vnext` has not archived to `openspec/specs/` by the time this change is implemented, the modified-capability delta for `railway-step-operators` will be merged at archive time.
- **No breaking changes**: `Loop` is purely additive to the vNext operator set.
