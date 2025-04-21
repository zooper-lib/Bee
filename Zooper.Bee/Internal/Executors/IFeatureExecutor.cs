using System.Threading;
using System.Threading.Tasks;
using Zooper.Bee.Features;
using Zooper.Fox;

namespace Zooper.Bee.Internal.Executors;

/// <summary>
/// Interface for executing workflow features
/// </summary>
/// <typeparam name="TPayload">The type of the workflow payload</typeparam>
/// <typeparam name="TError">The type of the error</typeparam>
internal interface IFeatureExecutor<TPayload, TError>
{
	/// <summary>
	/// Determines if this executor can handle the given feature
	/// </summary>
	/// <param name="feature">The feature to check</param>
	/// <returns>True if this executor can handle the feature, false otherwise</returns>
	bool CanExecute(IWorkflowFeature<TPayload, TError> feature);

	/// <summary>
	/// Executes the feature with the given payload
	/// </summary>
	/// <param name="feature">The feature to execute</param>
	/// <param name="payload">The current workflow payload</param>
	/// <param name="cancellationToken">The cancellation token</param>
	/// <returns>Either the error or the modified payload</returns>
	Task<Either<TError, TPayload>> Execute(
		IWorkflowFeature<TPayload, TError> feature,
		TPayload payload,
		CancellationToken cancellationToken);
}