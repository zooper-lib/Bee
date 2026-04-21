# Railway vNext Summary

## Goal

Make Bee railways easier to read by making step semantics explicit.

## Core Direction

- Keep the current two-phase split:
  - `Guard()` / `Validate()` before payload creation
  - execution steps after payload creation
- Do not make `Group()` smarter
- Replace vague step semantics with explicit operators that read well in a railway

## Semantic Split

The step phase should separate these concerns clearly:

- `Do`
  - main business-flow step
  - returns the next payload
- `Tap`
  - single pass-through side effect
  - does not return a new payload
- `Effects`
  - grouped `Tap`-style side effects
  - does not change the rail
- `Recover`
  - convert selected `Left` cases into valid `Right` outcomes
- `Branch`
  - conditionally run a right-side sub-pipeline
  - returns the modified payload from the branch
- `Ensure`
  - convert a right-side state into `Left` when a business rule fails

## Side-Effect Policies

- `Tap`
  - strict side effect
  - failure fails the railway
- `Effects`
  - strict grouped side effects
  - failure fails the railway
- `TryTap`
  - best-effort side effect
  - failure does not fail the railway
- `TryEffects`
  - best-effort grouped side effects
  - failure does not fail the railway
- `Detach`
  - background side effects
  - not awaited
  - failure does not fail the railway
- `Finally`
  - always-run lifecycle effect

## Meaning of Left and Right

- `Right` means a valid business or use-case outcome
- `Left` means the current boundary could not produce a valid answer
- “Expected” does not mean “likely” or “common”
- Cases like `NotFound` may belong on `Right` if they are valid domain outcomes
- Technical failures like timeouts or unreachable services remain `Left` unless a higher-level policy translates them into a valid outcome

## API Direction

Prefer explicit methods over one umbrella method:

- keep `Do()` for normal right-side progression
- replace top-level `Group()` usage with explicit `Branch()` semantics
- add first-class `Recover(...)` APIs for left-side continuation
- use `Tap(...)` for a single strict pass-through side effect
- use `Effects(...)` for grouped strict pass-through side effects
- use `TryTap(...)` / `TryEffects(...)` for best-effort side effects
- keep `Detach(...)` for background best-effort side effects
- add `Ensure(...)` for right-side rule enforcement

## Example Mental Model

This should read like:

```csharp
LoadPlanet()
Tap(AuditPlanetRequested)
Branch(if image missing, generate image)
BuildReadModel()
```

Not like:

```csharp
LoadPlanet()
Fail with ImageMissing
Recover ImageMissing
GenerateImage()
```

## Example Railway

```csharp
var railway = Railway.Create<GetPlanetQuery, PlanetContext, PlanetReadModel, PlanetError>(
  factory: query => PlanetContext.For(query.PlanetId),
  selector: ctx => PlanetReadModel.From(ctx),
  guards: g => g
    .Guard(query => RequireAuthenticatedUser(query))
    .Validate(query => ValidateQuery(query)),
  steps: r => r
    .Do(ctx => LoadPlanet(ctx))

    .Tap(ctx => AuditPlanetRequested(ctx))

    .Effects(e => e
      .Do(ctx => WriteRequestMetric(ctx))
      .Do(ctx => TracePlanetLookup(ctx))
    )

    .Recover<PlanetImageTimeoutError>((error, ctx) =>
      ctx.MarkImageAsPending())

    .Branch(
      when: ctx => ctx.Image is null && !ctx.ImagePending,
      branch: b => b
        .Do(ctx => GeneratePlanetImage(ctx))
        .Do(ctx => SavePlanetImage(ctx))
        .Tap(ctx => AuditPlanetImageGenerated(ctx))
    )

    .Ensure(
      when: ctx => !ctx.IsVisible,
      failWith: ctx => new PlanetError.NotVisible(ctx.PlanetId)
    )

    .TryTap(ctx => PublishTelemetry(ctx))

    .TryEffects(e => e
      .Do(ctx => WarmPlanetCache(ctx))
      .Do(ctx => PublishReadModelHint(ctx))
    )

    .Detach(d => d
      .Do(ctx => PublishPlanetViewedEvent(ctx))
      .Do(ctx => RecordAnalytics(ctx))
    )

    .Finally(ctx => RecordCompletionMetric(ctx))
);
```

## Key Distinction

- `Do` answers: what is the next payload?
- `Tap` answers: did the side effect succeed while the same payload continues?
- if removing a step changes the business result, it is `Do`
- if removing a step only removes logging, telemetry, auditing, or notifications, it is `Tap`

## What to Avoid

- avoid a generic “condition on both rails” abstraction
- avoid using `Left` as a branching mechanism for valid domain states
- avoid effect APIs that look like normal transforming steps
- avoid implicit semantics hidden behind `Group()`
- avoid implicit merge behavior where the caller should own the merge policy