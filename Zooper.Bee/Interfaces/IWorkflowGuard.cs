using System.Threading;
using System.Threading.Tasks;
using Zooper.Fox;

namespace Zooper.Bee.Interfaces;

/// <summary>
/// Base marker interface for all workflow guards.
/// </summary>
public interface IWorkflowGuard;

/// <summary>
/// Represents a guard that checks if a workflow can be executed.
/// </summary>
/// <typeparam name="TRequest">The type of the request.</typeparam>
/// <typeparam name="TError">The type of the error.</typeparam>
public interface IWorkflowGuard<in TRequest, TError> : IWorkflowGuard
{
	/// <summary>
	/// Checks if the workflow can be executed with the given request.
	/// </summary>
	/// <param name="request">The workflow request.</param>
	/// <param name="cancellationToken">Token to observe for cancellation.</param>
	/// <returns>Either an error if the guard fails, or Unit if it succeeds.</returns>
	Task<Either<TError, Unit>> ExecuteAsync(
		TRequest request,
		CancellationToken cancellationToken);
}