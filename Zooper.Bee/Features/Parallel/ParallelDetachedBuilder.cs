using System;
using Zooper.Bee.Features.Detached;

namespace Zooper.Bee.Features.Parallel;

/// <summary>
/// Builder for configuring parallel execution of multiple detached groups.
/// </summary>
/// <typeparam name="TRequest">The type of the request input</typeparam>
/// <typeparam name="TPayload">The type of the main workflow payload</typeparam>
/// <typeparam name="TSuccess">The type of the success result</typeparam>
/// <typeparam name="TError">The type of the error result</typeparam>
public sealed class ParallelDetachedBuilder<TRequest, TPayload, TSuccess, TError>
{
	private readonly WorkflowBuilder<TRequest, TPayload, TSuccess, TError> _workflow;
	private readonly ParallelDetached<TPayload, TError> _parallelDetached;

	internal ParallelDetachedBuilder(
		WorkflowBuilder<TRequest, TPayload, TSuccess, TError> workflow,
		ParallelDetached<TPayload, TError> parallelDetached)
	{
		_workflow = workflow;
		_parallelDetached = parallelDetached;
	}

	/// <summary>
	/// Adds a detached group to be executed in parallel.
	/// </summary>
	/// <param name="detachedConfiguration">The configuration for the detached group</param>
	/// <returns>The parallel detached builder for fluent chaining</returns>
	public ParallelDetachedBuilder<TRequest, TPayload, TSuccess, TError> Detached(
		Action<DetachedBuilder<TRequest, TPayload, TSuccess, TError>> detachedConfiguration)
	{
		var detached = new Detached<TPayload, TError>();
		_parallelDetached.DetachedGroups.Add(detached);

		var detachedBuilder = new DetachedBuilder<TRequest, TPayload, TSuccess, TError>(_workflow, detached);
		detachedConfiguration(detachedBuilder);

		return this;
	}

	/// <summary>
	/// Adds a conditional detached group to be executed in parallel.
	/// </summary>
	/// <param name="condition">The condition that determines if the detached group should execute</param>
	/// <param name="detachedConfiguration">The configuration for the detached group</param>
	/// <returns>The parallel detached builder for fluent chaining</returns>
	public ParallelDetachedBuilder<TRequest, TPayload, TSuccess, TError> Detached(
		Func<TPayload, bool> condition,
		Action<DetachedBuilder<TRequest, TPayload, TSuccess, TError>> detachedConfiguration)
	{
		var detached = new Detached<TPayload, TError>(condition);
		_parallelDetached.DetachedGroups.Add(detached);

		var detachedBuilder = new DetachedBuilder<TRequest, TPayload, TSuccess, TError>(_workflow, detached);
		detachedConfiguration(detachedBuilder);

		return this;
	}
}