using System;
using Zooper.Fox;

namespace Zooper.Bee;

/// <summary>
/// Provides factory methods for creating workflows without requiring a request parameter.
/// </summary>
public static class WorkflowBuilderFactory
{
	/// <summary>
	/// Creates a new workflow builder that doesn't require a request parameter.
	/// </summary>
	/// <typeparam name="TPayload">The type of payload that will be used throughout the workflow</typeparam>
	/// <typeparam name="TSuccess">The type of the success result</typeparam>
	/// <typeparam name="TError">The type of the error result</typeparam>
	/// <param name="payloadFactory">A factory function that creates the initial payload</param>
	/// <param name="resultSelector">A function that creates the success result from the final payload</param>
	/// <returns>A workflow builder instance</returns>
	public static WorkflowBuilder<Unit, TPayload, TSuccess, TError> Create<TPayload, TSuccess, TError>(
		Func<TPayload> payloadFactory,
		Func<TPayload, TSuccess> resultSelector)
	{
		return new WorkflowBuilder<Unit, TPayload, TSuccess, TError>(
			_ => payloadFactory(),
			resultSelector);
	}

	/// <summary>
	/// Creates a new workflow that doesn't require a request parameter.
	/// </summary>
	/// <typeparam name="TPayload">The type of payload that will be used throughout the workflow</typeparam>
	/// <typeparam name="TSuccess">The type of the success result</typeparam>
	/// <typeparam name="TError">The type of the error result</typeparam>
	/// <param name="payloadFactory">A factory function that creates the initial payload</param>
	/// <param name="resultSelector">A function that creates the success result from the final payload</param>
	/// <param name="configure">An action that configures the workflow</param>
	/// <returns>A workflow instance</returns>
	public static Workflow<Unit, TSuccess, TError> CreateWorkflow<TPayload, TSuccess, TError>(
		Func<TPayload> payloadFactory,
		Func<TPayload, TSuccess> resultSelector,
		Action<WorkflowBuilder<Unit, TPayload, TSuccess, TError>> configure)
	{
		var builder = new WorkflowBuilder<Unit, TPayload, TSuccess, TError>(
			_ => payloadFactory(),
			resultSelector);

		configure(builder);
		return builder.Build();
	}
}