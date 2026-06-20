## Context

Railway guards run before any step and operate only on `TRequest` (the payload is not yet built). They live on `RailwayGuardBuilder` as a `Guards` list (`RailwayGuard`, returns `Either<TError,Unit>`); each guard aborts the rail on failure. `RunGuardsAsync` in `RailwayStepsBuilder` runs all guards.

> **Dependency:** a prior change moves validations out of `RailwayGuardBuilder` into their own method group. This design assumes that landed — guards and validations are separate, and `.When` here gates **guards only**.

Step operators already offer `.Branch(when, configure)` — a predicate plus a nested sub-builder whose grouped operators run only when the predicate is true. The guard builder has no equivalent. `.Branch` is also a misnomer (no else path — it conditionally includes, does not fork), so this change adopts `.When` as the canonical name for both the guard operator and the step operator. See `proposal.md` for motivation.

## Goals / Non-Goals

**Goals:**
- Add `When(condition, configure)` to `RailwayGuardBuilder`.
- Nested sub-builder exposes the guard surface: `Guard` (sync + async bodies).
- Condition over `TRequest` in both sync (`Func<TRequest,bool>`) and async (`Func<TRequest,CancellationToken,Task<bool>>`) forms.
- Condition `false` skips the entire group of guards; rail continues. Condition `true` runs the group; any failure short-circuits as usual.
- Rename step operator `Branch` → `When`; keep `Branch` as an `[Obsolete]` forwarding alias.

**Non-Goals:**
- Conditions over payload (no payload at the guard phase).
- Conditional validations — validations are a separate method group (prior split); out of scope here.
- Reusable/DI-registered named condition objects.
- Extending the standalone `Guard(...)` overloads with a `condition` parameter (the conditional surface is `When`, not per-method overloads).
- Removing the `Branch` alias in this change (removal is deferred to the next major version).

## Decisions

**1. Store the condition as a single async predicate; wrap the sync form.**
Normalize both overloads to `Func<TRequest, CancellationToken, Task<bool>>`. The sync `Func<TRequest,bool> when` is adapted via `(req, _) => Task.FromResult(when(req))`. One internal representation, no duplicated execution logic.

**2. Carry an optional condition on each guard.**
Add a nullable `Func<TRequest, CancellationToken, Task<bool>>? when` to `RailwayGuard` (default `null` = always run). `null` preserves existing behavior for unconditional guards.
- *Alternative considered:* a first-class group object holding one condition + its own guard list, evaluated once. Rejected for now — the per-guard condition keeps the `Guards` list flat and `RunGuardsAsync` structurally unchanged. (See the per-guard re-evaluation trade-off below.)

**3. `When` reuses `RailwayGuardBuilder` as the nested sub-builder and flattens.**
`When(condition, configure)` creates a fresh `RailwayGuardBuilder`, runs `configure`, then appends each nested guard to the parent `Guards` list with the condition attached. Nested groups compose: if a nested guard already carries a condition `c`, the effective condition becomes `req,ct => await outer(req,ct) && await c(req,ct)` (logical AND, short-circuiting). Flattening means `RunGuardsAsync` needs no structural change.

**4. Gate in `RunGuardsAsync`, skip-as-pass.**
Before invoking a guard's `Check`, if `when != null` and `await when(request, ct)` is `false`, skip it (continue; do not invoke the body, do not short-circuit). Two new overloads on `RailwayGuardBuilder`:
```csharp
When(Func<TRequest,bool> condition, Action<RailwayGuardBuilder<...>> configure)                      // sync condition
When(Func<TRequest,CancellationToken,Task<bool>> condition, Action<RailwayGuardBuilder<...>> configure) // async condition
```

**5. Rename step `Branch` → `When`; keep `Branch` as an `[Obsolete]` alias.**
`RailwayStepsBuilder.When(condition, configure)` becomes the implementation; `Branch` is retained as `[Obsolete("Use When instead. Branch will be removed in the next major version.")]` and forwards to `When`. Existing callers compile with a deprecation warning. Removal deferred to the next major version.
- *Alternative considered:* hard rename with no alias. Rejected — needless break for existing `Branch` users when a forwarding alias costs nothing.

## Risks / Trade-offs

- [Condition re-evaluated per guard] → A group with N guards evaluates its condition N times, since the condition is attached per guard rather than to a single group unit. Mitigation: document it; keep predicates pure and cheap; for an expensive/IO condition guarding many checks, gate inside a single `Guard` body instead.
- [Async condition throws] → Propagates as an unhandled exception, same as a throwing step predicate. Not caught.
- [Nested-group condition composition] → AND-composition must short-circuit so an inner condition isn't evaluated when the outer is false. Covered by test.
- [Depends on the validation-split change] → If that change has not landed, `RailwayGuardBuilder` still exposes `Validate` and the nested sub-builder would leak it. Mitigation: sequence this change after the split; the nested builder must expose `Guard` only.
