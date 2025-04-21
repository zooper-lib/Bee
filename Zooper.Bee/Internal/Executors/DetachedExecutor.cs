using System.Threading;
using System.Threading.Tasks;
using Zooper.Bee.Features.Detached;
using Zooper.Fox;

namespace Zooper.Bee.Internal.Executors;

/// <summary>
/// Executor for Detached features
/// </summary>
/// <typeparam name="TPayload">The type of the workflow payload</typeparam>
/// <typeparam name="TError">The type of the error</typeparam>
internal class DetachedExecutor<TPayload, TError> : FeatureExecutorBase<TPayload, TError, Detached<TPayload, TError>>
{
	/// <inheritdoc />
	protected override async Task<Either<TError, TPayload>> ExecuteTyped(
		Detached<TPayload, TError> feature,
		TPayload payload,
		CancellationToken cancellationToken)
	{
		// Start detached activities but don't wait for them or use their results
		var detachedPayload = payload;

		// Check if activities collection is null
		if (feature.Activities == null)
		{
			return Either<TError, TPayload>.FromRight(payload);
		}

		// Disable the warning about not awaiting the Task.Run
#pragma warning disable CS4014
		Task.Run(async () =>
		{
			foreach (var activity in feature.Activities)
			{
				// Skip null activities
				if (activity == null)
				{
					continue;
				}

				var activityResult = await activity.Execute(detachedPayload, cancellationToken);
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
					detachedPayload = activityResult.Right;
				}
			}
		}, cancellationToken);
#pragma warning restore CS4014

		// Return original payload since detached execution doesn't affect the main flow
		return Either<TError, TPayload>.FromRight(payload);
	}
}