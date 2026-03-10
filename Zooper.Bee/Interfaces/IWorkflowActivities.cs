using System;

namespace Zooper.Bee.Interfaces;

/// <summary>
/// Base marker interface for workflow activity collections
/// </summary>
[Obsolete("Use IWorkflowSteps instead. This interface will be removed in a future version.")]
public interface IWorkflowActivities : IWorkflowSteps;

/// <summary>
/// Interface for a collection of workflow activities operating on the same payload and error types
/// </summary>
/// <typeparam name="TPayload">The type of payload the activities process</typeparam>
/// <typeparam name="TError">The type of error the activities might return</typeparam>
[Obsolete("Use IWorkflowSteps<TPayload, TError> instead. This interface will be removed in a future version.")]
public interface IWorkflowActivities<TPayload, TError> : IWorkflowSteps<TPayload, TError>;
