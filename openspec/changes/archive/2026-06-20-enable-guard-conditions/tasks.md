## 1. Internal: conditional guard model

- [x] 1.1 Add a nullable `Func<TRequest, CancellationToken, Task<bool>>? When` to `RailwayGuard<TRequest, TError>` (default `null` = always run); expose it for the runner to read.
- [x] 1.2 Update the `RailwayGuard` constructor(s) to accept the optional condition.

## 2. Guard builder `When` operator

- [x] 2.1 Add `When(Func<TRequest, bool> condition, Action<RailwayGuardBuilder<...>> configure)` to `RailwayGuardBuilder`, normalizing the sync condition to `(req, _) => Task.FromResult(condition(req))`.
- [x] 2.2 Add the async overload `When(Func<TRequest, CancellationToken, Task<bool>> condition, Action<RailwayGuardBuilder<...>> configure)`.
- [x] 2.3 Implement `When` by creating a fresh nested `RailwayGuardBuilder`, running `configure`, then flattening each nested guard into the parent `Guards` list with the condition attached.
- [x] 2.4 Compose nested conditions with short-circuiting logical AND (outer `&&` inner) when a nested guard already carries a condition.

## 3. Runner: gate guards

- [x] 3.1 In `RailwayStepsBuilder.RunGuardsAsync`, before calling `guard.Check`, evaluate the guard's condition; if non-null and awaited result is `false`, skip the guard (continue, no short-circuit).
- [x] 3.2 Confirm an unconditional guard (`When == null`) still runs exactly as before.

## 4. Step operator rename

- [x] 4.1 Rename `RailwayStepsBuilder.Branch(when, configure)` to `When(condition, configure)`; move the implementation. (Also renamed the analogous `LoopBuilder.Branch` for consistency.)
- [x] 4.2 Re-add `Branch(when, configure)` as `[Obsolete("Use When instead. Branch will be removed in the next major version.")]` forwarding to `When` (on both `RailwayStepsBuilder` and `LoopBuilder`).

## 5. Tests

- [x] 5.1 Guard `When` with sync condition `true` → group guards run; a failing guard short-circuits.
- [x] 5.2 Guard `When` with condition `false` → group guards skipped, rail continues (skip-as-pass).
- [x] 5.3 Guard `When` with async condition (`true`/`false` paths), condition awaited.
- [x] 5.4 Nested `When` groups: outer `false` skips nested guards and does not evaluate inner condition; both `true` runs nested guards.
- [x] 5.5 Unconditional `Guard(...)` outside any `When` still runs on every execution.
- [x] 5.6 Step `When` runs sub-pipeline conditionally; deprecated `Branch` alias behaves identically (and is marked obsolete).

## 6. Docs

- [x] 6.1 Update `README.md`, `RAILWAY-VNEXT-SUMMARY.md`, and `CHANGELOG.md`: document guard `When`; replace `.Branch` references with `.When`, noting `Branch` is deprecated.

## 7. Verify

- [x] 7.1 Build the solution; confirm only the intended `Branch` obsolete warning appears (no other warnings/errors).
- [x] 7.2 Run `Zooper.Bee.Tests`; confirm all pass.
