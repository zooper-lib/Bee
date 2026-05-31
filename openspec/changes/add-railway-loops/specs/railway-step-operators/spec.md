## MODIFIED Requirements

### Requirement: Step Execution Preserves Registration Order

The `RailwayStepsBuilder` SHALL execute every registered operator in the exact order it was added. This MUST hold uniformly for `Do`, `Tap`, `Effects`, `TryTap`, `TryEffects`, `Detach`, `Branch`, `Ensure`, `Recover`, and `Loop`. No operator category may be reordered relative to another.

#### Scenario: Operators of different categories run in registration order

- **WHEN** a railway registers `Do(a)`, `Tap(b)`, `Branch(…)`, `Ensure(…)`, `Do(c)` in that order
- **THEN** at runtime the operators execute in exactly that order
- **AND** no category is hoisted, deferred, or reordered

#### Scenario: Loop participates in registration order as a single transformer

- **WHEN** a railway registers `Do(a)`, `Loop(...)`, `Do(b)` in that order
- **THEN** `Do(a)` runs to completion before the loop begins iterating
- **AND** `Do(b)` runs only after the loop has produced its final `Either`
- **AND** the loop is not reordered relative to surrounding operators
