using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Zooper.Fox;

namespace Zooper.Bee;

/// <summary>
/// Represents a builder for a workflow that processes a request and
/// either succeeds with a <typeparamref name="TSuccess"/> result
/// or fails with a <typeparamref name="TError"/>.
/// </summary>
/// <typeparam name="TRequest">The type of the request input.</typeparam>
/// <typeparam name="TPayload">The type of the payload used to carry intermediate data.</typeparam>
/// <typeparam name="TSuccess">The type of the success result.</typeparam>
/// <typeparam name="TError">The type of the error result.</typeparam>
/// <remarks>
/// Initializes a new instance of the <see cref="WorkflowBuilder{TRequest, TContext, TSuccess, TError}"/> class.
/// </remarks>
/// <param name="contextFactory">
/// Factory function that takes a request of type <typeparamref name="TRequest"/>
/// and produces a context of type <typeparamref name="TPayload"/>.
/// </param>
/// <param name="resultSelector">
/// Selector function that converts the final <typeparamref name="TPayload"/>
/// into a success result of type <typeparamref name="TSuccess"/>.
/// </param>
public sealed class WorkflowBuilder<TRequest, TPayload, TSuccess, TError>(
	Func<TRequest, TPayload> contextFactory,
	Func<TPayload, TSuccess> resultSelector)
{
	private readonly Func<TRequest, TPayload> _contextFactory = contextFactory;
	private readonly Func<TPayload, TSuccess> _resultSelector = resultSelector;

	private readonly List<WorkflowValidation<TRequest, TError>> _validations = [];
	private readonly List<WorkflowActivity<TPayload, TError>> _activities = [];
	private readonly List<ConditionalWorkflowActivity<TPayload, TError>> _conditionalActivities = [];
	private readonly List<WorkflowActivity<TPayload, TError>> _finallyActivities = [];
	private readonly Dictionary<string, BranchDefinition> _branches = new();

	private sealed class BranchDefinition
	{
		public Func<TPayload, bool> Condition { get; }
		public List<WorkflowActivity<TPayload, TError>> Activities { get; } = [];

		public BranchDefinition(Func<TPayload, bool> condition)
		{
			Condition = condition;
		}
	}

	/// <summary>
	/// Adds an asynchronous validation that operates on the raw <typeparamref name="TRequest"/>.
	/// </summary>
	/// <param name="validation">
	/// A function that receives <typeparamref name="TRequest"/> and returns
	/// either an updated request or an error, asynchronously.
	/// </param>
	/// <returns>The current builder instance for fluent chaining.</returns>
	public WorkflowBuilder<TRequest, TPayload, TSuccess, TError> Validate(
		Func<TRequest, CancellationToken, Task<Either<TError, TRequest>>> validation)
	{
		_validations.Add(new WorkflowValidation<TRequest, TError>(validation));
		return this;
	}

	/// <summary>
	/// Adds a synchronous validation that operates on the raw <typeparamref name="TRequest"/>.
	/// </summary>
	/// <param name="validation">
	/// A function that receives <typeparamref name="TRequest"/> and returns
	/// either an updated request or an error, synchronously.
	/// </param>
	/// <returns>The current builder instance for fluent chaining.</returns>
	public WorkflowBuilder<TRequest, TPayload, TSuccess, TError> Validate(
		Func<TRequest, Either<TError, TRequest>> validation)
	{
		_validations.Add(new WorkflowValidation<TRequest, TError>((req, _) =>
			Task.FromResult(validation(req))
		));
		return this;
	}

	/// <summary>
	/// Adds multiple validations that operate on the raw <typeparamref name="TRequest"/>.
	/// </summary>
	/// <param name="validations">
	/// One or more functions that each validate the <typeparamref name="TRequest"/>
	/// and return either an updated request or an error.
	/// </param>
	/// <returns>The current builder instance for fluent chaining.</returns>
	public WorkflowBuilder<TRequest, TPayload, TSuccess, TError> ValidateAll(
		params Func<TRequest, CancellationToken, Task<Either<TError, TRequest>>>[] validations)
	{
		foreach (var validation in validations)
		{
			_validations.Add(new WorkflowValidation<TRequest, TError>(validation));
		}
		return this;
	}

	/// <summary>
	/// Adds multiple synchronous validations that operate on the raw <typeparamref name="TRequest"/>.
	/// </summary>
	/// <param name="validations">
	/// One or more functions that each validate the <typeparamref name="TRequest"/>
	/// and return either an updated request or an error.
	/// </param>
	/// <returns>The current builder instance for fluent chaining.</returns>
	public WorkflowBuilder<TRequest, TPayload, TSuccess, TError> ValidateAll(
		params Func<TRequest, Either<TError, TRequest>>[] validations)
	{
		foreach (var validation in validations)
		{
			_validations.Add(new WorkflowValidation<TRequest, TError>((req, _) =>
				Task.FromResult(validation(req))
			));
		}
		return this;
	}

	/// <summary>
	/// Adds an asynchronous activity to the workflow.
	/// </summary>
	/// <param name="activity">
	/// A function that transforms the payload into an <see cref="Either{TError, TPayload}"/>
	/// asynchronously.
	/// </param>
	/// <returns>The current builder instance for fluent chaining.</returns>
	public WorkflowBuilder<TRequest, TPayload, TSuccess, TError> Do(
		Func<TPayload, CancellationToken, Task<Either<TError, TPayload>>> activity)
	{
		_activities.Add(new WorkflowActivity<TPayload, TError>(activity));
		return this;
	}

	/// <summary>
	/// Adds a synchronous activity to the workflow.
	/// </summary>
	/// <param name="activity">
	/// A function that transforms the payload into an <see cref="Either{TError, TPayload}"/>
	/// synchronously.
	/// </param>
	/// <returns>The current builder instance for fluent chaining.</returns>
	public WorkflowBuilder<TRequest, TPayload, TSuccess, TError> Do(
		Func<TPayload, Either<TError, TPayload>> activity)
	{
		_activities.Add(new WorkflowActivity<TPayload, TError>((payload, _) =>
			Task.FromResult(activity(payload))
		));
		return this;
	}

	/// <summary>
	/// Adds multiple activities to the workflow.
	/// </summary>
	/// <param name="activities">
	/// One or more functions that each transform the payload.
	/// </param>
	/// <returns>The current builder instance for fluent chaining.</returns>
	public WorkflowBuilder<TRequest, TPayload, TSuccess, TError> DoAll(
		params Func<TPayload, CancellationToken, Task<Either<TError, TPayload>>>[] activities)
	{
		foreach (var activity in activities)
		{
			_activities.Add(new WorkflowActivity<TPayload, TError>(activity));
		}
		return this;
	}

	/// <summary>
	/// Adds multiple synchronous activities to the workflow.
	/// </summary>
	/// <param name="activities">
	/// One or more functions that each transform the payload.
	/// </param>
	/// <returns>The current builder instance for fluent chaining.</returns>
	public WorkflowBuilder<TRequest, TPayload, TSuccess, TError> DoAll(
		params Func<TPayload, Either<TError, TPayload>>[] activities)
	{
		foreach (var activity in activities)
		{
			_activities.Add(new WorkflowActivity<TPayload, TError>((payload, _) =>
				Task.FromResult(activity(payload))
			));
		}
		return this;
	}

	/// <summary>
	/// Adds an activity with a descriptive name for better logging and debugging.
	/// </summary>
	/// <param name="name">Name of the activity</param>
	/// <param name="activity">The activity function</param>
	/// <returns>The current builder instance for fluent chaining.</returns>
	public WorkflowBuilder<TRequest, TPayload, TSuccess, TError> DoNamed(
		string name,
		Func<TPayload, CancellationToken, Task<Either<TError, TPayload>>> activity)
	{
		_activities.Add(new WorkflowActivity<TPayload, TError>(activity, name));
		return this;
	}

	/// <summary>
	/// Adds a conditional activity that only executes if the predicate returns true.
	/// </summary>
	/// <param name="condition">Predicate that determines if the activity should execute</param>
	/// <param name="activity">The activity to execute if the condition is true</param>
	/// <returns>The current builder instance for fluent chaining.</returns>
	public WorkflowBuilder<TRequest, TPayload, TSuccess, TError> DoWhen(
		Func<TPayload, bool> condition,
		Func<TPayload, CancellationToken, Task<Either<TError, TPayload>>> activity)
	{
		_conditionalActivities.Add(new ConditionalWorkflowActivity<TPayload, TError>(
			condition,
			new WorkflowActivity<TPayload, TError>(activity)
		));
		return this;
	}

	/// <summary>
	/// Adds a conditional synchronous activity that only executes if the predicate returns true.
	/// </summary>
	/// <param name="condition">Predicate that determines if the activity should execute</param>
	/// <param name="activity">The activity to execute if the condition is true</param>
	/// <returns>The current builder instance for fluent chaining.</returns>
	public WorkflowBuilder<TRequest, TPayload, TSuccess, TError> DoWhen(
		Func<TPayload, bool> condition,
		Func<TPayload, Either<TError, TPayload>> activity)
	{
		_conditionalActivities.Add(new ConditionalWorkflowActivity<TPayload, TError>(
			condition,
			new WorkflowActivity<TPayload, TError>((payload, _) =>
				Task.FromResult(activity(payload))
			)
		));
		return this;
	}

	/// <summary>
	/// Adds an activity that only executes when a specific field/property in the payload is not null.
	/// </summary>
	/// <param name="selector">Selector function that extracts the field to check for non-null</param>
	/// <param name="activity">The activity to execute if the field is not null</param>
	/// <returns>The current builder instance for fluent chaining.</returns>
	public WorkflowBuilder<TRequest, TPayload, TSuccess, TError> DoWhenNotNull<TField>(
		Func<TPayload, TField?> selector,
		Func<TPayload, CancellationToken, Task<Either<TError, TPayload>>> activity) where TField : class
	{
		return DoWhen(
			payload => selector(payload) != null,
			activity);
	}

	/// <summary>
	/// Adds an activity that only executes when a specific field/property in the payload is not null.
	/// </summary>
	/// <param name="selector">Selector function that extracts the field to check for non-null</param>
	/// <param name="activity">The activity to execute if the field is not null</param>
	/// <returns>The current builder instance for fluent chaining.</returns>
	public WorkflowBuilder<TRequest, TPayload, TSuccess, TError> DoWhenNotNull<TField>(
		Func<TPayload, TField?> selector,
		Func<TPayload, Either<TError, TPayload>> activity) where TField : class
	{
		return DoWhen(
			payload => selector(payload) != null,
			activity);
	}

	/// <summary>
	/// Adds an activity that only executes when a specific nullable value type in the payload has a value.
	/// </summary>
	/// <param name="selector">Selector function that extracts the nullable value type to check</param>
	/// <param name="activity">The activity to execute if the value has a value</param>
	/// <returns>The current builder instance for fluent chaining.</returns>
	public WorkflowBuilder<TRequest, TPayload, TSuccess, TError> DoWhenHasValue<TValue>(
		Func<TPayload, TValue?> selector,
		Func<TPayload, CancellationToken, Task<Either<TError, TPayload>>> activity) where TValue : struct
	{
		return DoWhen(
			payload => selector(payload).HasValue,
			activity);
	}

	/// <summary>
	/// Adds an activity that only executes when a specific nullable value type in the payload has a value.
	/// </summary>
	/// <param name="selector">Selector function that extracts the nullable value type to check</param>
	/// <param name="activity">The activity to execute if the value has a value</param>
	/// <returns>The current builder instance for fluent chaining.</returns>
	public WorkflowBuilder<TRequest, TPayload, TSuccess, TError> DoWhenHasValue<TValue>(
		Func<TPayload, TValue?> selector,
		Func<TPayload, Either<TError, TPayload>> activity) where TValue : struct
	{
		return DoWhen(
			payload => selector(payload).HasValue,
			activity);
	}

	/// <summary>
	/// Adds an activity that will always execute regardless of other workflow success or failure.
	/// </summary>
	/// <param name="activity">The activity to execute</param>
	/// <returns>The current builder instance for fluent chaining.</returns>
	public WorkflowBuilder<TRequest, TPayload, TSuccess, TError> Finally(
		Func<TPayload, CancellationToken, Task<Either<TError, TPayload>>> activity)
	{
		_finallyActivities.Add(new WorkflowActivity<TPayload, TError>(activity));
		return this;
	}

	/// <summary>
	/// Adds a synchronous activity that will always execute regardless of other workflow success or failure.
	/// </summary>
	/// <param name="activity">The activity to execute</param>
	/// <returns>The current builder instance for fluent chaining.</returns>
	public WorkflowBuilder<TRequest, TPayload, TSuccess, TError> Finally(
		Func<TPayload, Either<TError, TPayload>> activity)
	{
		_finallyActivities.Add(new WorkflowActivity<TPayload, TError>((payload, _) =>
			Task.FromResult(activity(payload))
		));
		return this;
	}

	/// <summary>
	/// Starts defining a branch of the workflow that will only execute if the condition is true.
	/// </summary>
	/// <param name="branchName">Name of the branch for identification</param>
	/// <param name="condition">The condition that determines if this branch executes</param>
	/// <returns>The current builder instance for fluent chaining.</returns>
	public WorkflowBuilder<TRequest, TPayload, TSuccess, TError> Branch(
		string branchName,
		Func<TPayload, bool> condition)
	{
		if (_branches.ContainsKey(branchName))
		{
			throw new ArgumentException($"Branch with name '{branchName}' already exists", nameof(branchName));
		}

		_branches[branchName] = new BranchDefinition(condition);
		return this;
	}

	/// <summary>
	/// Adds an activity to a previously defined branch.
	/// </summary>
	/// <param name="branchName">Name of the branch to add the activity to</param>
	/// <param name="activity">The activity to add to the branch</param>
	/// <returns>The current builder instance for fluent chaining.</returns>
	public WorkflowBuilder<TRequest, TPayload, TSuccess, TError> InBranch(
		string branchName,
		Func<TPayload, CancellationToken, Task<Either<TError, TPayload>>> activity)
	{
		if (!_branches.TryGetValue(branchName, out var branch))
		{
			throw new ArgumentException($"Branch with name '{branchName}' does not exist", nameof(branchName));
		}

		branch.Activities.Add(new WorkflowActivity<TPayload, TError>(activity));
		return this;
	}

	/// <summary>
	/// Adds a synchronous activity to a previously defined branch.
	/// </summary>
	/// <param name="branchName">Name of the branch to add the activity to</param>
	/// <param name="activity">The activity to add to the branch</param>
	/// <returns>The current builder instance for fluent chaining.</returns>
	public WorkflowBuilder<TRequest, TPayload, TSuccess, TError> InBranch(
		string branchName,
		Func<TPayload, Either<TError, TPayload>> activity)
	{
		if (!_branches.TryGetValue(branchName, out var branch))
		{
			throw new ArgumentException($"Branch with name '{branchName}' does not exist", nameof(branchName));
		}

		branch.Activities.Add(new WorkflowActivity<TPayload, TError>((payload, _) =>
			Task.FromResult(activity(payload))
		));
		return this;
	}

	/// <summary>
	/// Adds multiple activities to a previously defined branch.
	/// </summary>
	/// <param name="branchName">Name of the branch to add the activities to</param>
	/// <param name="activities">The activities to add to the branch</param>
	/// <returns>The current builder instance for fluent chaining.</returns>
	public WorkflowBuilder<TRequest, TPayload, TSuccess, TError> InBranchAll(
		string branchName,
		params Func<TPayload, CancellationToken, Task<Either<TError, TPayload>>>[] activities)
	{
		if (!_branches.TryGetValue(branchName, out var branch))
		{
			throw new ArgumentException($"Branch with name '{branchName}' does not exist", nameof(branchName));
		}

		foreach (var activity in activities)
		{
			branch.Activities.Add(new WorkflowActivity<TPayload, TError>(activity));
		}

		return this;
	}

	/// <summary>
	/// Legacy support for UseCondition. Use <see cref="Validate"/> instead.
	/// </summary>
	[Obsolete("Use Validate instead")]
	public WorkflowBuilder<TRequest, TPayload, TSuccess, TError> UseCondition(
		Func<TRequest, CancellationToken, Task<Either<TError, TRequest>>> preStep)
	{
		return Validate(preStep);
	}

	/// <summary>
	/// Legacy support for UseCondition. Use <see cref="Validate"/> instead.
	/// </summary>
	[Obsolete("Use Validate instead")]
	public WorkflowBuilder<TRequest, TPayload, TSuccess, TError> UseCondition(
		Func<TRequest, Either<TError, TRequest>> preStep)
	{
		return Validate(preStep);
	}

	/// <summary>
	/// Legacy support for UseConditions. Use <see cref="ValidateAll"/> instead.
	/// </summary>
	[Obsolete("Use ValidateAll instead")]
	public WorkflowBuilder<TRequest, TPayload, TSuccess, TError> UseConditions(
		params Func<TRequest, CancellationToken, Task<Either<TError, TRequest>>>[] preSteps)
	{
		return ValidateAll(preSteps);
	}

	/// <summary>
	/// Legacy support for UseConditions. Use <see cref="ValidateAll"/> instead.
	/// </summary>
	[Obsolete("Use ValidateAll instead")]
	public WorkflowBuilder<TRequest, TPayload, TSuccess, TError> UseConditions(
		params Func<TRequest, Either<TError, TRequest>>[] preSteps)
	{
		return ValidateAll(preSteps);
	}

	/// <summary>
	/// Legacy support for UseStep. Use <see cref="Do"/> instead.
	/// </summary>
	[Obsolete("Use Do instead")]
	public WorkflowBuilder<TRequest, TPayload, TSuccess, TError> UseStep(
		Func<TPayload, CancellationToken, Task<Either<TError, TPayload>>> step)
	{
		return Do(step);
	}

	/// <summary>
	/// Legacy support for UseStep. Use <see cref="Do"/> instead.
	/// </summary>
	[Obsolete("Use Do instead")]
	public WorkflowBuilder<TRequest, TPayload, TSuccess, TError> UseStep(
		Func<TPayload, Either<TError, TPayload>> step)
	{
		return Do(step);
	}

	/// <summary>
	/// Legacy support for UseSteps. Use <see cref="DoAll"/> instead.
	/// </summary>
	[Obsolete("Use DoAll instead")]
	public WorkflowBuilder<TRequest, TPayload, TSuccess, TError> UseSteps(
		params Func<TPayload, CancellationToken, Task<Either<TError, TPayload>>>[] steps)
	{
		return DoAll(steps);
	}

	/// <summary>
	/// Legacy support for UseSteps. Use <see cref="DoAll"/> instead.
	/// </summary>
	[Obsolete("Use DoAll instead")]
	public WorkflowBuilder<TRequest, TPayload, TSuccess, TError> UseSteps(
		params Func<TPayload, Either<TError, TPayload>>[] steps)
	{
		return DoAll(steps);
	}

	/// <summary>
	/// Executes the workflow by creating the initial payload from the request,
	/// running each validation in sequence on the request, then
	/// running each activity in sequence on the payload,
	/// followed by conditional activities and branches,
	/// then finally activities, 
	/// and finally converting the resulting payload to a success value.
	/// </summary>
	/// <param name="request">The request object of type <typeparamref name="TRequest"/>.</param>
	/// <param name="token">A <see cref="CancellationToken"/> for optional cancellation.</param>
	/// <returns>
	/// An <see cref="Either{TError, TSuccess}"/> containing either an error
	/// or the success result.
	/// </returns>
	public async Task<Either<TError, TSuccess>> RunAsync(
		TRequest request,
		CancellationToken token)
	{
		// Storage for error result if we need to run finally blocks
		Either<TError, TPayload>? errorResult = null;
		TPayload? payload = default;

		try
		{
			// Run validations on the raw TRequest
			foreach (var validation in _validations)
			{
				var preResult = await validation.Execute(request, token).ConfigureAwait(false);

				if (preResult.IsLeft)
				{
					errorResult = Either<TError, TPayload>.FromLeft(
						preResult.Left ?? throw new ArgumentNullException(nameof(preResult), "Validation returned a null TError."));
					return Either<TError, TSuccess>.FromLeft(preResult.Left);
				}

				request = preResult.Right ?? throw new ArgumentNullException(nameof(preResult), "Validation returned a null TRequest.");
			}

			// Create the initial context
			payload = _contextFactory(request)
					  ?? throw new ArgumentNullException(nameof(request), "Context factory returned null.");

			// Run each activity in sequence
			foreach (var activity in _activities)
			{
				var result = await activity.Execute(payload, token).ConfigureAwait(false);

				if (result.IsLeft)
				{
					errorResult = result;
					return Either<TError, TSuccess>.FromLeft(
						result.Left ?? throw new ArgumentNullException(nameof(result), "Activity returned a null TError."));
				}

				payload = result.Right ?? throw new ArgumentNullException(nameof(result), "Activity returned a null TPayload.");
			}

			// Run conditional activities
			foreach (var conditionalActivity in _conditionalActivities)
			{
				if (conditionalActivity.ShouldExecute(payload))
				{
					var result = await conditionalActivity.Activity.Execute(payload, token).ConfigureAwait(false);

					if (result.IsLeft)
					{
						errorResult = result;
						return Either<TError, TSuccess>.FromLeft(
							result.Left ?? throw new ArgumentNullException(nameof(result), "Conditional activity returned a null TError."));
					}

					payload = result.Right ?? throw new ArgumentNullException(nameof(result), "Conditional activity returned a null TPayload.");
				}
			}

			// Execute branches
			foreach (var kvp in _branches)
			{
				var branch = kvp.Value;
				if (branch.Condition(payload))
				{
					foreach (var activity in branch.Activities)
					{
						var result = await activity.Execute(payload, token).ConfigureAwait(false);

						if (result.IsLeft)
						{
							errorResult = result;
							return Either<TError, TSuccess>.FromLeft(
								result.Left ?? throw new ArgumentNullException(nameof(result), "Branch activity returned a null TError."));
						}

						payload = result.Right ?? throw new ArgumentNullException(nameof(result), "Branch activity returned a null TPayload.");
					}
				}
			}

			// Convert the final context to a success result
			var success = _resultSelector(payload);

			return Either<TError, TSuccess>.FromRight(success);
		}
		finally
		{
			// Run finally activities regardless of success or failure
			if (payload != null && _finallyActivities.Count > 0)
			{
				foreach (var activity in _finallyActivities)
				{
					try
					{
						var result = await activity.Execute(payload, token).ConfigureAwait(false);

						// If previous execution succeeded but a finally block fails, update the result
						if (errorResult == null && result.IsLeft)
						{
							errorResult = result;
						}

						// Continue executing finally blocks regardless of errors
						if (result.IsRight)
						{
							payload = result.Right;
						}
					}
					catch (Exception)
					{
						// Swallow exceptions in finally blocks to ensure all finally blocks run
						// We don't want an exception in a finally block to prevent other finally blocks from running
					}
				}
			}
		}
	}
}

/// <summary>
/// Represents a validation step that operates on a request.
/// </summary>
/// <typeparam name="TRequest">Type of the request</typeparam>
/// <typeparam name="TError">Type of the error</typeparam>
internal sealed class WorkflowValidation<TRequest, TError>
{
	private readonly Func<TRequest, CancellationToken, Task<Either<TError, TRequest>>> _validation;
	private readonly string? _name;

	public WorkflowValidation(
		Func<TRequest, CancellationToken, Task<Either<TError, TRequest>>> validation,
		string? name = null)
	{
		_validation = validation;
		_name = name;
	}

	public Task<Either<TError, TRequest>> Execute(TRequest request, CancellationToken token)
	{
		return _validation(request, token);
	}
}

/// <summary>
/// Represents an activity (step) in the workflow that operates on a payload.
/// </summary>
/// <typeparam name="TPayload">Type of the payload</typeparam>
/// <typeparam name="TError">Type of the error</typeparam>
internal sealed class WorkflowActivity<TPayload, TError>
{
	private readonly Func<TPayload, CancellationToken, Task<Either<TError, TPayload>>> _activity;
	private readonly string? _name;

	public WorkflowActivity(
		Func<TPayload, CancellationToken, Task<Either<TError, TPayload>>> activity,
		string? name = null)
	{
		_activity = activity;
		_name = name;
	}

	public Task<Either<TError, TPayload>> Execute(TPayload payload, CancellationToken token)
	{
		return _activity(payload, token);
	}
}

/// <summary>
/// Represents a conditional activity in the workflow that only executes if a condition is met.
/// </summary>
/// <typeparam name="TPayload">Type of the payload</typeparam>
/// <typeparam name="TError">Type of the error</typeparam>
internal sealed class ConditionalWorkflowActivity<TPayload, TError>
{
	private readonly Func<TPayload, bool> _condition;

	public WorkflowActivity<TPayload, TError> Activity { get; }

	public ConditionalWorkflowActivity(
		Func<TPayload, bool> condition,
		WorkflowActivity<TPayload, TError> activity)
	{
		_condition = condition;
		Activity = activity;
	}

	public bool ShouldExecute(TPayload payload)
	{
		return _condition(payload);
	}
}