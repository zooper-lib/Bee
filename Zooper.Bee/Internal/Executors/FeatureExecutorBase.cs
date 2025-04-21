using System.Threading;
using System.Threading.Tasks;
using Zooper.Bee.Features;
using Zooper.Fox;

namespace Zooper.Bee.Internal.Executors;

/// <summary>
/// Base class for feature executors
/// </summary>
/// <typeparam name="TPayload">The type of the workflow payload</typeparam>
/// <typeparam name="TError">The type of the error</typeparam>
/// <typeparam name="TFeature">The specific type of feature this executor handles</typeparam>
internal abstract class FeatureExecutorBase<TPayload, TError, TFeature> : IFeatureExecutor<TPayload, TError>
	where TFeature : IWorkflowFeature<TPayload, TError>
{
	/// <inheritdoc />
	public bool CanExecute(IWorkflowFeature<TPayload, TError> feature)
	{
		return feature is TFeature;
	}

	/// <inheritdoc />
	public async Task<Either<TError, TPayload>> Execute(
		IWorkflowFeature<TPayload, TError> feature,
		TPayload payload,
		CancellationToken cancellationToken)
	{
		if (feature is not TFeature typedFeature)
		{
			return Either<TError, TPayload>.FromRight(payload);
		}

		return await ExecuteTyped(typedFeature, payload, cancellationToken);
	}

	/// <summary>
	/// Executes the typed feature
	/// </summary>
	/// <param name="feature">The typed feature to execute</param>
	/// <param name="payload">The current workflow payload</param>
	/// <param name="cancellationToken">The cancellation token</param>
	/// <returns>Either the error or the modified payload</returns>
	protected abstract Task<Either<TError, TPayload>> ExecuteTyped(
		TFeature feature,
		TPayload payload,
		CancellationToken cancellationToken);
}