# Tasks

## 1. Builder surfaces

- [ ] 1.1 Create `RailwayValidationBuilder<TRequest, TPayload, TSuccess, TError>` exposing only `.Validate(...)` overloads (async + sync), collecting `List<RailwayValidation<TRequest, TError>>`. Mirror existing XML docs.
- [ ] 1.2 Remove `.Validate(...)` overloads and the `Validations` list from `RailwayGuardBuilder`; leave only `.Guard(...)` overloads and `Guards`.
- [ ] 1.3 Update `RailwayGuardBuilder` XML docs to state it is guard-only and reference `RailwayValidationBuilder`.

## 2. Factory wiring

- [ ] 2.1 Add full `Railway.Create` overload `(factory, selector, validations, guards, steps)` — instantiate both builders, invoke optional delegates, pass `validations.Validations` + `guards.Guards` to `RailwayStepsBuilder`.
- [ ] 2.2 Add request-based convenience overloads delegating to the full one: `(factory, selector, guards, steps)` and `(factory, selector, steps)`.
- [ ] 2.3 Add parameterless-request (`Func<TPayload>` factory) mirrors for each overload.
- [ ] 2.4 Confirm `RailwayStepsBuilder` constructor signature and `ExecuteRailwayAsync` order (Validation → Guarding → Steps) are unchanged.

## 3. Verify ordering contract

- [ ] 3.1 Confirm `RunValidationsAsync`/`RunGuardsAsync` preserve within-phase declaration order (first failure wins).
- [ ] 3.2 Confirm validation failure short-circuits before guards and steps; guard failure short-circuits before steps.

## 4. Migrate call sites

- [ ] 4.1 Update `Zooper.Bee.Example/Program.cs` to the new API (move any `.Validate()` into the `validations` delegate).
- [ ] 4.2 Update existing tests in `Zooper.Bee.Tests/` that call `Railway.Create` with a guards delegate to match the new overload set.

## 5. Tests

- [ ] 5.1 Test: validation runs before guard before steps regardless of which delegates are supplied.
- [ ] 5.2 Test: validation failure returns error, no guard or step runs.
- [ ] 5.3 Test: guard failure returns error after validations pass, no step runs.
- [ ] 5.4 Test: two failing validations → first-registered error returned; same for two failing guards.
- [ ] 5.5 Test: railway with no validations and no guards runs steps directly and succeeds.
- [ ] 5.6 Test: parameterless-request overloads behave identically.

## 6. Docs

- [ ] 6.1 Update README / changelog noting the three-phase model and the breaking `.Validate()` migration.
