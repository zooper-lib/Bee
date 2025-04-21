using System;
using System.Threading;
using System.Threading.Tasks;
using Zooper.Fox;

namespace Zooper.Bee.Features.Context;

/// <summary>
/// Represents an activity in a context that operates on both the main workflow payload and a local state.
/// </summary>
/// <typeparam name="TPayload">Type of the main workflow payload</typeparam>
/// <typeparam name="TLocalState">Type of the local context state</typeparam>
/// <typeparam name="TError">Type of the error</typeparam>
internal sealed class ContextActivity<TPayload, TLocalState, TError>
{
	private readonly Func<TPayload, TLocalState, CancellationToken, Task<Either<TError, (TPayload MainPayload, TLocalState LocalState)>>> _activity;
	private readonly string? _name;

	/// <summary>
	/// Creates a new context activity.
	/// </summary>
	/// <param name="activity">The activity function that operates on both the main payload and local state</param>
	/// <param name="name">Optional name for the activity</param>
	public ContextActivity(
		Func<TPayload, TLocalState, CancellationToken, Task<Either<TError, (TPayload MainPayload, TLocalState LocalState)>>> activity,
		string? name = null)
	{
		_activity = activity ?? throw new ArgumentNullException(nameof(activity));
		_name = name;
	}

	/// <summary>
	/// Executes the activity with the provided payloads.
	/// </summary>
	/// <param name="mainPayload">The main workflow payload</param>
	/// <param name="localState">The local context state</param>
	/// <param name="token">Cancellation token</param>
	/// <returns>Either an error or the updated payload and state</returns>
	public Task<Either<TError, (TPayload MainPayload, TLocalState LocalState)>> Execute(
		TPayload mainPayload,
		TLocalState localState,
		CancellationToken token)
	{
		return _activity(mainPayload, localState, token);
	}
}