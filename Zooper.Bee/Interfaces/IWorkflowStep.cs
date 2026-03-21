using System;

namespace Zooper.Bee.Interfaces;

/// <summary>
/// Base marker interface for all workflow steps
/// </summary>
[Obsolete("Use IRailwayStep instead. This interface will be removed in a future version.")]
public interface IWorkflowStep : IRailwayStep;

/// <summary>
/// Interface for a step that works with a specific payload and error type
/// </summary>
/// <typeparam name="TPayload">The type of payload the step processes</typeparam>
/// <typeparam name="TError">The type of error the step might return</typeparam>
[Obsolete("Use IRailwayStep<TPayload, TError> instead. This interface will be removed in a future version.")]
public interface IWorkflowStep<TPayload, TError> : IRailwayStep<TPayload, TError>;
