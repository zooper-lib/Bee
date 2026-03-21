using System.Threading;
using System.Threading.Tasks;
using Zooper.Fox;

namespace Zooper.Bee.Interfaces;

/// <summary>
/// Base marker interface for all railway guards.
/// </summary>
public interface IRailwayGuard;

/// <summary>
/// Represents a guard that checks if a railway can be executed.
/// </summary>
/// <typeparam name="TRequest">The type of the request.</typeparam>
/// <typeparam name="TError">The type of the error.</typeparam>
public interface IRailwayGuard<in TRequest, TError> : IRailwayGuard
{
	/// <summary>
	/// Checks if the railway can be executed with the given request.
	/// </summary>
	/// <param name="request">The railway request.</param>
	/// <param name="cancellationToken">Token to observe for cancellation.</param>
	/// <returns>Either an error if the guard fails, or Unit if it succeeds.</returns>
	Task<Either<TError, Unit>> ExecuteAsync(
		TRequest request,
		CancellationToken cancellationToken);
}
