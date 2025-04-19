using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Zooper.Bee.Internal;
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
public sealed class WorkflowBuilder<TRequest, TPayload, TSuccess, TError>
{
	private readonly Func<TRequest, TPayload> _contextFactory;
	private readonly Func<TPayload, TSuccess> _resultSelector;

	private readonly List<WorkflowValidation<TRequest, TError>> _validations = [];
	private readonly List<WorkflowActivity<TPayload, TError>> _activities = [];
	private readonly List<ConditionalWorkflowActivity<TPayload, TError>> _conditionalActivities = [];
	private readonly List<WorkflowActivity<TPayload, TError>> _finallyActivities = [];
	private readonly List<Branch<TPayload, TError>> _branches = [];

	/// <summary>
	/// Initializes a new instance of the <see cref="WorkflowBuilder{TRequest, TPayload, TSuccess, TError}"/> class.
	/// </summary>
	/// <param name="contextFactory">
	/// Factory function that takes a request of type <typeparamref name="TRequest"/>
	/// and produces a context of type <typeparamref name="TPayload"/>.
	/// </param>
	/// <param name="resultSelector">
	/// Selector function that converts the final <typeparamref name="TPayload"/>
	/// into a success result of type <typeparamref name="TSuccess"/>.
	/// </param>
	public WorkflowBuilder(
		Func<TRequest, TPayload> contextFactory,
		Func<TPayload, TSuccess> resultSelector)
	{
		_contextFactory = contextFactory ?? throw new ArgumentNullException(nameof(contextFactory));
		_resultSelector = resultSelector ?? throw new ArgumentNullException(nameof(resultSelector));
	}

	/// <summary>
	/// Adds a validation rule to the workflow.
	/// </summary>
	/// <param name="validation">The validation function</param>
	/// <returns>The builder instance for method chaining</returns>
	public WorkflowBuilder<TRequest, TPayload, TSuccess, TError> Validate(
		Func<TRequest, CancellationToken, Task<Option<TError>>> validation)
	{
		_validations.Add(new WorkflowValidation<TRequest, TError>(validation));
		return this;
	}

	/// <summary>
	/// Adds a synchronous validation rule to the workflow.
	/// </summary>
	/// <param name="validation">The validation function</param>
	/// <returns>The builder instance for method chaining</returns>
	public WorkflowBuilder<TRequest, TPayload, TSuccess, TError> Validate(
		Func<TRequest, Option<TError>> validation)
	{
		_validations.Add(new WorkflowValidation<TRequest, TError>(
			(request, _) => Task.FromResult(validation(request))
		));
		return this;
	}

	/// <summary>
	/// Adds an activity to the workflow.
	/// </summary>
	/// <param name="activity">The activity function</param>
	/// <returns>The builder instance for method chaining</returns>
	public WorkflowBuilder<TRequest, TPayload, TSuccess, TError> Do(
		Func<TPayload, CancellationToken, Task<Either<TError, TPayload>>> activity)
	{
		_activities.Add(new WorkflowActivity<TPayload, TError>(activity));
		return this;
	}

	/// <summary>
	/// Adds a synchronous activity to the workflow.
	/// </summary>
	/// <param name="activity">The activity function</param>
	/// <returns>The builder instance for method chaining</returns>
	public WorkflowBuilder<TRequest, TPayload, TSuccess, TError> Do(
		Func<TPayload, Either<TError, TPayload>> activity)
	{
		_activities.Add(new WorkflowActivity<TPayload, TError>(
			(payload, _) => Task.FromResult(activity(payload))
		));
		return this;
	}

	/// <summary>
	/// Adds multiple activities to the workflow.
	/// </summary>
	/// <param name="activities">The activity functions</param>
	/// <returns>The builder instance for method chaining</returns>
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
	/// <param name="activities">The activity functions</param>
	/// <returns>The builder instance for method chaining</returns>
	public WorkflowBuilder<TRequest, TPayload, TSuccess, TError> DoAll(
		params Func<TPayload, Either<TError, TPayload>>[] activities)
	{
		foreach (var activity in activities)
		{
			_activities.Add(new WorkflowActivity<TPayload, TError>(
				(payload, _) => Task.FromResult(activity(payload))
			));
		}
		return this;
	}

	/// <summary>
	/// Adds a conditional activity to the workflow that will only execute if the condition returns true.
	/// </summary>
	/// <param name="condition">The condition to evaluate</param>
	/// <param name="activity">The activity to execute if the condition is true</param>
	/// <returns>The builder instance for method chaining</returns>
	public WorkflowBuilder<TRequest, TPayload, TSuccess, TError> DoIf(
		Func<TPayload, bool> condition,
		Func<TPayload, CancellationToken, Task<Either<TError, TPayload>>> activity)
	{
		_conditionalActivities.Add(
			new ConditionalWorkflowActivity<TPayload, TError>(
				condition,
				new WorkflowActivity<TPayload, TError>(activity)
			)
		);
		return this;
	}

	/// <summary>
	/// Adds a synchronous conditional activity to the workflow that will only execute if the condition returns true.
	/// </summary>
	/// <param name="condition">The condition to evaluate</param>
	/// <param name="activity">The activity to execute if the condition is true</param>
	/// <returns>The builder instance for method chaining</returns>
	public WorkflowBuilder<TRequest, TPayload, TSuccess, TError> DoIf(
		Func<TPayload, bool> condition,
		Func<TPayload, Either<TError, TPayload>> activity)
	{
		_conditionalActivities.Add(
			new ConditionalWorkflowActivity<TPayload, TError>(
				condition,
				new WorkflowActivity<TPayload, TError>(
					(payload, _) => Task.FromResult(activity(payload))
				)
			)
		);
		return this;
	}

	/// <summary>
	/// Creates a branch in the workflow that will only execute if the condition is true.
	/// </summary>
	/// <param name="condition">The condition to evaluate</param>
	/// <returns>A branch builder that allows adding activities to the branch</returns>
	public BranchBuilder<TRequest, TPayload, TSuccess, TError> Branch(Func<TPayload, bool> condition)
	{
		var branch = new Branch<TPayload, TError>(condition);
		_branches.Add(branch);
		return new BranchBuilder<TRequest, TPayload, TSuccess, TError>(this, branch);
	}

	/// <summary>
	/// Creates a branch in the workflow that will only execute if the condition is true.
	/// </summary>
	/// <param name="condition">The condition to evaluate</param>
	/// <param name="branchConfiguration">An action that configures the branch</param>
	/// <returns>The workflow builder to continue the workflow definition</returns>
	public WorkflowBuilder<TRequest, TPayload, TSuccess, TError> Branch(
		Func<TPayload, bool> condition,
		Action<BranchBuilder<TRequest, TPayload, TSuccess, TError>> branchConfiguration)
	{
		var branch = new Branch<TPayload, TError>(condition);
		_branches.Add(branch);
		var branchBuilder = new BranchBuilder<TRequest, TPayload, TSuccess, TError>(this, branch);
		branchConfiguration(branchBuilder);
		return this;
	}

	/// <summary>
	/// Adds an activity to the finally block that will always execute, even if the workflow fails.
	/// </summary>
	/// <param name="activity">The activity to execute</param>
	/// <returns>The builder instance for method chaining</returns>
	public WorkflowBuilder<TRequest, TPayload, TSuccess, TError> Finally(
		Func<TPayload, CancellationToken, Task<Either<TError, TPayload>>> activity)
	{
		_finallyActivities.Add(new WorkflowActivity<TPayload, TError>(activity));
		return this;
	}

	/// <summary>
	/// Adds a synchronous activity to the finally block that will always execute, even if the workflow fails.
	/// </summary>
	/// <param name="activity">The activity to execute</param>
	/// <returns>The builder instance for method chaining</returns>
	public WorkflowBuilder<TRequest, TPayload, TSuccess, TError> Finally(
		Func<TPayload, Either<TError, TPayload>> activity)
	{
		_finallyActivities.Add(new WorkflowActivity<TPayload, TError>(
			(payload, _) => Task.FromResult(activity(payload))
		));
		return this;
	}

	/// <summary>
	/// Builds a workflow that can be executed with a request of type <typeparamref name="TRequest"/>.
	/// </summary>
	/// <returns>A workflow that can be executed with a request of type <typeparamref name="TRequest"/>.</returns>
	public Workflow<TRequest, TSuccess, TError> Build()
	{
		return new Workflow<TRequest, TSuccess, TError>(
			async (request, cancellationToken) =>
			{
				// Run validations
				foreach (var validation in _validations)
				{
					var validationResult = await validation.Validate(request, cancellationToken);
					if (validationResult.IsSome)
					{
						return Either<TError, TSuccess>.FromLeft(validationResult.Value);
					}
				}

				// Create initial payload
				var payload = _contextFactory(request);

				// Execute main activities
				try
				{
					foreach (var activity in _activities)
					{
						var activityResult = await activity.Execute(payload, cancellationToken);
						if (activityResult.IsLeft)
						{
							return Either<TError, TSuccess>.FromLeft(activityResult.Left);
						}

						payload = activityResult.Right;
					}

					// Execute conditional activities
					foreach (var conditionalActivity in _conditionalActivities)
					{
						if (conditionalActivity.ShouldExecute(payload))
						{
							var activityResult = await conditionalActivity.Activity.Execute(payload, cancellationToken);
							if (activityResult.IsLeft)
							{
								return Either<TError, TSuccess>.FromLeft(activityResult.Left);
							}

							payload = activityResult.Right;
						}
					}

					// Execute branches
					foreach (var branch in _branches)
					{
						if (branch.Condition(payload))
						{
							foreach (var activity in branch.Activities)
							{
								var activityResult = await activity.Execute(payload, cancellationToken);
								if (activityResult.IsLeft)
								{
									return Either<TError, TSuccess>.FromLeft(activityResult.Left);
								}

								payload = activityResult.Right;
							}
						}
					}

					// Create success result
					var success = _resultSelector(payload);
					return Either<TError, TSuccess>.FromRight(success);
				}
				finally
				{
					// Execute finally activities
					foreach (var finallyActivity in _finallyActivities)
					{
						// Ignore errors from finally activities
						_ = await finallyActivity.Execute(payload, cancellationToken);
					}
				}
			}
		);
	}
}
