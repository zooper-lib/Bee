using System;
using System.Threading;
using System.Threading.Tasks;
using Zooper.Bee.Internal;
using Zooper.Fox;

namespace Zooper.Bee;

/// <summary>
/// Builder for a branch that enables a fluent API for adding activities to a branch.
/// </summary>
public sealed class BranchBuilder<TRequest, TPayload, TSuccess, TError>
{
	private readonly WorkflowBuilder<TRequest, TPayload, TSuccess, TError> _workflow;
	private readonly Branch<TPayload, TError> _branch;

	internal BranchBuilder(
		WorkflowBuilder<TRequest, TPayload, TSuccess, TError> workflow,
		Branch<TPayload, TError> branch)
	{
		_workflow = workflow;
		_branch = branch;
	}

	/// <summary>
	/// Adds an activity to the branch.
	/// </summary>
	/// <param name="activity">The activity to add</param>
	/// <returns>The branch builder for fluent chaining</returns>
	public BranchBuilder<TRequest, TPayload, TSuccess, TError> Do(Func<TPayload, CancellationToken, Task<Either<TError, TPayload>>> activity)
	{
		_branch.Activities.Add(new WorkflowActivity<TPayload, TError>(activity));
		return this;
	}

	/// <summary>
	/// Adds a synchronous activity to the branch.
	/// </summary>
	/// <param name="activity">The activity to add</param>
	/// <returns>The branch builder for fluent chaining</returns>
	public BranchBuilder<TRequest, TPayload, TSuccess, TError> Do(Func<TPayload, Either<TError, TPayload>> activity)
	{
		_branch.Activities.Add(new WorkflowActivity<TPayload, TError>((payload, _) =>
			Task.FromResult(activity(payload))
		));
		return this;
	}

	/// <summary>
	/// Adds multiple activities to the branch.
	/// </summary>
	/// <param name="activities">The activities to add</param>
	/// <returns>The branch builder for fluent chaining</returns>
	public BranchBuilder<TRequest, TPayload, TSuccess, TError> DoAll(params Func<TPayload, CancellationToken, Task<Either<TError, TPayload>>>[] activities)
	{
		foreach (var activity in activities)
		{
			_branch.Activities.Add(new WorkflowActivity<TPayload, TError>(activity));
		}
		return this;
	}

	/// <summary>
	/// Adds multiple synchronous activities to the branch.
	/// </summary>
	/// <param name="activities">The activities to add</param>
	/// <returns>The branch builder for fluent chaining</returns>
	public BranchBuilder<TRequest, TPayload, TSuccess, TError> DoAll(params Func<TPayload, Either<TError, TPayload>>[] activities)
	{
		foreach (var activity in activities)
		{
			_branch.Activities.Add(new WorkflowActivity<TPayload, TError>((payload, _) =>
				Task.FromResult(activity(payload))
			));
		}
		return this;
	}

	/// <summary>
	/// Returns to the main workflow builder to continue defining the workflow.
	/// </summary>
	/// <returns>The main workflow builder</returns>
	public WorkflowBuilder<TRequest, TPayload, TSuccess, TError> EndBranch()
	{
		return _workflow;
	}
}