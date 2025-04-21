using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Zooper.Fox;

namespace Zooper.Bee.Internal.Executors;

/// <summary>
/// Executor for Context features with support for any local state type
/// </summary>
/// <typeparam name="TPayload">The type of the workflow payload</typeparam>
/// <typeparam name="TError">The type of the error</typeparam>
internal class ContextExecutor<TPayload, TError> : IFeatureExecutor<TPayload, TError>
{
	/// <inheritdoc />
	public bool CanExecute(Features.IWorkflowFeature<TPayload, TError> feature)
	{
		if (feature == null)
		{
			return false;
		}

		var featureType = feature.GetType();
		if (featureType == null)
		{
			return false;
		}

		return featureType.IsGenericType &&
			featureType.GetGenericTypeDefinition() == typeof(Features.Context.Context<,,>);
	}

	/// <inheritdoc />
	public async Task<Either<TError, TPayload>> Execute(
		Features.IWorkflowFeature<TPayload, TError> feature,
		TPayload payload,
		CancellationToken cancellationToken)
	{
		if (feature == null)
		{
			return Either<TError, TPayload>.FromRight(payload);
		}

		// Skip if the condition is false
		if (feature.Condition != null && !feature.Condition(payload))
		{
			return Either<TError, TPayload>.FromRight(payload);
		}

		// Use reflection to call the appropriate method based on the feature's generic type parameters
		try
		{
			var featureType = feature.GetType();
			if (featureType == null)
			{
				return Either<TError, TPayload>.FromRight(payload);
			}

			var typeArgs = featureType.GetGenericArguments();
			if (typeArgs == null || typeArgs.Length < 2)
			{
				return Either<TError, TPayload>.FromRight(payload);
			}

			var localStateType = typeArgs[1];
			if (localStateType == null)
			{
				return Either<TError, TPayload>.FromRight(payload);
			}

			// Get the generic method and make it specific to the local state type
			var method = GetType().GetMethod(nameof(ExecuteTyped),
				System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

			if (method == null)
			{
				throw new InvalidOperationException($"Method {nameof(ExecuteTyped)} not found.");
			}

			var genericMethod = method.MakeGenericMethod(localStateType);
			if (genericMethod == null)
			{
				return Either<TError, TPayload>.FromRight(payload);
			}

			// Ensure payload is not null before passing to the method
			payload ??= default!; // Use default value if null

			// Invoke the method with the right generic parameter
			var result = genericMethod.Invoke(this, new object[] { feature, payload, cancellationToken });
			return result == null
				? throw new InvalidOperationException("Method invocation returned null.")
				: await (Task<Either<TError, TPayload>>)result;
		}
		catch (Exception)
		{
			// If any reflection-related exception occurs, return the payload unchanged
			return Either<TError, TPayload>.FromRight(payload);
		}
	}

	/// <summary>
	/// Executes a context with a specific local state type
	/// </summary>
	/// <typeparam name="TLocalState">The type of the local state</typeparam>
	/// <param name="feature">The context feature</param>
	/// <param name="payload">The current workflow payload</param>
	/// <param name="cancellationToken">The cancellation token</param>
	/// <returns>Either the error or the modified payload</returns>
	private async Task<Either<TError, TPayload>> ExecuteTyped<TLocalState>(
		Features.IWorkflowFeature<TPayload, TError> feature,
		TPayload payload,
		CancellationToken cancellationToken)
	{
		var context = feature as Features.Context.Context<TPayload, TLocalState, TError>;
		if (context == null)
		{
			return Either<TError, TPayload>.FromRight(payload);
		}

		// Check if local state factory is null
		if (context.LocalStateFactory == null)
		{
			return Either<TError, TPayload>.FromRight(payload);
		}

		// Create the local state
		TLocalState? localState;
		try
		{
			localState = context.LocalStateFactory(payload);
		}
		catch (Exception)
		{
			// If we can't create the local state, return the payload unchanged
			return Either<TError, TPayload>.FromRight(payload);
		}

		// Check if activities collection is null
		if (context.Activities == null)
		{
			return Either<TError, TPayload>.FromRight(payload);
		}

		// Execute the context activities
		foreach (var activity in context.Activities)
		{
			// Skip null activities
			if (activity == null)
			{
				continue;
			}

			var activityResult = await activity.Execute(payload, localState, cancellationToken);
			if (activityResult == null)
			{
				// Skip if the activity result is null
				continue;
			}

			if (activityResult.IsLeft)
			{
				return Either<TError, TPayload>.FromLeft(activityResult.Left);
			}

			// Update both payload and local state
			if (activityResult.Right.MainPayload != null)
			{
				payload = activityResult.Right.MainPayload;
			}
			if (activityResult.Right.LocalState != null)
			{
				localState = activityResult.Right.LocalState;
			}
		}

		return Either<TError, TPayload>.FromRight(payload);
	}
}