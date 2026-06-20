using System;
using System.Threading;
using System.Threading.Tasks;
using Zooper.Fox;

namespace Zooper.Bee.Internal;

/// <summary>
/// Represents a guard that checks if a railway can be executed.
/// </summary>
/// <typeparam name="TRequest">Type of the request</typeparam>
/// <typeparam name="TError">Type of the error</typeparam>
internal sealed class RailwayGuard<TRequest, TError>
{
	private readonly Func<TRequest, CancellationToken, Task<Either<TError, Unit>>> _condition;
	private readonly string? _name;

	/// <summary>
	/// Optional predicate over the request that gates whether this guard runs.
	/// When <c>null</c>, the guard always runs. When non-null and it returns <c>false</c>,
	/// the guard is skipped and treated as a pass.
	/// </summary>
	public Func<TRequest, CancellationToken, Task<bool>>? When { get; }

	public RailwayGuard(
		Func<TRequest, CancellationToken, Task<Either<TError, Unit>>> condition,
		Func<TRequest, CancellationToken, Task<bool>>? when = null,
		string? name = null)
	{
		_condition = condition;
		When = when;
		_name = name;
	}

	public Task<Either<TError, Unit>> Check(
		TRequest request,
		CancellationToken token)
	{
		return _condition(request, token);
	}
}
