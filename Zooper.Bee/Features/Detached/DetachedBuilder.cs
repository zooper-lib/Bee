using System;
using System.Threading;
using System.Threading.Tasks;
using Zooper.Bee.Internal;
using Zooper.Fox;

namespace Zooper.Bee.Features.Detached;

/// <summary>
/// Builder for a detached group that enables a fluent API for adding activities.
/// </summary>
/// <typeparam name="TRequest">The type of the request input</typeparam>
/// <typeparam name="TPayload">The type of the main workflow payload</typeparam>
/// <typeparam name="TSuccess">The type of the success result</typeparam>
/// <typeparam name="TError">The type of the error result</typeparam>
public sealed class DetachedBuilder<TRequest, TPayload, TSuccess, TError>
{
	private readonly WorkflowBuilder<TRequest, TPayload, TSuccess, TError> _workflow;
	private readonly Detached<TPayload, TError> _detached;

	internal DetachedBuilder(
		WorkflowBuilder<TRequest, TPayload, TSuccess, TError> workflow,
		Detached<TPayload, TError> detached)
	{
		_workflow = workflow;
		_detached = detached;
	}

	/// <summary>
	/// Adds an activity to the detached group.
	/// </summary>
	/// <param name="activity">The activity to add</param>
	/// <returns>The detached builder for fluent chaining</returns>
	public DetachedBuilder<TRequest, TPayload, TSuccess, TError> Do(
		Func<TPayload, CancellationToken, Task<Either<TError, TPayload>>> activity)
	{
		_detached.Activities.Add(new WorkflowActivity<TPayload, TError>(activity));
		return this;
	}

	/// <summary>
	/// Adds a synchronous activity to the detached group.
	/// </summary>
	/// <param name="activity">The activity to add</param>
	/// <returns>The detached builder for fluent chaining</returns>
	public DetachedBuilder<TRequest, TPayload, TSuccess, TError> Do(
		Func<TPayload, Either<TError, TPayload>> activity)
	{
		_detached.Activities.Add(new WorkflowActivity<TPayload, TError>(
			(payload, _) => Task.FromResult(activity(payload))
		));
		return this;
	}

	/// <summary>
	/// Adds multiple activities to the detached group.
	/// </summary>
	/// <param name="activities">The activities to add</param>
	/// <returns>The detached builder for fluent chaining</returns>
	public DetachedBuilder<TRequest, TPayload, TSuccess, TError> DoAll(
		params Func<TPayload, CancellationToken, Task<Either<TError, TPayload>>>[] activities)
	{
		foreach (var activity in activities)
		{
			_detached.Activities.Add(new WorkflowActivity<TPayload, TError>(activity));
		}
		return this;
	}

	/// <summary>
	/// Adds multiple synchronous activities to the detached group.
	/// </summary>
	/// <param name="activities">The activities to add</param>
	/// <returns>The detached builder for fluent chaining</returns>
	public DetachedBuilder<TRequest, TPayload, TSuccess, TError> DoAll(
		params Func<TPayload, Either<TError, TPayload>>[] activities)
	{
		foreach (var activity in activities)
		{
			_detached.Activities.Add(new WorkflowActivity<TPayload, TError>(
				(payload, _) => Task.FromResult(activity(payload))
			));
		}
		return this;
	}
}