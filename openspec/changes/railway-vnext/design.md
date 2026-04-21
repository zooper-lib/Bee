## Context

Bee's current step phase is implemented by `RailwayStepsBuilder<TRequest, TPayload, TSuccess, TError>` (introduced in 3.5.0). Every operator ultimately appends a `Func<TPayload, CancellationToken, Task<Either<TError, TPayload>>>` to a single `_steps` list, and `RunStepsAsync` executes them in order, short-circuiting on the first `Left`. The public vocabulary (`Do`, `DoIf`, `DoAll`, `Group`, `WithContext`, `Detach`, `Parallel`, `ParallelDetached`, `Finally`) does not tell a reader whether a step changes the payload, performs a side effect, or alters control flow. `Group` in particular is used for both unconditional sequences and conditional sub-pipelines.

Railway vNext replaces this vocabulary with an explicit operator set (`Do`, `Tap`, `Effects`, `TryTap`, `TryEffects`, `Detach`, `Finally`, `Recover`, `Branch`, `Ensure`). The central design pressure is that `Recover` is a new, first-class operator that converts selected `Left` outcomes back into `Right`, which the current short-circuit executor cannot express without a rewrite.

## Goals / Non-Goals

**Goals:**

- Provide an operator set whose name uniquely identifies step intent (progression, side effect, branch, rule enforcement, recovery, lifecycle).
- Make `Recover<TError>` a first-class operator that runs in registration order with the other step operators.
- Make side-effect failure policy explicit at the call site: `Tap`/`Effects` fail the railway, `TryTap`/`TryEffects` do not, `Detach` is not awaited.
- Enforce the `Left`/`Right` semantics from the summary: `Left` is a boundary failure, `Right` is a valid domain outcome — even outcomes like `NotFound` when the domain considers them valid.
- Keep the guard phase (`RailwayGuardBuilder` with `Guard` / `Validate`) unchanged.
- Produce code that reads top-to-bottom as a narrative of the use case, matching the `LoadPlanet → Tap → Branch → BuildReadModel` mental model from the summary.

**Non-Goals:**

- Keeping the 3.5 operator vocabulary as a compatibility shim. `Group`, `DoIf`, `DoAll` are removed, not deprecated.
- Generalising `Recover` into an "on any `Left`" hook. Recovery must be scoped to a specific error type.
- Adding a new parallelism story. Parallelism behaviour (if any survives) is addressed in a later change.
- Changing the guard phase or the `Railway.Create` factory signature.
- Changing the payload/context model (the `TRequest → TPayload → TSuccess` shape is retained).

## Decisions

### Decision 1: Replace the short-circuit executor with an Either-flowing pipeline

The step executor will be rewritten so each registered operator is modelled as a transformer `Func<Either<TError, TPayload>, CancellationToken, Task<Either<TError, TPayload>>>`. The executor threads the current `Either` through every operator without short-circuiting at the executor level — each operator decides for itself whether to act on `Right`, act on `Left`, or pass through.

| Operator | Acts on `Right`                | Acts on `Left`                      |
|----------|--------------------------------|-------------------------------------|
| `Do`     | runs, may return new `Either`  | passes through                      |
| `Tap`    | runs; failure → `Left`         | passes through                      |
| `Effects`| runs each inner `Do` in order; first failure → `Left` | passes through |
| `TryTap` | runs; failure is swallowed     | passes through                      |
| `TryEffects` | runs each inner `Do`; failures swallowed | passes through          |
| `Detach` | fires and returns; not awaited | passes through                      |
| `Branch` | runs inner pipeline if `when`  | passes through                      |
| `Ensure` | if `when`, returns `Left(failWith)` | passes through                 |
| `Recover<TErr>` | passes through          | if `Left` is assignable to `TErr`, run handler → `Right` |
| `Finally`| runs regardless in a finally block | runs regardless in a finally block |

**Why over short-circuit with lookahead:** a lookahead approach (on `Left`, scan forward for a matching `Recover`) couples the executor to the operator list shape and requires special cases for every operator that should/shouldn't be skipped. Either-flowing keeps the executor dumb: operators are self-describing transformers. It also matches the railway mental model exactly — the `Either` is the rail, and each operator is a switch on the rail.

**Alternatives considered:**

- *Short-circuit with lookahead for `Recover`*: simpler to retrofit, but every new operator would need rules in the executor about how it interacts with in-flight errors. Rejected.
- *Two parallel lists (right-track steps vs. recovery handlers keyed by error type)*: breaks registration-order semantics (summary shows recovery interleaved with steps) and cannot express "recover only errors produced before this point". Rejected.

### Decision 2: `Tap` is a distinct operator, not a `Do` variant

`Tap` takes a side-effect delegate that never returns a new payload. Signatures:

```csharp
Tap(Func<TPayload, CancellationToken, Task> effect)
Tap(Action<TPayload> effect)
Tap(Func<TPayload, CancellationToken, Task<Either<TError, Unit>>> effect) // strict with error channel
```

The third overload allows a `Tap` to *fail* the railway by returning `Left`; the payload still does not change on success.

**Why distinct from `Do`:** the summary's key distinction — "if removing the step changes the business result, it's `Do`; if it only removes logging/telemetry/auditing, it's `Tap`" — only survives if `Tap` is syntactically distinct. A `Do` that happens to return the same payload is indistinguishable from a `Tap` at a call site.

### Decision 3: `Effects` and `TryEffects` use a nested builder exposing only `Do`

```csharp
.Effects(e => e.Do(ctx => …).Do(ctx => …))
```

The inner builder (`EffectsBuilder<TPayload, TError>`) exposes **only** `Do`, not `Tap`/`Branch`/`Recover`/etc. Each inner `Do` is a side-effect delegate with the same shape as `Tap` (the third overload). The surrounding `Effects` governs the failure policy:

- `Effects` — strict: the group acts like a single `Tap`; first inner failure produces `Left`.
- `TryEffects` — best-effort: failures are swallowed per inner step; the group never produces `Left`.

**Why reuse `Do` inside `Effects` instead of inventing a new verb:** inside an `Effects` block the reader has already been told "these are effects". The inner verb only needs to mean "another effect"; `Do` reads naturally and matches the example in the summary. The inner builder's type disambiguates — inner `Do` does not take the `TPayload → Either<TError, TPayload>` shape; it takes the effect shape.

### Decision 4: `Detach` stays fire-and-forget and is configured like `Effects`

```csharp
.Detach(d => d.Do(ctx => …).Do(ctx => …))
```

Inner delegates run on background `Task`s scheduled from the main pipeline; the pipeline does not await them. Exceptions are swallowed. There is no conditional `Detach(when:, …)` at this layer — if a detached block should be conditional, wrap it in a `Branch`.

**Why no built-in condition:** the 3.5 `Detach(Func<TPayload,bool>? condition, …)` overload is one of the implicit-semantics patterns the summary explicitly warns against. Reusing `Branch` keeps conditional control flow in one place.

### Decision 5: `Branch` takes named `when` and `branch` parameters

```csharp
.Branch(
    when: ctx => ctx.Image is null && !ctx.ImagePending,
    branch: b => b.Do(…).Do(…).Tap(…))
```

The inner builder (`BranchBuilder<TPayload, TError>`) is a full right-side DSL — it exposes `Do`, `Tap`, `Effects`, `TryTap`, `TryEffects`, `Recover`, `Ensure`, but **not** `Branch` (one level deep) or `Finally`/`Detach` (scope confusion). The branch's final payload becomes the main pipeline's payload.

**Why named parameters:** the positional form `.Branch(when, branch)` was considered but positional C# lambdas with identical delegate shapes (`Func<TPayload, bool>` vs `Action<BranchBuilder>`) are hard to read at the call site. Named parameters make intent unmistakable and match the summary's example verbatim.

**Why no nested `Branch`:** nested branches are expressible but hurt readability, which is the whole point of this change. If a caller needs nested control flow, they can extract the inner branch into a named helper method. This is a soft API convention, not a hard compile-time rule, to avoid over-engineering the builder type system.

### Decision 6: `Ensure` is not `DoIf` in disguise

```csharp
.Ensure(
    when: ctx => !ctx.IsVisible,
    failWith: ctx => new PlanetError.NotVisible(ctx.PlanetId))
```

`Ensure` only produces `Left`. It never mutates the payload. It is the exact inverse of `Recover`: `Ensure` pushes `Right` onto `Left`; `Recover` pulls `Left` onto `Right`.

**Why separate from `DoIf`:** `DoIf` (now removed) ran an arbitrary activity when a condition held — it could mutate, fail, or do nothing. `Ensure` has a single, specific job. Callers who want a mutating conditional should use `Branch`.

### Decision 7: `Recover<TError>` is type-directed and scoped to prior errors

```csharp
.Recover<PlanetImageTimeoutError>((error, ctx) => ctx.MarkImageAsPending())
```

- Generic type parameter filters which errors this recovers. If the in-flight `Left`'s runtime type (or the type the error reports via a discriminator if `TError` is a closed union) is assignable to `TError`, the handler runs. Otherwise the `Left` passes through.
- The handler receives both the error and the payload at the point of failure. The pre-failure payload is the one captured immediately before the step that produced the `Left`, threaded through the executor as part of the operator's state.
- The handler returns a new `TPayload` (synchronous or `Task<TPayload>`), which becomes the rail's `Right`.
- Recovery is scoped to errors produced by operators *before* the `Recover`. An error produced by a later operator is not caught by an earlier `Recover`.

**Why type-directed (generic) instead of predicate-based:** the summary's example (`Recover<PlanetImageTimeoutError>`) and its explicit warnings about "avoid using `Left` as a branching mechanism for valid domain states" mean recovery should be narrowly scoped. Typing `TError` prevents the `Recover(_ => true, …)` antipattern from emerging.

**Why no payload-returning `Either`:** the handler cannot fail to a different `Left`. If a recovery is itself fallible, the caller should follow the `Recover` with a `Do` or `Ensure` that expresses the secondary failure. Keeping `Recover` infallible makes the type signatures simpler and the reading order obvious.

**Carrying pre-failure payload:** the executor must snapshot the last `Right` payload before each operator runs so that, when a `Recover` fires, the handler receives a consistent payload rather than `default`. This is a per-frame state, not a global one.

### Decision 8: `Finally` runs outside the Either-flowing pipeline

`Finally` is not an operator in the transformer list. It is registered separately (matching the current implementation) and runs in a `try/finally` that wraps the entire step-phase execution. It always runs — on `Right`, on unrecovered `Left`, and on cancellation — with the last observed payload. `Finally` never changes the rail's outcome.

**Why separate:** modelling `Finally` as a transformer that ignores its input would misrepresent its lifecycle role. Keeping it out of the transformer chain makes its "always runs" nature visible in the executor, not hidden in operator semantics.

### Decision 9: Retire `Parallel`, `ParallelDetached`, `WithContext`, `BranchWithLocalPayload` for now

The 3.5 builder also exposes `Parallel`, `ParallelDetached`, `WithContext`, and (via the obsolete `RailwayBuilder`) `BranchWithLocalPayload`. None appear in the vNext summary. They will be **removed** from the new `RailwayStepsBuilder`. Rationale:

- `Parallel`/`ParallelDetached`: their failure policy is implicit ("merge results") and they are the kind of "condition on both rails" abstraction the summary warns against. Reintroducing them with explicit semantics is out of scope for this change.
- `WithContext`: its local-state abstraction is solvable by letting the caller extend `TPayload`. If the new vocabulary reveals a genuine gap, it can be re-added in a follow-up.
- `BranchWithLocalPayload`: already obsolete in 3.5 (`RailwayBuilder` only, behind `[Obsolete]`). Not carried forward.

**Risk**: removing `Parallel` without a replacement breaks any caller that relied on fan-out. The migration notes in the changelog will need to call this out explicitly and suggest `Task.WhenAll` inside a `Do` as a stop-gap.

### Decision 10: API shape — keep `Railway.Create` signature, evolve the builders

`Railway.Create(factory, selector, guards, steps)` signature is unchanged. The `steps` action still receives a `RailwayStepsBuilder<TRequest, TPayload, TSuccess, TError>`. The builder's methods are what's replaced. Because the guard phase is unchanged and the factory signature is unchanged, migration is localised to the `steps: r => r.…` body.

**Why keep `Railway.Create`:** the 3.5 two-phase factory is the stable part of the API — it separates guards from steps at the type level, which is still what we want. The churn is all inside the steps phase.

## Risks / Trade-offs

- **Executor rewrite** → Carries a regression risk for step ordering and the `Finally` lifecycle. *Mitigation:* port all 3.5 `RailwayStepsBuilder` tests that cover registration order and finally-always-runs, adapt to the new vocabulary, and keep them green. Add new tests for every operator's `Right`/`Left` behaviour from the table in Decision 1.
- **Removing `Parallel`/`ParallelDetached`/`WithContext`** → Downstream code may not compile. *Mitigation:* changelog migration notes with explicit code recipes (`Task.WhenAll` inside a `Do`, extend `TPayload` instead of `WithContext`). Flag this prominently as a breaking change.
- **`Recover` semantics for payload snapshotting** → Easy to introduce a subtle bug where the handler receives a partially-mutated payload from a failed `Do`. *Mitigation:* contract is that operators returning `Left` must return the pre-call payload alongside the error; the executor snapshots before each operator runs and uses the snapshot if the operator returns `Left`.
- **Type-directed `Recover` with closed-union `TError`** → If `TError` is a discriminated union (common pattern in F#-style error types), runtime `is`-checks may not match intuitively. *Mitigation:* document that `Recover<TError>` uses `is` against the boxed `Left` value; recommend using concrete error subtypes as the generic argument, not the closed union.
- **`Effects` / `TryEffects` builder duplication** → Two near-identical nested builders that differ only in failure policy. *Mitigation:* single `EffectsBuilder<TPayload, TError>` type, instantiated in two modes; `RailwayStepsBuilder.Effects` and `.TryEffects` are thin wrappers that pick the mode.
- **"Reads like a narrative" is subjective** → The success metric for this change is qualitative. *Mitigation:* rewrite `Zooper.Bee.Example` in the new vocabulary as part of this change and diff the before/after in the PR. The example is the success metric.

## Migration Plan

1. Land this change behind a feature branch; no staged rollout is possible since the API surface is source-breaking.
2. Ship as `4.0.0` with a migration section in `CHANGELOG.md` that lists the removed operators and gives a recipe for each:
   - `Group` → `Branch(when: …, branch: …)` (conditional) or inline sequence of `Do`/`Tap` calls (unconditional).
   - `DoIf` → `Branch(when: …, branch: b => b.Do(…))`.
   - `DoAll` → multiple `Do(…)` calls.
   - `Parallel` / `ParallelDetached` → `Task.WhenAll` inside a `Do`, or await removal of parallelism from the railway and move it into the handler.
   - `WithContext` → extend `TPayload` with the local state.
3. Rewrite `Zooper.Bee.Example` in the new vocabulary in the same PR.
4. Update `README.md` to reference the new operator set.
5. No rollback in the package sense — consumers who cannot migrate pin to `3.5.x`.

## Open Questions

- **`Parallel` removal** — confirm with the user whether removing `Parallel`/`ParallelDetached` outright is acceptable, or whether they should remain as-is during vNext with a plan to revisit. Current design assumes removal.
- **Sync overloads** — every 3.5 operator has a sync and async overload. vNext should follow the same pattern (`Tap(Action<TPayload>)` plus `Tap(Func<TPayload, CancellationToken, Task>)`), but confirm before proliferating overloads on every operator.
- **Nested `Branch`** — the design forbids it at the builder type level (the `BranchBuilder` does not expose `Branch`). Confirm this is the right call, or allow nesting with a depth convention.
- **`Ensure` inside `Branch`** — permitted. Confirm.
- **`Recover` inside `Branch`** — permitted, and scoped to errors raised within that branch. Confirm this scoping rule matches the user's mental model.
- **Error-type discriminator** — if `TError` is a closed union, how does `Recover<TSubError>` match? Current design: plain `is`-check against the runtime type of `Left`. Confirm.
