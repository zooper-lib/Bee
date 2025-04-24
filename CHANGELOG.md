# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

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
