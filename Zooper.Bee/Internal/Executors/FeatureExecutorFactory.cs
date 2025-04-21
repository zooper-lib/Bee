using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Zooper.Bee.Features;
using Zooper.Fox;

namespace Zooper.Bee.Internal.Executors;

/// <summary>
/// Factory for creating feature executors
/// </summary>
/// <typeparam name="TPayload">The type of the workflow payload</typeparam>
/// <typeparam name="TError">The type of the error</typeparam>
internal class FeatureExecutorFactory<TPayload, TError>
{
	private readonly IList<IFeatureExecutor<TPayload, TError>> _executors;

	/// <summary>
	/// Initializes a new instance of the <see cref="FeatureExecutorFactory{TPayload, TError}"/> class.
	/// </summary>
	public FeatureExecutorFactory()
	{
		_executors = new List<IFeatureExecutor<TPayload, TError>>
		{
			new GroupExecutor<TPayload, TError>(),
			new DetachedExecutor<TPayload, TError>(),
			new ParallelExecutor<TPayload, TError>(),
			new ParallelDetachedExecutor<TPayload, TError>(),
			new ContextExecutor<TPayload, TError>(),
			// Add other executors here as they are implemented
		};
	}

	/// <summary>
	/// Executes the given feature
	/// </summary>
	/// <param name="feature">The feature to execute</param>
	/// <param name="payload">The current workflow payload</param>
	/// <param name="cancellationToken">The cancellation token</param>
	/// <returns>Either the error or the modified payload</returns>
	public async Task<Either<TError, TPayload>> ExecuteFeature(
		IWorkflowFeature<TPayload, TError> feature,
		TPayload payload,
		CancellationToken cancellationToken)
	{
		// Skip null features
		if (feature == null)
		{
			return Either<TError, TPayload>.FromRight(payload);
		}

		// Skip if the condition is false
		if (feature.Condition != null && !feature.Condition(payload))
		{
			return Either<TError, TPayload>.FromRight(payload);
		}

		// Find an executor that can handle this feature
		var executor = _executors.FirstOrDefault(e => e.CanExecute(feature));
		if (executor == null)
		{
			throw new InvalidOperationException($"No executor found for feature type {feature.GetType().Name}");
		}

		// Execute the feature
		return await executor.Execute(feature, payload, cancellationToken);
	}
}