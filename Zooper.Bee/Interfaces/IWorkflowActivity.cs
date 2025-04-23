using System.Threading;
using System.Threading.Tasks;
using Zooper.Fox;

namespace Zooper.Bee.Interfaces;

/// <summary>
/// Base marker interface for all workflow activities
/// </summary>
public interface IWorkflowActivity;

/// <summary>
/// Interface for an activity that works with a specific payload and error type
/// </summary>
/// <typeparam name="TPayload">The type of payload the activity processes</typeparam>
/// <typeparam name="TError">The type of error the activity might return</typeparam>
public interface IWorkflowActivity<TPayload, TError> : IWorkflowActivity
{
	/// <summary>
	/// Executes the activity with the given payload
	/// </summary>
	/// <param name="payload">The input payload</param>
	/// <param name="cancellationToken">Cancellation token</param>
	/// <returns>Either the error or the updated payload</returns>
	Task<Either<TError, TPayload>> Execute(TPayload payload, CancellationToken cancellationToken);
}
