using System;
using System.Threading;
using System.Threading.Tasks;
using Zooper.Bee.Internal;
using Zooper.Fox;

namespace Zooper.Bee;

/// <summary>
/// Builder for a branch with a local payload that enables a fluent API for adding activities.
/// </summary>
/// <typeparam name="TRequest">The type of the request input</typeparam>
/// <typeparam name="TPayload">The type of the main workflow payload</typeparam>
/// <typeparam name="TLocalPayload">The type of the local branch payload</typeparam>
/// <typeparam name="TSuccess">The type of the success result</typeparam>
/// <typeparam name="TError">The type of the error result</typeparam>
public sealed class BranchWithLocalPayloadBuilder<TRequest, TPayload, TLocalPayload, TSuccess, TError>
{
	private readonly WorkflowBuilder<TRequest, TPayload, TSuccess, TError> _workflow;
	private readonly BranchWithLocalPayload<TPayload, TLocalPayload, TError> _branch;

	internal BranchWithLocalPayloadBuilder(
		WorkflowBuilder<TRequest, TPayload, TSuccess, TError> workflow,
		BranchWithLocalPayload<TPayload, TLocalPayload, TError> branch)
	{
		_workflow = workflow;
		_branch = branch;
	}

	/// <summary>
	/// Adds an activity to the branch that operates on both the main and local payloads.
	/// </summary>
	/// <param name="activity">The activity to add</param>
	/// <returns>The branch builder for fluent chaining</returns>
	public BranchWithLocalPayloadBuilder<TRequest, TPayload, TLocalPayload, TSuccess, TError> Do(
		Func<TPayload, TLocalPayload, CancellationToken, Task<Either<TError, (TPayload MainPayload, TLocalPayload LocalPayload)>>> activity)
	{
		_branch.Activities.Add(new BranchActivity<TPayload, TLocalPayload, TError>(activity));
		return this;
	}

	/// <summary>
	/// Adds a synchronous activity to the branch that operates on both the main and local payloads.
	/// </summary>
	/// <param name="activity">The activity to add</param>
	/// <returns>The branch builder for fluent chaining</returns>
	public BranchWithLocalPayloadBuilder<TRequest, TPayload, TLocalPayload, TSuccess, TError> Do(
		Func<TPayload, TLocalPayload, Either<TError, (TPayload MainPayload, TLocalPayload LocalPayload)>> activity)
	{
		_branch.Activities.Add(new BranchActivity<TPayload, TLocalPayload, TError>(
			(mainPayload, localPayload, _) => Task.FromResult(activity(mainPayload, localPayload))
		));
		return this;
	}

	/// <summary>
	/// Adds multiple activities to the branch.
	/// </summary>
	/// <param name="activities">The activities to add</param>
	/// <returns>The branch builder for fluent chaining</returns>
	public BranchWithLocalPayloadBuilder<TRequest, TPayload, TLocalPayload, TSuccess, TError> DoAll(
		params Func<TPayload, TLocalPayload, CancellationToken, Task<Either<TError, (TPayload MainPayload, TLocalPayload LocalPayload)>>>[] activities)
	{
		foreach (var activity in activities)
		{
			_branch.Activities.Add(new BranchActivity<TPayload, TLocalPayload, TError>(activity));
		}
		return this;
	}

	/// <summary>
	/// Adds multiple synchronous activities to the branch.
	/// </summary>
	/// <param name="activities">The activities to add</param>
	/// <returns>The branch builder for fluent chaining</returns>
	public BranchWithLocalPayloadBuilder<TRequest, TPayload, TLocalPayload, TSuccess, TError> DoAll(
		params Func<TPayload, TLocalPayload, Either<TError, (TPayload MainPayload, TLocalPayload LocalPayload)>>[] activities)
	{
		foreach (var activity in activities)
		{
			_branch.Activities.Add(new BranchActivity<TPayload, TLocalPayload, TError>(
				(mainPayload, localPayload, _) => Task.FromResult(activity(mainPayload, localPayload))
			));
		}
		return this;
	}
}