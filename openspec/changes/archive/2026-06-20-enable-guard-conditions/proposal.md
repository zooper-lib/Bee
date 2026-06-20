## Why

Railway guards always execute — there is no way to make a group of guards apply only in certain cases. Steps support a conditional sub-pipeline via `.Branch(when, ...)`, but the guard builder has no equivalent, forcing users to fold conditionals inside each guard body or split into multiple railways. A guard conditional operator lets callers register a group of guards that only run when a predicate over the request is true.

Separately, `.Branch` is a mild misnomer: the operator has no else path — it conditionally *includes* a group, it does not fork. We adopt `.When` as the canonical name for both the new guard operator and the existing step operator, with `.Branch` kept as a deprecated alias on steps.

> **Dependency:** a separate, prior change splits validations out of the guard builder into their own method group. This change assumes that split has landed — `.When` here gates **guards only** and does not touch validations.

## What Changes

- Add a `When(condition, configure)` operator to `RailwayGuardBuilder`. It opens a nested sub-builder that registers a group of guards gated by a condition over `TRequest`.
- The nested sub-builder exposes the guard surface — `Guard` (sync + async bodies). Validations are out of scope (their own method group after the prior split).
- The `condition` supports **both** sync (`Func<TRequest, bool>`) and async (`Func<TRequest, CancellationToken, Task<bool>>`) forms.
- When the condition is `false`, the entire group of guards is skipped (they do not run); the rail continues unchanged. When `true`, the group's guards run and any failure short-circuits the rail as usual.
- **Steps**: rename `RailwayStepsBuilder.Branch(when, configure)` to `When(condition, configure)`. Keep `Branch` as an `[Obsolete]` alias that forwards to `When` for one major version. **BREAKING (soft)** — `Branch` still compiles but emits a deprecation warning.
- Existing unconditional `Guard(...)` overloads remain unchanged.

## Capabilities

### New Capabilities
- `railway-guard-conditions`: Conditional execution of a group of guards via `When`, gated by a sync-or-async predicate over the request, with skip-the-whole-group semantics.

### Modified Capabilities
<!-- No existing spec files under openspec/specs/ (specs dir is empty), so the steps-operator rename is captured in this change's delta specs/design rather than a modified capability. -->

## Impact

- `Zooper.Bee/RailwayGuardBuilder.cs`: new `When(condition, configure)` overloads (sync + async condition); nested sub-builder reuse.
- `Zooper.Bee/Internal/RailwayGuard.cs`: carry an optional async condition; evaluate before the check.
- `Zooper.Bee/RailwayStepsBuilder.cs`: rename `Branch` → `When`; add `[Obsolete]` `Branch` alias; skip conditional guards in `RunGuardsAsync`.
- Docs: `README.md` / `RAILWAY-VNEXT-SUMMARY.md` — update `.Branch` references to `.When`.
- Tests: `Zooper.Bee.Tests` — coverage for guard `When` run/skip, sync + async condition, nested groups, failure short-circuit, and the steps `Branch` obsolete alias still working.
- Public API: additive guard surface plus a soft-deprecated step method (`Branch`).
- **Depends on** the prior validation-split change.
