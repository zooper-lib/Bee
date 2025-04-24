namespace Zooper.Bee.Interfaces;

/// <summary>
/// Base marker interface for all workflow guards.
/// </summary>
public interface IWorkflowGuards;

/// <summary>
/// Interface for a collection of workflow guards operating on the same request and error types.
/// </summary>
/// <typeparam name="TRequest">The type of the request.</typeparam>
/// <typeparam name="TError">The type of the error.</typeparam>
public interface IWorkflowGuards<TRequest, TError> : IWorkflowGuards;