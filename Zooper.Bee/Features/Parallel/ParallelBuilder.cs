using System;
using Zooper.Bee.Features.Group;

namespace Zooper.Bee.Features.Parallel;

/// <summary>
/// Builder for configuring parallel execution of multiple groups.
/// </summary>
/// <typeparam name="TRequest">The type of the request input</typeparam>
/// <typeparam name="TPayload">The type of the main workflow payload</typeparam>
/// <typeparam name="TSuccess">The type of the success result</typeparam>
/// <typeparam name="TError">The type of the error result</typeparam>
public sealed class ParallelBuilder<TRequest, TPayload, TSuccess, TError>
{
	private readonly WorkflowBuilder<TRequest, TPayload, TSuccess, TError> _workflow;
	private readonly Parallel<TPayload, TError> _parallel;

	internal ParallelBuilder(
		WorkflowBuilder<TRequest, TPayload, TSuccess, TError> workflow,
		Parallel<TPayload, TError> parallel)
	{
		_workflow = workflow;
		_parallel = parallel;
	}

	/// <summary>
	/// Adds a group to be executed in parallel.
	/// </summary>
	/// <param name="groupConfiguration">The configuration for the group</param>
	/// <returns>The parallel builder for fluent chaining</returns>
	public ParallelBuilder<TRequest, TPayload, TSuccess, TError> Group(
		Action<GroupBuilder<TRequest, TPayload, TSuccess, TError>> groupConfiguration)
	{
		var group = new Group<TPayload, TError>();
		_parallel.Groups.Add(group);

		var groupBuilder = new GroupBuilder<TRequest, TPayload, TSuccess, TError>(_workflow, group);
		groupConfiguration(groupBuilder);

		return this;
	}

	/// <summary>
	/// Adds a conditional group to be executed in parallel.
	/// </summary>
	/// <param name="condition">The condition that determines if the group should execute</param>
	/// <param name="groupConfiguration">The configuration for the group</param>
	/// <returns>The parallel builder for fluent chaining</returns>
	public ParallelBuilder<TRequest, TPayload, TSuccess, TError> Group(
		Func<TPayload, bool> condition,
		Action<GroupBuilder<TRequest, TPayload, TSuccess, TError>> groupConfiguration)
	{
		var group = new Group<TPayload, TError>(condition);
		_parallel.Groups.Add(group);

		var groupBuilder = new GroupBuilder<TRequest, TPayload, TSuccess, TError>(_workflow, group);
		groupConfiguration(groupBuilder);

		return this;
	}
}