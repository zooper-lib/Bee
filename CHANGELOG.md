# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

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
  - New `Branch` method overload without a condition for logical grouping of activities: `.Branch(branchBuilder => { ... })`

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
