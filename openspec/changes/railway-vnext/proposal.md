## Why

Bee railways are hard to read because step semantics are implicit: `Do()` is used for business progression, side effects, and conditional branches alike, and `Group()` hides conditional sub-pipelines behind a generic name. Readers cannot tell from the call site whether a step changes the payload, performs telemetry, or alters control flow. Railway vNext makes step intent explicit at the DSL level so that a railway reads like a narrative of the use case.

## What Changes

- **BREAKING**: Replace the generic step vocabulary of the 3.5 `RailwayStepsBuilder` with an explicit operator set that distinguishes payload progression, side effects, recovery, branching, and rule enforcement.
- **BREAKING**: Remove top-level `Group()` from the steps builder. Conditional sub-pipelines must be expressed with `Branch(when:, branch:)`. There is no deprecation shim — `Group()` is replaced, not renamed.
- **BREAKING**: Remove `DoIf(...)` and `DoAll(...)` from the steps builder. `DoIf` is subsumed by `Branch`; `DoAll` has no analogue in the new vocabulary and is dropped.
- Add `Do(...)` as the sole operator for right-side payload progression (returns the next payload).
- Add `Tap(...)` for a single strict pass-through side effect (failure fails the railway, payload unchanged).
- Add `Effects(e => e.Do(...).Do(...))` for grouped strict pass-through side effects.
- Add `TryTap(...)` and `TryEffects(...)` for best-effort side effects whose failure does not fail the railway.
- Keep `Detach(...)` for background (not-awaited) best-effort side effects, with updated docs to fit the new vocabulary.
- Keep `Finally(...)` as the always-run lifecycle effect.
- Add `Recover<TError>((error, ctx) => ...)` as a first-class operator for converting selected `Left` cases back into `Right` outcomes.
- Add `Branch(when:, branch:)` for conditional right-side sub-pipelines; the branch returns the modified payload.
- Add `Ensure(when:, failWith:)` for right-side business-rule enforcement that converts `Right` into `Left`.
- Codify the meaning of `Left` and `Right`: `Right` = valid domain outcome (including outcomes like `NotFound` when the domain considers them valid); `Left` = the boundary could not produce a valid answer. `Left` must not be used as a branching mechanism for valid domain states.
- Update `CHANGELOG.md` with a `## [Unreleased]` entry describing the vNext API, the breaking removals, and the migration guidance.
- Update `README.md` examples and `Zooper.Bee.Example` to use the new operator vocabulary.

## Capabilities

### New Capabilities

- `railway-step-operators`: The right-side progression DSL for the post-guard step phase — `Do`, `Branch`, `Ensure`, `Recover`, and the `Left`/`Right` semantics that govern when each is appropriate.
- `railway-side-effects`: The side-effect DSL with explicit failure policies — `Tap`, `Effects`, `TryTap`, `TryEffects`, `Detach`, `Finally` — and the rules for which policy applies to which operator.

### Modified Capabilities

_None._ `openspec/specs/` is empty; the two capabilities above are the first specs in the repository, so there are no prior requirement documents to amend.

## Impact

- **Package `Zooper.Bee`**: `RailwayStepsBuilder<...>` API surface replaced. `Group`, `DoIf`, `DoAll` removed. New builder types for `Effects`, `TryEffects`, `Detach`, and `Branch` sub-pipelines. `IRailwayStep` and related interfaces re-evaluated against the new vocabulary.
- **Package `Zooper.Bee.MediatR`**: consumers that wire railways through MediatR may reference removed operators; expect compile breaks until call sites migrate.
- **Package `Zooper.Bee.Example`**: all example railways rewritten in the new vocabulary to serve as the reference for migration.
- **Package `Zooper.Bee.Tests`**: existing tests for `Group`, `DoIf`, `DoAll` removed or rewritten; new tests for `Tap`, `Effects`, `TryTap`, `TryEffects`, `Recover`, `Branch`, `Ensure`, and their failure-policy contracts.
- **Docs**: `CHANGELOG.md` (`Unreleased` section) and `README.md` updated. Migration notes added to the changelog call out the removal of `Group`/`DoIf`/`DoAll` and the mapping to new operators.
- **Version**: the next release is a major version bump (4.0.0) because the steps-builder API surface changes are not source-compatible.
- **Downstream consumers**: any project using `Railway.Create(...).steps(...)` with `Group`, `DoIf`, or `DoAll` must migrate before upgrading. The guard phase (`Guard`, `Validate`) is unchanged.
