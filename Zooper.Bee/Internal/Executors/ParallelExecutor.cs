using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Zooper.Bee.Features.Parallel;
using Zooper.Fox;

namespace Zooper.Bee.Internal.Executors;

/// <summary>
/// Executor for Parallel features
/// </summary>
/// <typeparam name="TPayload">The type of the workflow payload</typeparam>
/// <typeparam name="TError">The type of the error</typeparam>
internal class ParallelExecutor<TPayload, TError> : FeatureExecutorBase<TPayload, TError, Parallel<TPayload, TError>>
{
	/// <inheritdoc />
	protected override async Task<Either<TError, TPayload>> ExecuteTyped(
		Parallel<TPayload, TError> feature,
		TPayload payload,
		CancellationToken cancellationToken)
	{
		// Execute groups in parallel and merge results
		var tasks = new List<Task<Either<TError, TPayload>>>();

		// Check if groups collection is null
		if (feature.Groups == null)
		{
			return Either<TError, TPayload>.FromRight(payload);
		}

		foreach (var parallelGroup in feature.Groups)
		{
			// Skip null groups
			if (parallelGroup == null)
			{
				continue;
			}

			// Skip if the condition is false
			if (parallelGroup.Condition != null && !parallelGroup.Condition(payload))
			{
				continue;
			}

			tasks.Add(ExecuteGroupActivities(parallelGroup.Activities, payload, cancellationToken));
		}

		if (tasks.Count == 0)
		{
			// No groups to execute
			return Either<TError, TPayload>.FromRight(payload);
		}

		// Wait for all tasks and process results
		var results = await Task.WhenAll(tasks);

		// If any task returned an error, return that error
		foreach (var result in results)
		{
			// Skip null results
			if (result == null)
			{
				continue;
			}

			if (result.IsLeft)
			{
				// Check if Left is null
				if (result.Left == null)
				{
					continue;
				}
				return Either<TError, TPayload>.FromLeft(result.Left);
			}
		}

		// Create a merged result from all parallel executions
		var mergedPayload = payload;

		// Apply each result to the merged payload
		// This uses reflection to copy over non-default property values
		foreach (var result in results)
		{
			var source = result.Right;

			// Skip if source is null
			if (source == null)
			{
				continue;
			}

			var sourceType = source.GetType();
			if (sourceType == null)
			{
				continue;
			}

			var targetType = mergedPayload?.GetType();
			if (targetType == null)
			{
				continue;
			}

			// Get all properties from the payload type
			var properties = sourceType.GetProperties();
			if (properties == null || properties.Length == 0)
			{
				continue;
			}

			foreach (var property in properties)
			{
				// Skip if property is null
				if (property == null)
				{
					continue;
				}

				// Skip if property type is null
				if (property.PropertyType == null)
				{
					continue;
				}

				object? sourceValue = null;
				try
				{
					sourceValue = property.GetValue(source);
				}
				catch (Exception)
				{
					// Skip if we can't get the value
					continue;
				}

				object? defaultValue = null;
				try
				{
					defaultValue = property.PropertyType.IsValueType ?
						Activator.CreateInstance(property.PropertyType) : null;
				}
				catch (Exception)
				{
					// If we can't create default, use null as default
				}

				// Only copy non-default values (like Sum, Product, etc.)
				if (sourceValue != null && (defaultValue == null || !sourceValue.Equals(defaultValue)))
				{
					// Ensure the property can be written to and the merged payload is not null
					if (property.CanWrite && mergedPayload != null)
					{
						try
						{
							property.SetValue(mergedPayload, sourceValue);
						}
						catch (Exception)
						{
							// Skip if we can't set the value
						}
					}
				}
			}
		}

		return Either<TError, TPayload>.FromRight(mergedPayload);
	}

	// Helper method to execute a group's activities
	private async Task<Either<TError, TPayload>> ExecuteGroupActivities(
		List<WorkflowActivity<TPayload, TError>> activities,
		TPayload payload,
		CancellationToken cancellationToken)
	{
		// Check if activities is null
		if (activities == null)
		{
			return Either<TError, TPayload>.FromRight(payload);
		}

		var currentPayload = payload;

		foreach (var activity in activities)
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