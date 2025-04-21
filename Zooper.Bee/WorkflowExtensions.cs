using System.Threading;
using System.Threading.Tasks;
using Zooper.Fox;

namespace Zooper.Bee;

/// <summary>
/// Extension methods for the Workflow class.
/// </summary>
public static class WorkflowExtensions
{
	/// <summary>
	/// Executes a workflow that doesn't require a request parameter.
	/// </summary>
	/// <typeparam name="TSuccess">The type of the success result</typeparam>
	/// <typeparam name="TError">The type of the error result</typeparam>
	/// <param name="workflow">The workflow to execute</param>
	/// <returns>The result of the workflow execution</returns>
	public static Task<Fox.Either<TError, TSuccess>> Execute<TSuccess, TError>(
		this Workflow<Unit, TSuccess, TError> workflow)
	{
		return workflow.Execute(Unit.Value);
	}

	/// <summary>
	/// Executes a workflow that doesn't require a request parameter.
	/// </summary>
	/// <typeparam name="TSuccess">The type of the success result</typeparam>
	/// <typeparam name="TError">The type of the error result</typeparam>
	/// <param name="workflow">The workflow to execute</param>
	/// <param name="cancellationToken">A cancellation token to observe while waiting for the task to complete</param>
	/// <returns>The result of the workflow execution</returns>
	public static Task<Fox.Either<TError, TSuccess>> Execute<TSuccess, TError>(
		this Workflow<Unit, TSuccess, TError> workflow,
		CancellationToken cancellationToken)
	{
		return workflow.Execute(Unit.Value, cancellationToken);
	}
}