# Railway Execution Phases

## Purpose

Define the ordered execution model for a railway: how validation, guarding, and steps phases run, their short-circuit semantics, declaration-order guarantees within a phase, and the builder surfaces that make the phase boundary explicit.

## Requirements

### Requirement: Three ordered execution phases

A railway SHALL execute in three distinct, ordered phases: **Validation**, then **Guarding**, then **Steps**. The phases MUST always run in this order regardless of the order in which the builder surfaces are configured.

#### Scenario: Validation runs before guarding before steps
- **WHEN** a railway has at least one validation, one guard, and one step registered
- **THEN** all validations run first, then all guards, then the steps phase begins

#### Scenario: Phase order is independent of configuration order
- **WHEN** a guard is configured before a validation in the builder
- **THEN** the validation still executes before the guard at run time

### Requirement: Validation phase semantics

The Validation phase SHALL run every registered validation against the request before any guard or step. A validation that returns a failure MUST short-circuit the railway and return that error; the guarding and steps phases MUST NOT run.

#### Scenario: Validation failure short-circuits the railway
- **WHEN** a validation reports an error
- **THEN** the railway returns that error
- **AND** no guards run
- **AND** no steps run

#### Scenario: All validations pass
- **WHEN** every validation reports success
- **THEN** execution proceeds to the Guarding phase

### Requirement: Guarding phase semantics

The Guarding phase SHALL run every registered guard against the request after all validations pass and before any step. A guard that returns a failure MUST short-circuit the railway and return that error; the steps phase MUST NOT run.

#### Scenario: Guard failure short-circuits the railway
- **WHEN** all validations pass and a guard reports an error
- **THEN** the railway returns that error
- **AND** no steps run

#### Scenario: All guards pass
- **WHEN** every guard reports success
- **THEN** execution proceeds to the Steps phase (payload factory and operators run)

### Requirement: Declaration order preserved within a phase

Within a single phase, registered items SHALL execute in the order they were added. The first failing item in declaration order MUST be the error returned for that phase.

#### Scenario: Validations run in declaration order
- **WHEN** two validations are registered and both would fail
- **THEN** the error from the first-registered validation is returned

#### Scenario: Guards run in declaration order
- **WHEN** two guards are registered and both would fail
- **THEN** the error from the first-registered guard is returned

### Requirement: Separated builder surfaces for validation and guarding

The builder API SHALL expose validation registration and guard registration as distinct surfaces so that the phase boundary is explicit in the API shape. Mixing validation and guard registration on a single combined builder that silently ignores declaration order MUST NOT be the supported model.

#### Scenario: Registering a validation
- **WHEN** the caller registers a validation through the validation surface
- **THEN** it executes in the Validation phase

#### Scenario: Registering a guard
- **WHEN** the caller registers a guard through the guard surface
- **THEN** it executes in the Guarding phase

#### Scenario: Phases are optional
- **WHEN** a railway registers no validations and no guards
- **THEN** the railway executes the Steps phase directly with no error
