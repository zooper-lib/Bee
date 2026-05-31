## Context

`railway-vnext` introduces an Either-flowing step executor where each operator is modelled as a `Func<Either<TError, TPayload>, CancellationToken, Task<Either<TError, TPayload>>>`. `Branch` is the closest existing precedent for what `Loop` needs: a nested right-side sub-pipeline that runs conditionally, with its inner builder exposing a curated subset of operators and producing an `Either` that replaces the rail state.

There is currently no way to express bounded iteration inside a railway. Authors who need "poll until ready" or "retry with mutated input" either wrap the railway in an external `while`, write recursive `Do` helpers, or shell out to ad-hoc retry libraries. All three hide iteration intent from the DSL and from the railway's reading order.

`Loop` is additive to the vNext operator set. The executor rewrite from `railway-vnext` (Decision 1) already supports treating any operator as an Either-transformer, so `Loop` slots in as one more transformer — it does not require a second executor rewrite.

## Goals / Non-Goals

**Goals:**

- First-class bounded iteration on the right rail with a break condition, a hard attempt cap, and an inter-iteration mutation hook.
- Reads like the rest of the vNext DSL: `Loop(body: …, until: …, maxAttempts: …, mutate: …)` matches the named-parameter style of `Branch` and `Ensure`.
- Nested right-side operators inside the body (`Do`, `Tap`, `Effects`, `TryTap`, `TryEffects`, `Branch`, `Ensure`, `Recover`) work exactly as they do at the top level, scoped to the current iteration.
- Exhaustion is expressed on the rail, not as an exception: hitting `maxAttempts` without `until` becoming true produces a caller-defined `Left`.
- `Left` semantics from `railway-step-operators` are preserved: a `Left` raised inside the body short-circuits the loop (caller can wrap critical sections in `Recover` to keep iterating).

**Non-Goals:**

- Backoff, jitter, or delay between iterations. The first iteration of `Loop` is a control-flow primitive, not a retry policy library. Delays can be added inside the body via an async `Do`/`Tap` if needed.
- Unbounded loops (`maxAttempts` optional / null). All loops must be bounded.
- Nested `Loop` inside `Loop`. Same readability rationale as nested `Branch` (vNext Decision 5).
- `Loop` inside `Branch` body or `Loop` body — out of scope for v1; the inner builders do not expose `Loop`.
- `Detach` or `Finally` inside `Loop` body. Lifecycle concerns belong at the top level.
- Concurrent iterations or fan-out. `Loop` is strictly sequential.
- Parallel execution of multiple iterations.

## Decisions

### Decision 1: `Loop` is a single transformer that drives its body N times

`Loop` registers as one Either-transformer in the parent operator list. Internally the transformer compiles its body once into a nested transformer chain (matching how `Branch` compiles its `BranchBuilder`) and then runs that chain in a sequential loop driven by the loop's own controller. The parent executor is unaware of iteration — it sees a single operator that consumes an `Either` and produces an `Either`.

**Why over "expand body into the parent list N times":** the body is dynamic in length (iteration count depends on runtime state). Static expansion is impossible. Treating `Loop` as a single transformer also matches the `Branch` precedent and keeps registration order preserved at the parent level.

**Alternatives considered:**

- *Special-case the executor to understand iteration*: bloats the executor with one more operator-aware code path. Rejected for the same reason `Recover` is operator-local, not executor-local (vNext Decision 1).
- *Build `Loop` on top of recursion at the call site*: would make iteration invisible to the DSL — the explicit goal this change exists to fix.

### Decision 2: Iteration semantics

Pseudocode (sync form; async is identical with `await`):

```
attempt = 1
state = incomingRight   // pre-failure payload snapshot
loop:
    result = runBody(Right(state))
    if result is Left(err):
        return Left(err)                          // body short-circuit
    state = result.RightValue
    if until(state, attempt) == true:
        return Right(state)                       // break condition met
    if attempt == maxAttempts:
        return Left(exhausted(state, attempt))    // exhaustion → caller-defined Left
    state = mutate(state, attempt)                // inter-iteration mutation
    attempt = attempt + 1
    goto loop
```

`mutate` only runs **between** iterations — never before the first, never after the loop exits. `until` runs after each iteration. `exhausted` runs at most once, only when the cap is hit.

**Why `until` after the body (not before):** the natural reading is "do work, then check if we're done." A pre-check (`while(until is false)`) would require running `until` against the incoming payload, which may not have the loop-relevant fields populated yet. Authors who want a pre-check can start the body with `Ensure(when: …, failWith: …)` or place the check inside the body.

### Decision 3: Signature uses named parameters

```csharp
public RailwayStepsBuilder<…> Loop(
    Action<LoopBuilder<TPayload, TError>> body,
    Func<TPayload, int, bool> until,
    int maxAttempts,
    Func<TPayload, int, TError> exhausted,
    Func<TPayload, int, TPayload>? mutate = null);
```

Async overloads mirror this with `Func<TPayload, int, CancellationToken, Task<bool>>`, `Func<TPayload, int, CancellationToken, Task<TError>>`, and `Func<TPayload, int, CancellationToken, Task<TPayload>>`.

- `attempt` is 1-indexed (matches "first attempt" / "max attempts" mental model).
- `mutate` is optional — pure polling loops have no mutation.
- `exhausted` is **required**. There is no library-default error type because `TError` is open; the caller chooses what `Left` they want.
- `maxAttempts` is required and must be `>= 1`. Constructor validates this and throws `ArgumentOutOfRangeException` at registration time.

**Why `exhausted` is a delegate, not a constant:** the exhaustion error often wants to carry the last observed payload state (e.g., `LoopExhaustedError(planetId: payload.PlanetId, attempts: 5)`). A `TError` constant cannot capture iteration-local data.

**Why `attempt` is exposed:** loops that mutate based on iteration index (e.g., increment a retry counter, swap a strategy) need it. Passing it explicitly is clearer than asking authors to thread it through the payload.

**Alternatives considered:**

- *Pre-check semantics (`while`)*: rejected — see Decision 2.
- *Library-defined `LoopExhaustedError`*: would require `TError` to inherit a common base or implement an interface, breaking the open-`TError` contract. Rejected.
- *`mutate` returns `Either<TError, TPayload>` (fallible mutation)*: rejected. Mutation is a payload tweak; if it can fail, model it as an inner `Do` at the start of the next body rather than a separate failable hook. Keeps the loop primitive simple.

### Decision 4: `LoopBuilder` allowed operators

`LoopBuilder<TPayload, TError>` exposes:

- `Do` (sync + async)
- `Tap` (all three overloads from vNext)
- `Effects` / `TryEffects`
- `TryTap`
- `Branch`
- `Ensure`
- `Recover<TErr>`

It does **not** expose: `Loop` (no nesting), `Detach` (lifecycle), `Finally` (lifecycle).

**Why the same set as `BranchBuilder` plus `Branch`:** inside a loop iteration, all the right-side operators that make sense in a branch also make sense in a loop body. Branch is included because conditional behaviour inside an iteration is a common pattern (e.g., "if the response is partial, run one extra step"). The vNext convention that `BranchBuilder` does not expose `Branch` is a per-builder choice for readability — `LoopBuilder` is a separate scope and benefits from `Branch`.

**Why no nested `Loop`:** matches vNext Decision 5's rationale for forbidding nested `Branch`. Authors who genuinely need nested iteration can extract the inner pipeline into a helper that builds and runs its own railway, or revisit when v2 has more evidence.

### Decision 5: `Recover` inside the body is scoped to the current iteration

`Recover<TErr>` registered inside the body catches `Left`s produced by earlier operators **in the same iteration**. A `Left` raised in iteration *N* that is not caught by a `Recover` in the body exits the entire loop with that `Left` — it does not "carry over" to iteration *N+1*.

This is a direct consequence of Decision 1 + Decision 2: each iteration runs the compiled body chain end-to-end on a fresh `Right(state)`. The Either threaded through one iteration is independent of the next.

**Why this scoping:** any other rule would either (a) require the loop to inspect operator types inside the body (breaks executor opacity from vNext Decision 1) or (b) introduce a second kind of recovery that spans iterations — which is the caller's job to express by putting `Recover` inside the body.

**Consequence:** "keep retrying despite errors of type `TErr`" is expressed as `Loop(body: b => b.Do(risky).Recover<TErr>(fallback), until: …, maxAttempts: …)`. The `Recover` converts the iteration's `Left` back to `Right`, the loop continues, and `mutate` (if set) gets to adjust for the next attempt.

### Decision 6: Payload snapshot semantics

When the loop transformer is invoked, the incoming rail is either `Right(p)` or `Left(e)`.

- `Left(e)` → the loop passes through unchanged; `body`, `until`, `mutate`, `exhausted` are not invoked. Matches every other right-side operator.
- `Right(p)` → `p` is the starting state for iteration 1.

The body's inner executor follows the same payload-snapshot rules as the top-level executor (vNext Decision 7 / 8): each inner operator snapshots the last `Right` payload before running so that inner `Recover` handlers receive the pre-failure payload. The loop controller observes only the final `Either` from each iteration; it does not see intermediate snapshots inside the body.

### Decision 7: Cancellation propagates without special handling

The `CancellationToken` is threaded into `body`, `until`, `mutate`, and `exhausted` (async overloads). If the token is cancelled mid-iteration, `body` raises `OperationCanceledException` per existing executor behaviour — the loop transformer does not catch it. Cancellation between iterations (after `mutate`, before next `body` start) is the caller's responsibility to honour inside async delegates.

**Why no explicit "cancellation = exit with Left":** cancellation has uniform semantics in vNext (exception propagates). Special-casing inside `Loop` would diverge from that contract.

### Decision 8: API additions are non-breaking

Adding `Loop(...)` overloads to `RailwayStepsBuilder<...>` is purely additive. No existing operator changes. `Loop` is not added to `BranchBuilder` (per Decision 4). The capability delta for `railway-step-operators` adds `Loop` to the "registration order is preserved" requirement and the operator inventory, but does not modify or remove any existing requirement.

## Risks / Trade-offs

- **Infinite loop bug despite the cap** → A `maxAttempts` of `int.MaxValue` combined with a slow body is still effectively infinite. *Mitigation:* document that `maxAttempts` is a hard cap, not a "reasonable" cap; recommend explicit small numbers (typical: 3–10) in the docs. No runtime ceiling — the caller owns the cap.
- **`mutate` mistaken as a fallible step** → Callers may try to put real side effects with failure semantics inside `mutate`. *Mitigation:* docs and the operator name reinforce that `mutate` is a pure payload transform. Fallible work goes into the body as `Do` / `Tap`.
- **`Recover`-inside-`Loop` confusion** → Some authors will expect a top-level `Recover` after a `Loop` to catch errors from *individual iterations*. It will not — it catches the loop's final `Left` (either body short-circuit or exhaustion). *Mitigation:* docs include a worked example of "recover per-iteration vs. recover post-loop"; tests cover both shapes.
- **No nested `Loop`** → Some use cases (matrix retry: outer "strategies", inner "attempts per strategy") cannot be expressed in v1. *Mitigation:* document the limitation. If demand is real, lift in v2 once the v1 shape is settled.
- **Iteration count drift between `until`/`mutate`/`exhausted`** → The contract says `until` and `exhausted` see `attempt = N` for iteration `N`, and `mutate` sees `attempt = N` while preparing iteration `N+1`. Off-by-one bugs here would be silent. *Mitigation:* explicit test for "attempt indices match across all three hooks" and a worked example in `Zooper.Bee.Example`.
- **Sync/async overload explosion** → Three hooks × two arities (sync/async) × overloads = many method signatures. *Mitigation:* generate the matrix once in the builder; document that the async forms accept any combination (sync `until` with async `mutate`, etc.).

## Migration Plan

`Loop` is additive. No migration is required for existing railways.

- Land alongside `railway-vnext` (depends on the executor rewrite from that change). If `railway-vnext` archives first, this change applies cleanly. If both are in flight simultaneously, the implementer should rebase this change onto vNext's executor types.
- Ship in the next minor release after vNext (which is `4.0.0`). `Loop` lands in `4.1.0` unless explicitly held back.
- Update `Zooper.Bee.Example` with one loop sample (recommended: poll-for-ready, since it exercises `until` without `mutate`).
- `CHANGELOG.md` `## [Unreleased]` entry highlights `Loop` and links to the sample.
- No rollback strategy needed — additive operator.

## Open Questions

- **`exhausted` ergonomics** — required delegate vs. optional with a library-defined `LoopExhaustedError<TPayload>` (would require widening `TError` constraints). Current design: required delegate, no constraint. Confirm.
- **Per-iteration timing/observability hook** — should `Loop` expose an optional `onIteration(payload, attempt)` callback for logging without forcing the author to put a `Tap` inside the body? Current design: no, use `Tap` inside the body. Confirm.
- **`mutate` running on the last iteration** — current design says `mutate` does **not** run after the final iteration even if exhaustion is about to fire. Confirm this is the right call (vs. "always mutate after a Right that doesn't satisfy until").
- **Empty body** — `Loop(body: _ => {}, until: …, …)` would spin maxAttempts times calling only `until` and `mutate`. Allow or reject at registration? Current design: allow (caller may want a pure until-poll over an externally-mutated payload via `mutate`). Confirm.
- **`maxAttempts = 1` semantics** — equivalent to "run body once, check until, exit with Right if true or Left(exhausted) if false." Useful as a degenerate case or confusing? Current design: allowed. Confirm.
