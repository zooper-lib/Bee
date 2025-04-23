namespace Zooper.Bee.Interfaces;

/// <summary>
/// Base marker interface for workflow activity collections
/// </summary>
public interface IWorkflowActivities;

/// <summary>
/// Interface for a collection of workflow activities operating on the same payload and error types
/// </summary>
/// <typeparam name="TPayload">The type of payload the activities process</typeparam>
/// <typeparam name="TError">The type of error the activities might return</typeparam>
public interface IWorkflowActivities<TPayload, TError> : IWorkflowActivities;