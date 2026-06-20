## Why

The railway builder exposes both `.Guard()` and `.Validate()` on a single `RailwayGuardBuilder`, but at execution time **all** validations run before **all** guards regardless of the order they were declared. A user who writes `.Guard(a).Validate(b)` still sees `b` run before `a`. The ordering is invisible in the API and contradicts the chaining order, which is confusing and easy to get wrong. We want execution order to be predictable and to match an explicit, documented phase model: **Validation → Guarding → Steps**.

## What Changes

- Establish three explicit, ordered execution phases for a railway: **Validation**, **Guarding**, then **Steps**.
- Validation phase runs first (all validations), Guarding phase second (all guards), Steps phase last — making the existing implicit order an intentional, documented contract.
- Separate validation and guarding into distinct builder surfaces so the API structure makes the phase boundary obvious, instead of mixing `.Guard()` and `.Validate()` on one builder where declaration order is silently ignored.
- Preserve declaration order **within** each phase (validations run in the order added; guards run in the order added).
- Update `Railway.Create` factory overloads and XML docs to reflect the three-phase model.
- **BREAKING**: `RailwayGuardBuilder` no longer carries both concerns; call sites that mix `.Guard()` and `.Validate()` on one builder must move to the new phase-specific builders.

## Capabilities

### New Capabilities
- `railway-execution-phases`: Defines the three ordered phases of a railway (Validation, Guarding, Steps), the semantics and ordering guarantees of each, the builder surface for registering validations and guards, and the failure/short-circuit behavior between phases.

### Modified Capabilities
<!-- No existing specs in openspec/specs/; this is the first spec for this behavior. -->

## Impact

- `Zooper.Bee/RailwayGuardBuilder.cs` — split or restructure into phase-specific builder(s).
- `Zooper.Bee/RailwayFactory.cs` — factory overloads and configuration delegates.
- `Zooper.Bee/RailwayStepsBuilder.cs` — `ExecuteRailwayAsync`, `RunValidationsAsync`, `RunGuardsAsync` ordering contract.
- `Zooper.Bee/Internal/RailwayValidation.cs`, `Zooper.Bee/Internal/RailwayGuard.cs` and related interfaces/extensions.
- `Zooper.Bee.Example/Program.cs` — example usage updated to the new API.
- Downstream consumers using `.Guard()`/`.Validate()` on the combined builder (breaking call-site change).
