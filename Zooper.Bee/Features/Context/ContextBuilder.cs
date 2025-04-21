using System;
using System.Threading;
using System.Threading.Tasks;
using Zooper.Fox;

namespace Zooper.Bee.Features.Context;

/// <summary>
/// Builder for a context with a local state that enables a fluent API for adding activities.
/// </summary>
/// <typeparam name="TRequest">The type of the request input</typeparam>
/// <typeparam name="TPayload">The type of the main workflow payload</typeparam>
/// <typeparam name="TLocalState">The type of the local context state</typeparam>
/// <typeparam name="TSuccess">The type of the success result</typeparam>
/// <typeparam name="TError">The type of the error result</typeparam>
public sealed class ContextBuilder<TRequest, TPayload, TLocalState, TSuccess, TError>
{
	private readonly WorkflowBuilder<TRequest, TPayload, TSuccess, TError> _workflow;
	private readonly Context<TPayload, TLocalState, TError> _context;

	internal ContextBuilder(
		WorkflowBuilder<TRequest, TPayload, TSuccess, TError> workflow,
		Context<TPayload, TLocalState, TError> context)
	{
		_workflow = workflow;
		_context = context;
	}

	/// <summary>
	/// Adds an activity to the context that operates on both the main payload and local state.
	/// </summary>
	/// <param name="activity">The activity to add</param>
	/// <returns>The context builder for fluent chaining</returns>
	public ContextBuilder<TRequest, TPayload, TLocalState, TSuccess, TError> Do(
		Func<TPayload, TLocalState, CancellationToken, Task<Either<TError, (TPayload MainPayload, TLocalState LocalState)>>> activity)
	{
		_context.Activities.Add(new ContextActivity<TPayload, TLocalState, TError>(activity));
		return this;
	}

	/// <summary>
	/// Adds a synchronous activity to the context that operates on both the main payload and local state.
	/// </summary>
	/// <param name="activity">The activity to add</param>
	/// <returns>The context builder for fluent chaining</returns>
	public ContextBuilder<TRequest, TPayload, TLocalState, TSuccess, TError> Do(
		Func<TPayload, TLocalState, Either<TError, (TPayload MainPayload, TLocalState LocalState)>> activity)
	{
		_context.Activities.Add(new ContextActivity<TPayload, TLocalState, TError>(
			(mainPayload, localState, _) => Task.FromResult(activity(mainPayload, localState))
		));
		return this;
	}

	/// <summary>
	/// Adds multiple activities to the context.
	/// </summary>
	/// <param name="activities">The activities to add</param>
	/// <returns>The context builder for fluent chaining</returns>
	public ContextBuilder<TRequest, TPayload, TLocalState, TSuccess, TError> DoAll(
		params Func<TPayload, TLocalState, CancellationToken, Task<Either<TError, (TPayload MainPayload, TLocalState LocalState)>>>[] activities)
	{
		foreach (var activity in activities)
		{
			_context.Activities.Add(new ContextActivity<TPayload, TLocalState, TError>(activity));
		}
		return this;
	}

	/// <summary>
	/// Adds multiple synchronous activities to the context.
	/// </summary>
	/// <param name="activities">The activities to add</param>
	/// <returns>The context builder for fluent chaining</returns>
	public ContextBuilder<TRequest, TPayload, TLocalState, TSuccess, TError> DoAll(
		params Func<TPayload, TLocalState, Either<TError, (TPayload MainPayload, TLocalState LocalState)>>[] activities)
	{
		foreach (var activity in activities)
		{
			_context.Activities.Add(new ContextActivity<TPayload, TLocalState, TError>(
				(mainPayload, localState, _) => Task.FromResult(activity(mainPayload, localState))
			));
		}
		return this;
	}
}