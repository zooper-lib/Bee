## 1. Executor Foundation

- [ ] 1.1 Define the operator transformer type (`Func<Either<TError, TPayload>, CancellationToken, Task<Either<TError, TPayload>>>`) in `Zooper.Bee.Internal`
- [ ] 1.2 Add a payload-snapshot mechanism so each operator sees the last `Right` payload and a failing operator can report `Left(err, preCallPayload)`
- [ ] 1.3 Rewrite `RailwayStepsBuilder.ExecuteRailwayAsync` to thread the current `Either` through the registered operators without short-circuiting at the executor level
- [ ] 1.4 Keep `Finally` activities out of the transformer list; wrap step-phase execution in a `try/finally` that runs them in registration order with the last observed payload
- [ ] 1.5 Make every `Finally` activity resilient: a thrown exception in one MUST NOT prevent subsequent `Finally` activities from running

## 2. Right-Rail Operators

- [ ] 2.1 Implement `Do` (sync + async overloads) as a transformer that acts on `Right`, passes `Left` through, and replaces rail state with the delegate's `Either`
- [ ] 2.2 Implement `Ensure(when, failWith)` as a transformer that only ever produces `Left`; passes both `Right` (when `when` is false) and `Left` through; never invokes `failWith` on an incoming `Left`
- [ ] 2.3 Implement `Branch(when, branch)` with a dedicated `BranchBuilder<TPayload, TError>`; expose `Do`, `Tap`, `Effects`, `TryTap`, `TryEffects`, `Recover`, `Ensure` on the branch builder and omit `Branch`, `Detach`, `Finally`
- [ ] 2.4 Ensure `Branch` skips both `when` evaluation and body execution on an incoming `Left`
- [ ] 2.5 Verify `Branch` body errors can be caught by a `Recover` registered inside the same branch (branch-local recovery scope)

## 3. Side-Effect Operators

- [ ] 3.1 Implement `Tap` with three overloads — `Action<TPayload>`, `Func<TPayload, CancellationToken, Task>`, and `Func<TPayload, CancellationToken, Task<Either<TError, Unit>>>` — none of which change the payload
- [ ] 3.2 Decide and document the throwing-sync-`Tap` policy (rethrow to caller vs. wrap as `Left`); implement accordingly and reflect in `Tap` XML docs
- [ ] 3.3 Build `EffectsBuilder<TPayload, TError>` exposing only `Do` (effect-shaped, no payload return); reuse this type for both `Effects` and `TryEffects`
- [ ] 3.4 Implement `Effects(configure)` — strict grouped side effects; first inner failure produces `Left`, remaining inner effects do not run
- [ ] 3.5 Implement `TryEffects(configure)` — best-effort grouped side effects; failures swallowed, every inner effect attempted in registration order, rail remains `Right`
- [ ] 3.6 Implement `TryTap` — best-effort single side effect that swallows exceptions and `Left(err)` returns, keeping the rail on `Right`
- [ ] 3.7 Implement the new `Detach(configure)` using the same inner-builder shape as `Effects` (only `Do`); schedule delegates as background tasks without awaiting them; swallow exceptions
- [ ] 3.8 Make all side-effect operators pass `Left` through unchanged when the rail is already on `Left`

## 4. Recovery Operator

- [ ] 4.1 Implement `Recover<TErr>(Func<TErr, TPayload, TPayload>)` — synchronous handler
- [ ] 4.2 Implement `Recover<TErr>(Func<TErr, TPayload, CancellationToken, Task<TPayload>>)` — asynchronous handler
- [ ] 4.3 Match `Left` values via runtime `is`-check against `TErr`; non-matching `Left` values MUST pass through untouched
- [ ] 4.4 Pass the pre-failure payload snapshot (from 1.2) into the recovery handler, not `default`
- [ ] 4.5 Ensure a `Recover` registered at position N cannot catch errors produced by operators at positions > N

## 5. Remove Legacy Operators

- [ ] 5.1 Remove `Group(...)` from `RailwayStepsBuilder` and its supporting `Features.Group` types used exclusively by this operator
- [ ] 5.2 Remove `DoIf(...)` from `RailwayStepsBuilder`
- [ ] 5.3 Remove `DoAll(...)` from `RailwayStepsBuilder`
- [ ] 5.4 Remove `Parallel(...)` and `ParallelDetached(...)` from `RailwayStepsBuilder` and drop their supporting feature types if no other consumer remains
- [ ] 5.5 Remove `WithContext(...)` from `RailwayStepsBuilder` and drop the `Features.Context` plumbing if no longer used
- [ ] 5.6 Remove the 3.5 `Detach(Func<TPayload, bool>?, Action<DetachedBuilder>)` overload in favor of the new `Detach(configure)`
- [ ] 5.7 Confirm the obsolete `RailwayBuilder` (pre-3.5) is untouched or updated consistently — decide whether to leave its vocabulary as-is since it is already `[Obsolete]`, or delete it entirely for vNext

## 6. Tests

- [ ] 6.1 Port or delete 3.5 `RailwayStepsBuilder` tests that referenced `Group`, `DoIf`, `DoAll`, `Parallel`, `ParallelDetached`, `WithContext`
- [ ] 6.2 Add `Do` tests: sync/async, `Right`-transform, `Left`-produce, `Left`-passthrough, cancellation token propagation
- [ ] 6.3 Add `Tap` tests: all three overloads, no-payload-change, strict failure, `Left`-passthrough
- [ ] 6.4 Add `Effects` tests: order of inner effects, first-failure-stops, `Left`-passthrough, no inner-effect execution on incoming `Left`
- [ ] 6.5 Add `TryTap` and `TryEffects` tests: swallowed-throw, swallowed-`Left`, best-effort ordering, `Left`-passthrough
- [ ] 6.6 Add `Detach` tests: non-blocking scheduling, swallowed exceptions, rail outcome independent of detached completion, `Left`-skip
- [ ] 6.7 Add `Finally` tests: runs on `Right`, runs on `Left`, runs on cancellation, multiple `Finally` activities in order, one failing activity does not block the rest
- [ ] 6.8 Add `Branch` tests: condition-true runs body, condition-false passes through, `Left`-skips-both-condition-and-body, inner failure propagates, inner `Recover` catches within branch
- [ ] 6.9 Add `Ensure` tests: produces `Left` on condition-true, passes `Right` through on condition-false, passes `Left` through unchanged
- [ ] 6.10 Add `Recover` tests: type-matching, non-matching passthrough, `Right`-passthrough, pre-failure-payload fidelity, cannot-catch-later-errors, sync and async handlers
- [ ] 6.11 Add registration-order tests covering interleaved operators from every category
- [ ] 6.12 Add a readability-pass test: build the full example railway from the vNext summary end-to-end and assert success path + each failure/branch path

## 7. Example Project

- [ ] 7.1 Rewrite every railway in `Zooper.Bee.Example` in the new vocabulary; remove any use of `Group`, `DoIf`, `DoAll`, `Parallel`, `ParallelDetached`, `WithContext`
- [ ] 7.2 Add at least one `Recover` + `Ensure` + `Branch` example so the example covers the non-trivial operators
- [ ] 7.3 Verify the example project builds and runs against the new `Zooper.Bee`

## 8. MediatR Package Alignment

- [ ] 8.1 Audit `Zooper.Bee.MediatR` for any references to removed operators; migrate them to the new vocabulary
- [ ] 8.2 Ensure `Zooper.Bee.MediatR` builds against the new `Zooper.Bee` and its tests pass

## 9. Docs and Changelog

- [ ] 9.1 Update `README.md` usage examples to the new operator vocabulary
- [ ] 9.2 Add a `## [Unreleased]` section to `CHANGELOG.md` with `### Added`, `### Changed`, `### Removed`, `### Migration` subsections
- [ ] 9.3 `Added`: list every new operator (`Tap`, `Effects`, `TryTap`, `TryEffects`, `Recover`, `Branch`, `Ensure`, updated `Detach`, updated `Finally`) with a one-line purpose each
- [ ] 9.4 `Changed`: describe the executor rewrite to an Either-flowing pipeline and the new `Left`/`Right` semantics guidance
- [ ] 9.5 `Removed`: call out `Group`, `DoIf`, `DoAll`, `Parallel`, `ParallelDetached`, `WithContext`, and the 3.5 conditional `Detach` overload as BREAKING
- [ ] 9.6 `Migration`: give a copy-pasteable recipe for each removed operator (`Group` → `Branch`, `DoIf` → `Branch`, `DoAll` → multiple `Do`, `Parallel` → `Task.WhenAll` in a `Do`, `ParallelDetached` → `Detach` with parallel work inside, `WithContext` → extend `TPayload`)
- [ ] 9.7 Add XML documentation on every new operator method describing its Right-rail behaviour, Left-rail behaviour, and failure policy
- [ ] 9.8 Bump the package version to `4.0.0` in `Directory.Build.props` (or the relevant version source of truth) and note the bump in the changelog entry

## 10. Release Readiness

- [ ] 10.1 Run the full test suite (`Zooper.Bee.Tests` + `Zooper.Bee.MediatR` tests) and confirm green
- [ ] 10.2 Build `Zooper.Bee`, `Zooper.Bee.MediatR`, and `Zooper.Bee.Example` in Release configuration
- [ ] 10.3 Verify there are no remaining `[Obsolete]` references pointing at vNext-removed members
- [ ] 10.4 Do a final read-through of the example railway to confirm it reads as a narrative — this is the qualitative success metric from `design.md`
