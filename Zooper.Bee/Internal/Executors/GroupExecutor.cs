using System.Threading;
using System.Threading.Tasks;
using Zooper.Bee.Features.Group;
using Zooper.Fox;

namespace Zooper.Bee.Internal.Executors;

/// <summary>
/// Executor for Group features
/// </summary>
/// <typeparam name="TPayload">The type of the workflow payload</typeparam>
/// <typeparam name="TError">The type of the error</typeparam>
internal class GroupExecutor<TPayload, TError> : FeatureExecutorBase<TPayload, TError, Group<TPayload, TError>>
{
	/// <inheritdoc />
	protected override async Task<Either<TError, TPayload>> ExecuteTyped(
		Group<TPayload, TError> feature,
		TPayload payload,
		CancellationToken cancellationToken)
	{
		var currentPayload = payload;

		// Check if activities collection is null
		if (feature.Activities == null)
		{
			return Either<TError, TPayload>.FromRight(currentPayload);
		}

		foreach (var activity in feature.Activities)
		{
			// Skip null activities
			if (activity == null)
			{
				continue;
			}

			var activityResult = await activity.Execute(currentPayload, cancellationToken);
			if (activityResult == null)
			{
				// Skip if the activity result is null
				continue;
			}

			if (activityResult.IsLeft)
			{
				// Check if Left is null
				if (activityResult.Left == null)
				{
					continue;
				}
				return Either<TError, TPayload>.FromLeft(activityResult.Left);
			}

			// Check if Right is null
			if (activityResult.Right == null)
			{
				continue;
			}
			currentPayload = activityResult.Right;
		}

		return Either<TError, TPayload>.FromRight(currentPayload);
	}
}