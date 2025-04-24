using System;
using System.Threading;
using System.Threading.Tasks;
using Zooper.Fox;

namespace Zooper.Bee.Internal;

/// <summary>
/// Represents a guard that checks if a workflow can be executed.
/// </summary>
/// <typeparam name="TRequest">Type of the request</typeparam>
/// <typeparam name="TError">Type of the error</typeparam>
internal sealed class WorkflowGuard<TRequest, TError>
{
	private readonly Func<TRequest, CancellationToken, Task<Either<TError, Unit>>> _condition;
	private readonly string? _name;

	public WorkflowGuard(
		Func<TRequest, CancellationToken, Task<Either<TError, Unit>>> condition,
		string? name = null)
	{
		_condition = condition;
		_name = name;
	}

	public Task<Either<TError, Unit>> Check(
		TRequest request,
		CancellationToken token)
	{
		return _condition(request, token);
	}
}