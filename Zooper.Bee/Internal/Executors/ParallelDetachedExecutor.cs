using System.Threading;
using System.Threading.Tasks;
using Zooper.Bee.Features.Parallel;
using Zooper.Fox;

namespace Zooper.Bee.Internal.Executors;

/// <summary>
/// Executor for ParallelDetached features
/// </summary>
/// <typeparam name="TPayload">The type of the workflow payload</typeparam>
/// <typeparam name="TError">The type of the error</typeparam>
internal class ParallelDetachedExecutor<TPayload, TError> : FeatureExecutorBase<TPayload, TError, ParallelDetached<TPayload, TError>>
{
	/// <inheritdoc />
	protected override Task<Either<TError, TPayload>> ExecuteTyped(
		ParallelDetached<TPayload, TError> feature,
		TPayload payload,
		CancellationToken cancellationToken)
	{
		// Start detached groups in parallel but don't wait for them or use their results
		var detachedPayload = payload;

		// Check if detached groups collection is null
		if (feature.DetachedGroups == null)
		{
			return Task.FromResult(Either<TError, TPayload>.FromRight(payload));
		}

		foreach (var detachedGroup in feature.DetachedGroups)
		{
			// Skip null groups
			if (detachedGroup == null)
			{
				continue;
			}

			// Skip if the condition is false
			if (detachedGroup.Condition != null && !detachedGroup.Condition(detachedPayload))
			{
				continue;
			}

			// Start each detached group in its own task
#pragma warning disable CS4014
			Task.Run(async () =>
			{
				var localPayload = detachedPayload;
				foreach (var activity in detachedGroup.Activities)
				{
					// Skip null activities
					if (activity == null)
					{
						continue;
					}

					var activityResult = await activity.Execute(localPayload, cancellationToken);
					if (activityResult == null)
					{
						continue;
					}

					if (activityResult.IsLeft)
					{
						// Log or handle error if needed
						break;
					}

					if (activityResult.Right != null)
					{
						localPayload = activityResult.Right;
					}
				}
			}, cancellationToken);
#pragma warning restore CS4014
		}

		// Return original payload since parallel detached execution doesn't affect the main flow
		return Task.FromResult(Either<TError, TPayload>.FromRight(payload));
	}
}