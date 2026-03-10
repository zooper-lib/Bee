using System;
using System.Threading;
using System.Threading.Tasks;
using Zooper.Fox;

namespace Zooper.Bee.Internal;

/// <summary>
/// Represents a step in the workflow that operates on a payload.
/// </summary>
/// <typeparam name="TPayload">Type of the payload</typeparam>
/// <typeparam name="TError">Type of the error</typeparam>
internal sealed class WorkflowStep<TPayload, TError>
{
	private readonly Func<TPayload, CancellationToken, Task<Either<TError, TPayload>>> _activity;
	private readonly string? _name;

	public WorkflowStep(
		Func<TPayload, CancellationToken, Task<Either<TError, TPayload>>> activity,
		string? name = null)
	{
		_activity = activity;
		_name = name;
	}

	public Task<Either<TError, TPayload>> Execute(TPayload payload, CancellationToken token)
	{
		return _activity(payload, token);
	}
}
