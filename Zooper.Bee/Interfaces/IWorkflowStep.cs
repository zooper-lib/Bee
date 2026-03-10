using System.Threading;
using System.Threading.Tasks;
using Zooper.Fox;

namespace Zooper.Bee.Interfaces;

/// <summary>
/// Base marker interface for all workflow steps
/// </summary>
public interface IWorkflowStep;

/// <summary>
/// Interface for a step that works with a specific payload and error type
/// </summary>
/// <typeparam name="TPayload">The type of payload the step processes</typeparam>
/// <typeparam name="TError">The type of error the step might return</typeparam>
public interface IWorkflowStep<TPayload, TError> : IWorkflowStep
{
	/// <summary>
	/// Executes the step with the given payload
	/// </summary>
	/// <param name="payload">The input payload</param>
	/// <param name="cancellationToken">Cancellation token</param>
	/// <returns>Either the error or the updated payload</returns>
	Task<Either<TError, TPayload>> Execute(TPayload payload, CancellationToken cancellationToken);
}
