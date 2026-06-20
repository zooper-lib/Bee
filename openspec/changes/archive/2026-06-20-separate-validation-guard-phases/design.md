## Context

`Railway.Create` uses a two-phase builder: a `RailwayGuardBuilder` (exposing both `.Guard()` and `.Validate()`) and a `RailwayStepsBuilder`. The guard builder collects guards and validations into two separate lists. At run time `ExecuteRailwayAsync` (RailwayStepsBuilder.cs:558) runs `RunValidationsAsync` first, then `RunGuardsAsync`, then operators — so validations always precede guards regardless of the order they were chained on the builder. The implicit ordering is correct but invisible: `.Guard(a).Validate(b)` runs `b` before `a`, surprising users.

The DI registration extensions (`AddRailwayGuards`, `AddRailwayValidations`) and the `IRailwayGuard*`/`IRailwayValidation*` interfaces are assembly-scan helpers and are **not** coupled to the builder; they are unaffected.

## Goals / Non-Goals

**Goals:**
- Make the three phases — Validation → Guarding → Steps — explicit in the API surface.
- Preserve the existing run-time order and within-phase declaration order.
- Keep the `Internal.RailwayValidation` / `Internal.RailwayGuard` wrapper types and `RailwayStepsBuilder` execution unchanged.

**Non-Goals:**
- No change to DI registration extensions or the `IRailway*` interfaces.
- No change to step/operator behavior, finally activities, or branching/loop operators.
- No back-compat shim for the combined builder (chosen: clean break).

## Decisions

**Separate phase lambdas on `Railway.Create`.** Configuration is split into two distinct builder types, each exposing only its phase's methods:
- `RailwayValidationBuilder<...>` — exposes `.Validate(...)` overloads only; collects `List<RailwayValidation>`.
- `RailwayGuardBuilder<...>` — reduced to `.Guard(...)` overloads only; collects `List<RailwayGuard>`.

`Railway.Create` gains a `validations` configuration delegate alongside the existing `guards` and `steps` delegates:

```csharp
Railway.Create<TRequest, TPayload, TSuccess, TError>(
    Func<TRequest, TPayload> factory,
    Func<TPayload, TSuccess> selector,
    Action<RailwayValidationBuilder<...>>? validations,
    Action<RailwayGuardBuilder<...>>? guards,
    Action<RailwayStepsBuilder<...>> steps)
```

Both `validations` and `guards` are nullable/optional. Overload set:
- `(factory, selector, validations, guards, steps)` — full.
- `(factory, selector, guards, steps)` — guards only (validations null).
- `(factory, selector, steps)` — neither.
- Parameterless-request (`Func<TPayload>` factory) mirrors of each.

The `guards`-only and `steps`-only overloads keep today's common call sites compiling; only call sites that called `.Validate()` on the combined builder must move that call into the new `validations` delegate.

**Wiring.** `Create` instantiates `RailwayValidationBuilder` and `RailwayGuardBuilder`, invokes the optional delegates, then passes `validationBuilder.Validations` and `guardBuilder.Guards` into the existing `RailwayStepsBuilder` constructor (unchanged signature). `ExecuteRailwayAsync` ordering is already Validation → Guarding → Steps and stays as-is.

**Phase order is structural, not chained.** Because each phase has its own delegate and builder, a user cannot interleave validate/guard calls; the phase a check belongs to is determined by which delegate registers it. This removes the original confusion at the source.

## Risks / Trade-offs

- **Breaking change.** Any call site using `.Validate()` on `RailwayGuardBuilder` no longer compiles. Mitigation: mechanical migration (move `.Validate(...)` calls into the new `validations` delegate); document in changelog. The example project must be updated.
- **Overload proliferation.** Optional `validations` + `guards` across request/parameterless factory variants multiplies `Create` overloads. Accepted to keep nullable-delegate ergonomics and avoid forcing empty lambdas. Resolved by delegating overloads to the one full implementation.
- **Naming.** Reusing the `RailwayGuardBuilder` name with reduced surface could mislead readers who remember it held validations. Mitigation: updated XML docs stating it is guard-only and pointing to `RailwayValidationBuilder`.
