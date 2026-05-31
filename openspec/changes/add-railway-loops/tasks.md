## 1. Loop Builder and Transformer

- [x] 1.1 Add `LoopBuilder<TPayload, TError>` class in `Zooper.Bee` exposing `Do`, `Tap`, `Effects`, `TryTap`, `TryEffects`, `Branch`, `Ensure`, `Recover<TErr>` — omit `Loop`, `Detach`, `Finally`
- [x] 1.2 Reuse the vNext Either-transformer compilation pipeline so a `LoopBuilder` compiles its operators into a single inner transformer chain (matching how `BranchBuilder` compiles)
- [x] 1.3 Add an internal `LoopOperator` (or equivalent) that wraps the compiled body and drives the iteration controller as a single outer transformer
- [x] 1.4 Validate `maxAttempts >= 1` at registration time; throw `ArgumentOutOfRangeException` with a clear message naming the parameter
- [x] 1.5 Make `mutate` optional (nullable delegate); `until` and `exhausted` are required

## 2. Loop Iteration Controller

- [x] 2.1 Implement iteration order from the spec: body → check `until` → check exhaustion → run `mutate` → next iteration
- [x] 2.2 Pass through incoming `Left` unchanged without invoking body, `until`, `mutate`, or `exhausted`
- [x] 2.3 Short-circuit on body `Left`: exit loop immediately with that `Left`, skip `until`, `mutate`, `exhausted`
- [x] 2.4 On `until == true`, exit with `Right(payload)` and skip both `mutate` and `exhausted`
- [x] 2.5 On reaching iteration `maxAttempts` with `until == false`, exit with `Left(exhausted(payload, maxAttempts))`; do not run `mutate` after the final iteration
- [x] 2.6 Use 1-indexed attempt numbers; pass `attempt` to `until`, `mutate`, and `exhausted` consistently
- [x] 2.7 Each iteration runs the compiled body chain end-to-end on a fresh `Right(state)` so inner `Recover` is scoped to that iteration only
- [x] 2.8 Thread `CancellationToken` into async overloads of `until`, `mutate`, and `exhausted`; let `OperationCanceledException` propagate from the body per existing executor behaviour

## 3. RailwayStepsBuilder API Surface

- [x] 3.1 Add the sync `Loop(...)` overload to `RailwayStepsBuilder<...>` with `Func<TPayload, int, bool> until`, `Func<TPayload, int, TError> exhausted`, optional `Func<TPayload, int, TPayload> mutate`
- [x] 3.2 Add the async `Loop(...)` overload with `Func<TPayload, int, CancellationToken, Task<bool>>`, `Func<TPayload, int, CancellationToken, Task<TError>>`, optional `Func<TPayload, int, CancellationToken, Task<TPayload>>`
- [x] 3.3 Allow mixed sync/async hooks by providing internal adapters (e.g. sync `until` lifted to a completed `Task<bool>`) so callers can combine them without overload explosion
- [x] 3.4 Add XML docs covering: right-rail behaviour, left-passthrough, iteration order, the role of each hook, that `mutate` cannot fail the rail, that exhaustion uses the caller's delegate
- [x] 3.5 Confirm `Loop` is NOT exposed on `BranchBuilder` or `LoopBuilder` (no nested loops, no loops inside branch bodies for v1)

## 4. Tests

- [x] 4.1 Body short-circuit: iteration 2 returns `Left(err)` → loop exits `Left(err)`, iteration 3 does not run, `until`/`mutate`/`exhausted` not invoked
- [x] 4.2 Break on `until`: iteration 1 returns `Right(p1)`, `until(p1, 1) == true` → loop exits `Right(p1)`, `mutate` and `exhausted` not invoked
- [x] 4.3 Exhaustion: `maxAttempts = 3`, every `until` returns false, iteration 3 returns `Right(p3)` → loop exits `Left(exhausted(p3, 3))`, `mutate` not invoked after iteration 3
- [x] 4.4 Mutate between iterations: iteration 1 returns `Right(p1)`, `mutate(p1, 1) → p1m` → iteration 2 starts with `Right(p1m)`
- [x] 4.5 No-mutate path: when `mutate` is null and iteration 1 returns `Right(p1)` with `until == false`, iteration 2 starts with `Right(p1)`
- [x] 4.6 Incoming `Left` passthrough: rail is `Left(err)` at start of `Loop` → no hooks invoked, rail unchanged
- [x] 4.7 `maxAttempts = 1` runs body once; if `until == false`, exits `Left(exhausted(...))`; if `until == true`, exits `Right`
- [x] 4.8 `maxAttempts = 0` and negative values throw `ArgumentOutOfRangeException` at registration
- [x] 4.9 Empty body (`Loop(body: _ => {}, ...)`) is allowed; behaves as a pure poll over an externally-mutated payload
- [x] 4.10 `Recover<TErr>` inside body converts iteration N's `Left` to `Right` → loop continues to iteration N+1 with mutate applied
- [x] 4.11 `Recover` inside body in iteration 1 does NOT catch an uncaught `Left` raised in iteration 2 (per-iteration recovery scope)
- [x] 4.12 Attempt index consistency: `until`, `mutate`, and `exhausted` all see the expected `attempt` number for the iteration they correspond to
- [x] 4.13 Async hooks: all async overloads of `until`, `mutate`, `exhausted` are awaited; cancellation token is propagated
- [x] 4.14 Mixed sync/async hooks compile and execute (e.g., sync `until` with async `mutate`)
- [x] 4.15 Operators surrounding `Loop` run in registration order: `Do(a)` completes before loop begins; `Do(b)` runs only after loop completes
- [x] 4.16 `LoopBuilder` exposes `Do`, `Tap`, `Effects`, `TryTap`, `TryEffects`, `Branch`, `Ensure`, `Recover` (positive compile test)
- [x] 4.17 `LoopBuilder` does NOT expose `Loop`, `Detach`, `Finally` (negative compile assertion — separate assembly or `// @compile-fail` style harness if available, otherwise documented)
- [x] 4.18 Inner `Branch` inside `Loop` body works as expected: branch body runs conditionally per iteration

## 5. Example Project

- [x] 5.1 Add a poll-for-ready sample to `Zooper.Bee.Example` using `Loop` with `until`, no `mutate`
- [x] 5.2 Add a retry-with-mutation sample using `Loop` with all four hooks (`body`, `until`, `mutate`, `exhausted`) — demonstrate `Recover` inside the body to keep iterating despite transient failures
- [x] 5.3 Verify both samples build and run

## 6. MediatR Package Alignment

- [x] 6.1 Audit `Zooper.Bee.MediatR` — no API breaks expected since `Loop` is additive; confirm build still passes
- [x] 6.2 Run `Zooper.Bee.MediatR` tests against the new `Zooper.Bee`

## 7. Docs and Changelog

- [x] 7.1 Add `Loop` to `README.md` operator list and table; include one short code example
- [x] 7.2 Update `CHANGELOG.md` `## [Unreleased]` — under `### Added`: `Loop(body, until, maxAttempts, exhausted, mutate)` operator with one-line summary
- [x] 7.3 XML docs on every `Loop` overload describe: right-rail behaviour, left-passthrough, iteration order, `mutate` cannot fail the rail, exhaustion uses caller's delegate
- [x] 7.4 Document the per-iteration `Recover` scoping rule with a small example in XML docs or README

## 8. Release Readiness

- [x] 8.1 Run full test suite (`Zooper.Bee.Tests` + `Zooper.Bee.MediatR` tests + example project build) and confirm green
- [x] 8.2 Build `Zooper.Bee`, `Zooper.Bee.MediatR`, `Zooper.Bee.Example` in Release configuration
- [x] 8.3 Bump package version (minor bump, e.g. `4.1.0`) since `Loop` is additive on top of the `4.0.0` vNext API
- [x] 8.4 Read through the retry-with-mutation example to confirm the loop reads as narrative — qualitative success check matching the vNext readability metric
