## ADDED Requirements

### Requirement: Loop Operator Drives a Bounded Iteration on the Right Rail

The `RailwayStepsBuilder.Loop(body, until, maxAttempts, exhausted, mutate)` operator SHALL execute a nested right-side sub-pipeline (the body) repeatedly until either the break condition `until` becomes true or the cap `maxAttempts` is reached.

When the rail is `Right(payload)`, the loop MUST run the body with `Right(payload)` as the starting state for iteration 1. When the rail is `Left`, the loop MUST pass the `Left` through unchanged and MUST NOT evaluate `body`, `until`, `mutate`, or `exhausted`.

The builder SHALL expose synchronous and asynchronous overloads. The asynchronous overload's `until`, `mutate`, and `exhausted` delegates MUST receive the `CancellationToken` of the surrounding railway execution.

#### Scenario: Loop passes through an incoming Left

- **WHEN** the rail is `Left(error)` at the start of a `Loop`
- **THEN** the rail after the operator is `Left(error)`
- **AND** the body is not executed
- **AND** `until`, `mutate`, and `exhausted` are not invoked

#### Scenario: Loop starts iteration 1 with the incoming Right payload

- **WHEN** the rail is `Right(p0)` at the start of a `Loop`
- **THEN** the body's first iteration runs with `Right(p0)`
- **AND** the body sees `p0` as its starting payload

### Requirement: Loop Iteration Order — Body, Until, Exhaustion Check, Mutate

For each iteration `N` (1-indexed), the loop MUST perform the following operations in this exact order:

1. Run the body with the current right-side state.
2. If the body's result is `Left(err)`, exit the loop with `Left(err)` immediately and do not evaluate `until`, `exhausted`, or `mutate`.
3. Otherwise, evaluate `until(payload, attempt = N)` against the body's resulting payload. If `until` returns `true`, exit the loop with `Right(payload)`.
4. If `until` is `false` and `N == maxAttempts`, exit the loop with `Left(exhausted(payload, attempt = N))`.
5. Otherwise, evaluate `mutate(payload, attempt = N)` (if provided) to produce the starting payload for iteration `N+1`. If `mutate` is not provided, the starting payload for iteration `N+1` is the body's output payload unchanged.

`mutate` MUST NOT run before iteration 1 starts. `mutate` MUST NOT run after the loop has decided to exit (either via `until` or exhaustion). `exhausted` MUST run at most once per `Loop` invocation, and only when the cap has been reached without `until` being satisfied.

#### Scenario: Loop exits on Right when until is true after the first iteration

- **WHEN** the body of iteration 1 returns `Right(p1)` and `until(p1, 1)` returns `true`
- **THEN** the rail after the loop is `Right(p1)`
- **AND** `mutate` is not invoked
- **AND** `exhausted` is not invoked
- **AND** no further iterations run

#### Scenario: Loop runs mutate between iterations only

- **WHEN** the body of iteration 1 returns `Right(p1)`, `until(p1, 1)` returns `false`, `mutate(p1, 1)` returns `p1m`, and `maxAttempts >= 2`
- **THEN** iteration 2's body starts with `Right(p1m)`
- **AND** `mutate` is invoked exactly once with `(p1, 1)` between iterations 1 and 2

#### Scenario: Loop with no mutate preserves the body output for the next iteration

- **WHEN** the body of iteration 1 returns `Right(p1)`, `until(p1, 1)` returns `false`, no `mutate` is provided, and `maxAttempts >= 2`
- **THEN** iteration 2's body starts with `Right(p1)`

#### Scenario: until receives the body's output payload and the 1-indexed attempt number

- **WHEN** the body of iteration `N` returns `Right(pN)`
- **THEN** `until` is invoked with arguments `(pN, N)`
- **AND** `until` is invoked exactly once per non-failing iteration

### Requirement: Loop Exhaustion Produces a Caller-Defined Left

When the loop reaches iteration `maxAttempts` and `until` is `false` at the end of that iteration, the loop MUST exit with `Left(exhausted(payload, maxAttempts))` where `payload` is the body's output payload from the final iteration. The `exhausted` delegate MUST be supplied by the caller; the library MUST NOT provide a default error value.

`exhausted` MUST be invoked exactly once when exhaustion occurs, and MUST NOT be invoked when the loop exits on `until` or on a body `Left`.

#### Scenario: Loop exhaustion produces Left via the exhausted delegate

- **WHEN** `maxAttempts` is 3, every iteration's `until` returns `false`, and iteration 3's body returns `Right(p3)`
- **THEN** the rail after the loop is `Left(exhausted(p3, 3))`
- **AND** `exhausted` is invoked exactly once with `(p3, 3)`
- **AND** `mutate` is not invoked after iteration 3

#### Scenario: exhausted is not invoked when until is satisfied

- **WHEN** the loop exits because `until` returns `true` in some iteration
- **THEN** `exhausted` is not invoked

#### Scenario: exhausted is not invoked when the body produces Left

- **WHEN** the body of some iteration returns `Left(err)`
- **THEN** `exhausted` is not invoked
- **AND** the loop exits with `Left(err)`

### Requirement: Loop Body Left Short-Circuits the Loop

When the body of any iteration produces `Left(err)`, the loop MUST exit immediately with `Left(err)`. The loop MUST NOT advance to the next iteration. `until`, `exhausted`, and `mutate` MUST NOT be invoked when the body returns `Left`.

A `Recover<TErr>` operator registered **inside** the body MAY catch the `Left` and convert it back to `Right` before the body completes; in that case the iteration ends on `Right` and the loop's normal post-body sequence (Decision: until → exhaust check → mutate) proceeds.

#### Scenario: Body Left exits the loop without further iterations

- **WHEN** iteration 2's body returns `Left(err)`
- **THEN** the rail after the loop is `Left(err)`
- **AND** iteration 3 does not run
- **AND** `until`, `mutate`, and `exhausted` are not invoked for iteration 2

#### Scenario: Recover inside the body keeps the loop iterating

- **WHEN** iteration 2's body raises `Left(TimeoutError)` internally and a `Recover<TimeoutError>` inside the body converts it to `Right(p2)` before the body completes
- **AND** `until(p2, 2)` returns `false` and `maxAttempts >= 3`
- **THEN** iteration 3 runs with the payload produced by `mutate(p2, 2)` (or `p2` if no `mutate` is provided)
- **AND** the loop has not exited

### Requirement: LoopBuilder Allowed Operators

The nested builder `LoopBuilder<TPayload, TError>` SHALL expose `Do`, `Tap`, `Effects`, `TryTap`, `TryEffects`, `Branch`, `Ensure`, and `Recover<TErr>`. It MUST NOT expose `Loop` (no nested loops), `Detach`, or `Finally`.

Operators inside the body MUST preserve registration order and MUST behave exactly as their top-level counterparts within the scope of a single iteration. `Recover` inside the body is scoped to errors raised by operators that appear earlier in the same body during the same iteration.

#### Scenario: LoopBuilder exposes the documented operator surface

- **WHEN** code inside the body calls `Do`, `Tap`, `Effects`, `TryTap`, `TryEffects`, `Branch`, `Ensure`, or `Recover<TErr>`
- **THEN** the code compiles

#### Scenario: LoopBuilder does not expose Loop

- **WHEN** code inside the body calls `.Loop(...)`
- **THEN** the code fails to compile because `Loop` is not a member of `LoopBuilder`

#### Scenario: LoopBuilder does not expose Detach

- **WHEN** code inside the body calls `.Detach(...)`
- **THEN** the code fails to compile because `Detach` is not a member of `LoopBuilder`

#### Scenario: LoopBuilder does not expose Finally

- **WHEN** code inside the body calls `.Finally(...)`
- **THEN** the code fails to compile because `Finally` is not a member of `LoopBuilder`

#### Scenario: Recover inside the body does not span iterations

- **WHEN** iteration 1's body produces `Right(p1)` and iteration 2's body raises a `Left` that no `Recover` inside the body matches
- **THEN** the loop exits with that `Left` after iteration 2
- **AND** any `Recover` that succeeded in iteration 1 does not catch iteration 2's error

### Requirement: Loop Requires a Positive maxAttempts

The `Loop` operator SHALL require `maxAttempts >= 1`. A `maxAttempts` value less than 1 MUST be rejected at builder-registration time with `ArgumentOutOfRangeException`.

There is no upper bound on `maxAttempts` other than the platform's `int` range. Authors are responsible for choosing reasonable values.

#### Scenario: maxAttempts of zero is rejected at registration

- **WHEN** code calls `.Loop(body, until, maxAttempts: 0, exhausted)`
- **THEN** an `ArgumentOutOfRangeException` is thrown at builder registration time
- **AND** the railway is not constructed

#### Scenario: Negative maxAttempts is rejected at registration

- **WHEN** code calls `.Loop(body, until, maxAttempts: -1, exhausted)`
- **THEN** an `ArgumentOutOfRangeException` is thrown at builder registration time

#### Scenario: maxAttempts of one is permitted

- **WHEN** code calls `.Loop(body, until, maxAttempts: 1, exhausted)`
- **THEN** the builder accepts the registration
- **AND** at runtime the loop runs the body exactly once before either exiting on `until` or producing `Left(exhausted(payload, 1))`

### Requirement: Loop Mutate Is a Pure Payload Transform

The `mutate` delegate SHALL be a payload-to-payload transform whose only purpose is to prepare the input for the next iteration. `mutate` MUST NOT change the rail outcome (it does not produce `Left`). `mutate` MUST NOT be invoked when the loop has decided to exit.

#### Scenario: mutate cannot fail the rail

- **WHEN** a `Loop` is registered
- **THEN** the `mutate` delegate's return type is `TPayload` (or `Task<TPayload>` in the async overload)
- **AND** there is no overload of `mutate` that returns `Either<TError, TPayload>`

#### Scenario: mutate is skipped on the iteration that exits the loop

- **WHEN** iteration `N` returns `Right(pN)` and `until(pN, N)` returns `true`
- **THEN** `mutate` is not invoked for iteration `N`

#### Scenario: mutate is skipped on the exhausting iteration

- **WHEN** iteration `maxAttempts` returns `Right(pN)` and `until(pN, maxAttempts)` returns `false`
- **THEN** `mutate` is not invoked
- **AND** the loop exits with `Left(exhausted(pN, maxAttempts))`

### Requirement: Loop Preserves Outer Registration Order

A `Loop` operator SHALL participate in the parent `RailwayStepsBuilder`'s registration-order guarantee as a single transformer. Operators registered before a `Loop` MUST execute before any body iteration begins; operators registered after a `Loop` MUST execute only after the loop has produced its final `Either`.

#### Scenario: Operators surrounding Loop run in registration order

- **WHEN** a railway registers `Do(a)`, `Loop(...)`, `Do(b)` in that order
- **THEN** `Do(a)` runs to completion before any iteration of the loop starts
- **AND** `Do(b)` runs only after the loop has produced its final `Either`
- **AND** no operator inside the loop body runs outside its enclosing iteration
