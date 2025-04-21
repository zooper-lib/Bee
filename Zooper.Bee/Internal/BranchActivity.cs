using System;
using System.Threading;
using System.Threading.Tasks;
using Zooper.Fox;

namespace Zooper.Bee.Internal;

/// <summary>
/// Represents an activity in a branch that operates on both the main workflow payload and a local branch payload.
/// </summary>
/// <typeparam name="TPayload">Type of the main workflow payload</typeparam>
/// <typeparam name="TLocalPayload">Type of the local branch payload</typeparam>
/// <typeparam name="TError">Type of the error</typeparam>
internal sealed class BranchActivity<TPayload, TLocalPayload, TError>
{
	private readonly Func<TPayload, TLocalPayload, CancellationToken, Task<Either<TError, (TPayload MainPayload, TLocalPayload LocalPayload)>>> _activity;
	private readonly string? _name;

	/// <summary>
	/// Creates a new branch activity.
	/// </summary>
	/// <param name="activity">The activity function that operates on both payloads</param>
	/// <param name="name">Optional name for the activity</param>
	public BranchActivity(
		Func<TPayload, TLocalPayload, CancellationToken, Task<Either<TError, (TPayload MainPayload, TLocalPayload LocalPayload)>>> activity,
		string? name = null)
	{
		_activity = activity;
		_name = name;
	}

	/// <summary>
	/// Executes the activity with the provided payloads.
	/// </summary>
	/// <param name="mainPayload">The main workflow payload</param>
	/// <param name="localPayload">The local branch payload</param>
	/// <param name="token">Cancellation token</param>
	/// <returns>Either an error or the updated payloads</returns>
	public Task<Either<TError, (TPayload MainPayload, TLocalPayload LocalPayload)>> Execute(
		TPayload mainPayload,
		TLocalPayload localPayload,
		CancellationToken token)
	{
		return _activity(mainPayload, localPayload, token);
	}
}