namespace Zooper.Bee.Interfaces;

/// <summary>
/// Base marker interface for workflow step collections
/// </summary>
public interface IWorkflowSteps;

/// <summary>
/// Interface for a collection of workflow steps operating on the same payload and error types
/// </summary>
/// <typeparam name="TPayload">The type of payload the steps process</typeparam>
/// <typeparam name="TError">The type of error the steps might return</typeparam>
public interface IWorkflowSteps<TPayload, TError> : IWorkflowSteps;
