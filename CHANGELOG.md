# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

## [4.0.0] - 2026-04-21

### Added

- **New operator vocabulary on `RailwayStepsBuilder`** — explicit Either-flowing pipeline
  - `Do(sync)` / `Do(async)` — transform payload, can return Left to signal failure
  - `Ensure(predicate, failWith)` — assert a condition; returns Left when predicate is false
  - `Branch(when, configure)` — conditional sub-pipeline with its own `Recover` scope; skips on Left
  - `Tap(sync)` / `Tap(async-no-return)` / `Tap(async-Either)` — observe payload; passes through on Right; throws rethrown to caller
  - `TryTap(sync)` / `TryTap(async)` — best-effort observe; exceptions are silently swallowed
  - `Effects(configure)` — strict side-effect group via `EffectsBuilder`; can return Left to fail pipeline
  - `TryEffects(configure)` — best-effort side-effect group; exceptions silently swallowed per-effect
  - `Detach(configure)` — fire-and-forget side effects; never blocks the pipeline; exceptions silently swallowed
  - `Recover<TErr>(sync)` / `Recover<TErr>(async)` — typed error recovery; uses pre-failure payload snapshot; no-op when state is already Right or error type does not match
  - `Finally(sync)` / `Finally(async)` — guaranteed cleanup; runs after all operators regardless of state; multiple Finally activities run independently (exceptions swallowed per-activity)
- **`EffectsBuilder<TPayload, TError>`** — inner builder for `Effects`, `TryEffects`, `Detach`; exposes `Do(Action)`, `Do(async Task)`, `Do(async Task<Either>)`
- **`BranchBuilder<TPayload, TError>`** — inner builder for `Branch`; exposes `Do`, `Tap`, `TryTap`, `Effects`, `TryEffects`, `Recover<TErr>`, `Ensure`
- **Pre-failure payload snapshot** (`lastRight`) — threaded through the executor so `Recover` always receives the payload from before the error occurred
- **`RailwayHandler<TRequest, TPayload, TSuccess, TError>`** updated in `Zooper.Bee.MediatR` — now uses `ConfigureGuards` + `ConfigureSteps` instead of `ConfigureRailway(RailwayBuilder)`

### Changed

- `RailwayStepsBuilder` executor is now **Either-flowing** (no short-circuit at executor level) — each operator receives the full `Either` state and decides whether to act
- `Zooper.Bee.MediatR.WorkflowHandler` now extends `RailwayHandler` and delegates to `ConfigureSteps`

### Removed

- **`RailwayBuilder<TRequest, TPayload, TSuccess, TError>`** and **`RailwayBuilderFactory`** — replaced by `Railway.Create()`
- **`WorkflowBuilder`** and **`WorkflowBuilderFactory`** — use `Railway.Create()` directly
- **`BranchWithLocalPayloadBuilder`** and corresponding internals
- Operators removed from `RailwayStepsBuilder`: `DoIf`, `DoAll`, `Group`, `Parallel`, `ParallelDetached`, `WithContext`, old `Detach(condition, configure)`
- Features removed: `Group`, `Parallel`, `ParallelDetached`, `WithContext` / `Context`, `Detached` (old form)
- Internal executors removed: `GroupExecutor`, `ParallelExecutor`, `ParallelDetachedExecutor`, `ContextExecutor`, `DetachedExecutor`, `FeatureExecutorFactory`, `FeatureExecutorBase`

### Migration Guide

| v3.x | v4.0 |
|------|------|
| `new RailwayBuilder<Req, Pay, Succ, Err>(factory, selector)` | `Railway.Create<Req, Pay, Succ, Err>(factory, selector, steps => ...)` |
| `.Do(p => Either...)` | `.Do(p => Either...)` *(unchanged)* |
| `.DoIf(when, step)` | `.Branch(when, b => b.Do(step))` |
| `.Group(when, b => b.Do(...))` | `.Branch(when, b => b.Do(...))` |
| `.Parallel(...)` / `.ParallelDetached(...)` | `.Detach(eff => eff.Do(...))` |
| `.WithContext(...)` | embed state directly in your payload record |
| `.Detach(condition, configure)` | `.Branch(condition, b => b)` + `.Detach(configure)` |
| `ConfigureRailway(RailwayBuilder)` in MediatR | `ConfigureSteps(RailwayStepsBuilder)` |

## [3.5.0] - 2026-04-01

### Added

- **`Railway.Create()` — two-phase builder factory** that enforces a clear separation between the
  guard/validation phase and the step execution phase at the type-system level
  - `Railway.Create(factory, selector, guards, steps)` — with guards
  - `Railway.Create(factory, selector, steps)` — without guards (convenience overload)
  - Parameterless variants (`Func<TPayload>` factory) for railways with no request input
  - New `RailwayGuardBuilder<...>` — only exposes `Guard()` and `Validate()`
  - New `RailwayStepsBuilder<...>` — only exposes `Do()`, `DoIf()`, `DoAll()`, `Group()`,
    `WithContext()`, `Detach()`, `Parallel()`, `ParallelDetached()`, `Finally()`, and `Build()`

### Fixed

- **Registration-order bug**: `Group()`, `WithContext()`, `Detach()`, `Parallel()`, and
  `ParallelDetached()` steps now execute in the exact order they were registered, interleaved
  correctly with `Do()` steps. Previously all `Do()` steps ran before all feature steps
  regardless of registration order.

### Deprecated

- `RailwayBuilder<TRequest, TPayload, TSuccess, TError>` — use `Railway.Create()` instead
- `RailwayBuilderFactory` — use `Railway.Create()` instead

## [3.4.1] - 2026-03-21

### Changed

- **Renamed all `Workflow` classes to `Railway`** to better reflect the railway-oriented programming pattern
  - `Workflow<TRequest, TSuccess, TError>` -> `Railway<TRequest, TSuccess, TError>`
  - `WorkflowBuilder<...>` -> `RailwayBuilder<...>`
  - `WorkflowBuilderFactory` -> `RailwayBuilderFactory`
  - `CreateWorkflow<...>()` -> `CreateRailway<...>()`
  - `IWorkflowStep` -> `IRailwayStep`
  - `IWorkflowValidation` -> `IRailwayValidation`
  - `IWorkflowGuard` -> `IRailwayGuard`
  - `AddWorkflows()` -> `AddRailways()`
  - `AddWorkflowSteps()` -> `AddRailwaySteps()`
- All old `Workflow` names are preserved as `[Obsolete]` shims for backward compatibility
- Updated all example files to use the new Railway terminology

## [3.4.0] - 2026-03-10

### Added

- New `IWorkflowStep` and `IWorkflowStep<TPayload, TError>` interfaces to replace `IWorkflowActivity`
- New `IWorkflowSteps` and `IWorkflowSteps<TPayload, TError>` interfaces to replace `IWorkflowActivities`
- New `WorkflowStepsExtensions` class with `AddWorkflowSteps*` extension methods for dependency injection

### Deprecated

- `IWorkflowActivity` and `IWorkflowActivity<TPayload, TError>` — use `IWorkflowStep` / `IWorkflowStep<TPayload, TError>` instead
- `IWorkflowActivities` and `IWorkflowActivities<TPayload, TError>` — use `IWorkflowSteps` / `IWorkflowSteps<TPayload, TError>` instead
- `WorkflowActivitiesExtensions` class and all `AddWorkflowActivities*` methods — use `WorkflowStepsExtensions` and `AddWorkflowSteps*` instead

### Compatibility

- All existing code using the deprecated types and methods will continue to work but will show deprecation warnings
- To migrate, replace:

  ```csharp
  services.AddWorkflowActivities();
  ```

  With:

  ```csharp
  services.AddWorkflowSteps();
  ```

  And replace interface implementations:

  ```csharp
  public class MyStep : IWorkflowActivity<MyPayload, MyError>
  ```

  With:

  ```csharp
  public class MyStep : IWorkflowStep<MyPayload, MyError>
  ```

## 3.3.0 - 2025.04.24

### Added

- New guards feature for verifying workflow execution requirements
    - Added `Guard` methods to check if a workflow can be executed
    - Guards run after validations and before Activities and provide early termination

- New component interfaces for dependency injection and workflow composition
    - Added `IWorkflowGuard` and `IWorkflowGuard<TRequest, TError>` interfaces
    - Added `IWorkflowGuards` and `IWorkflowGuards<TRequest, TError>` interfaces

- New extension methods for registering guards with dependency injection
    - Added `AddWorkflowGuards()` for registering workflow guards

## 3.2.1 - 2025.04.24

### Modified

- Code readability improvements in `WorkflowBuilder`

## 3.2.0 - 2025-04-24

### Added

- New project `Zooper.Bee.MediatR` for MediatR integration
- New `WorkflowHandler<TRequest, TPayload, TSuccess, TError>` base class for defining workflows as MediatR handlers
- Support for creating dedicated workflow classes that integrate with MediatR's request/response pattern

## [3.1.0] - 2025-04-23

### Added

- Added new extension methods for dependency injection:
    - `AddWorkflows()` - Registers all workflow components (validations, activities, and workflows)
    - `AddWorkflowValidations()` - Registers workflow validations only
    - `AddWorkflowActivities()` - Registers workflow activities only

- Added support for automatic assembly scanning to discover workflow components
- Added the ability to specify service lifetime for workflow registrations

## [3.0.0] - 2025-05-01

### Added

- Complete redesign of the workflow feature API
    - New `Group` method that groups activities with an optional condition
    - New `WithContext` method for isolated contexts with their own local state
    - New `Detach` method for executing activities without merging their results back
    - New `Parallel` method for parallel execution of multiple groups
    - New `ParallelDetached` method for parallel execution of detached activities
- Better support for nullable conditions - all new methods accept nullable condition
- Clear separation of merged and non-merged execution paths
- Improved naming consistency across the API

### Changed

- **BREAKING CHANGE**: Reorganized internal class structure
    - Added feature-specific namespaces and folders
    - Created a consistent `IWorkflowFeature` interface for all features
- **BREAKING CHANGE**: Renamed `Branch` to `Group` for better clarity
- **BREAKING CHANGE**: Renamed `BranchWithLocalPayload` to `WithContext` to better express intention

### Deprecated

- The old `Branch` method is now marked as obsolete and will be removed in a future version
- The old `BranchWithLocalPayload` method is now marked as obsolete and will be removed in a future version

### Compatibility

- All existing code using the deprecated methods will continue to work but will show deprecation warnings
- To migrate, replace:

  ```csharp
  .Branch(condition, branch => branch.Do(...))
  ```

  With:

  ```csharp
  .Group(condition, group => group.Do(...))
  ```

  And replace:

  ```csharp
  .BranchWithLocalPayload(condition, factory, branch => branch.Do(...))
  ```

  With:

  ```csharp
  .WithContext(condition, factory, context => context.Do(...))
  ```

## [2.2.0] - 2025-04-25

### Added

- Added unconditional branch with local payload method overload
    - New `BranchWithLocalPayload` method that doesn't require a condition parameter and always executes
    - Supports both callback-style API and fluent API patterns:
      `.BranchWithLocalPayload(localPayloadFactory, branch => { ... })`

## [2.1.0] - 2025-04-21

### Added

- Added support for branches with isolated local payloads
    - New `BranchWithLocalPayload` method that allows branches to use their own payload type
    - Activities within these branches can access and modify both the main payload and the local payload
    - Local payloads are isolated to their branch and don't affect other parts of the workflow

## [2.0.0] - 2025-04-19

### Added

- Added an improved API for workflow branching that doesn't require `.EndBranch()` calls
    - New `Branch` method overload that accepts a configuration action: `.Branch(condition, branchBuilder => { ... })`
    - New `Branch` method overload without a condition for logical grouping of activities:
      `.Branch(branchBuilder => { ... })`

### Changed

- **BREAKING CHANGE**: Fixed XML documentation warnings in `WorkflowBuilder`
    - Removed unnecessary param tags from class-level documentation

### Deprecated

- The `.EndBranch()` method in `BranchBuilder` is now considered deprecated and will be removed in a future version
    - Use the new callback-style API for defining branches instead

### Compatibility

- All existing code using the fluent API with `.EndBranch()` continues to work, but will produce a deprecation warning
- To migrate, replace:

  ```csharp
  .Branch(condition)
      .Do(action1)
      .Do(action2)
  .EndBranch()
  ```

  With:

  ```csharp
  .Branch(condition, branch => branch
      .Do(action1)
      .Do(action2)
  )
  ```

## [1.0.0] - 2025-04-18

### Added

- Initial release of Zooper.Bee workflow library
- Core workflow builder with fluent API
- Support for activities, validations, and conditional branches
- Finally blocks that always execute
