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
	private readonly List<object> _branchesWithLocalPayload = [];

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
	/// Creates a branch in the workflow that always executes.
	/// This is a convenience method for organizing related activities.
	/// </summary>
	/// <param name="branchConfiguration">An action that configures the branch</param>
	/// <returns>The workflow builder to continue the workflow definition</returns>
	public WorkflowBuilder<TRequest, TPayload, TSuccess, TError> Branch(
		Action<BranchBuilder<TRequest, TPayload, TSuccess, TError>> branchConfiguration)
	{
		// Create a branch with a condition that always returns true
		var branch = new Branch<TPayload, TError>(_ => true);
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
	/// Creates a branch in the workflow with a local payload that will only execute if the condition is true.
	/// </summary>
	/// <typeparam name="TLocalPayload">The type of the local branch payload</typeparam>
	/// <param name="condition">The condition to evaluate</param>
	/// <param name="localPayloadFactory">The factory function that creates the local payload</param>
	/// <returns>A branch builder that allows adding activities to the branch</returns>
	public BranchWithLocalPayloadBuilder<TRequest, TPayload, TLocalPayload, TSuccess, TError> BranchWithLocalPayload<TLocalPayload>(
		Func<TPayload, bool> condition,
		Func<TPayload, TLocalPayload> localPayloadFactory)
	{
		var branch = new BranchWithLocalPayload<TPayload, TLocalPayload, TError>(condition, localPayloadFactory);
		_branchesWithLocalPayload.Add(branch);
		return new BranchWithLocalPayloadBuilder<TRequest, TPayload, TLocalPayload, TSuccess, TError>(this, branch);
	}

	/// <summary>
	/// Creates a branch in the workflow with a local payload that will only execute if the condition is true.
	/// </summary>
	/// <typeparam name="TLocalPayload">The type of the local branch payload</typeparam>
	/// <param name="condition">The condition to evaluate</param>
	/// <param name="localPayloadFactory">The factory function that creates the local payload</param>
	/// <param name="branchConfiguration">An action that configures the branch</param>
	/// <returns>The workflow builder to continue the workflow definition</returns>
	public WorkflowBuilder<TRequest, TPayload, TSuccess, TError> BranchWithLocalPayload<TLocalPayload>(
		Func<TPayload, bool> condition,
		Func<TPayload, TLocalPayload> localPayloadFactory,
		Action<BranchWithLocalPayloadBuilder<TRequest, TPayload, TLocalPayload, TSuccess, TError>> branchConfiguration)
	{
		var branch = new BranchWithLocalPayload<TPayload, TLocalPayload, TError>(condition, localPayloadFactory);
		_branchesWithLocalPayload.Add(branch);
		var branchBuilder = new BranchWithLocalPayloadBuilder<TRequest, TPayload, TLocalPayload, TSuccess, TError>(this, branch);
		branchConfiguration(branchBuilder);
		return this;
	}

	/// <summary>
	/// Creates a branch in the workflow with a local payload that always executes.
	/// This is a convenience method for organizing related activities.
	/// </summary>
	/// <typeparam name="TLocalPayload">The type of the local branch payload</typeparam>
	/// <param name="localPayloadFactory">The factory function that creates the local payload</param>
	/// <returns>A branch builder that allows adding activities to the branch</returns>
	public BranchWithLocalPayloadBuilder<TRequest, TPayload, TLocalPayload, TSuccess, TError> BranchWithLocalPayload<TLocalPayload>(
		Func<TPayload, TLocalPayload> localPayloadFactory)
	{
		var branch = new BranchWithLocalPayload<TPayload, TLocalPayload, TError>(_ => true, localPayloadFactory);
		_branchesWithLocalPayload.Add(branch);
		return new BranchWithLocalPayloadBuilder<TRequest, TPayload, TLocalPayload, TSuccess, TError>(this, branch);
	}

	/// <summary>
	/// Creates a branch in the workflow with a local payload that always executes.
	/// This is a convenience method for organizing related activities.
	/// </summary>
	/// <typeparam name="TLocalPayload">The type of the local branch payload</typeparam>
	/// <param name="localPayloadFactory">The factory function that creates the local payload</param>
	/// <param name="branchConfiguration">An action that configures the branch</param>
	/// <returns>The workflow builder to continue the workflow definition</returns>
	public WorkflowBuilder<TRequest, TPayload, TSuccess, TError> BranchWithLocalPayload<TLocalPayload>(
		Func<TPayload, TLocalPayload> localPayloadFactory,
		Action<BranchWithLocalPayloadBuilder<TRequest, TPayload, TLocalPayload, TSuccess, TError>> branchConfiguration)
	{
		// Create a branch with a condition that always returns true
		var branch = new BranchWithLocalPayload<TPayload, TLocalPayload, TError>(_ => true, localPayloadFactory);
		_branchesWithLocalPayload.Add(branch);
		var branchBuilder = new BranchWithLocalPayloadBuilder<TRequest, TPayload, TLocalPayload, TSuccess, TError>(this, branch);
		branchConfiguration(branchBuilder);
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

					// Execute branches with local payloads
					foreach (var branchObj in _branchesWithLocalPayload)
					{
						var branchResult = await ExecuteBranchWithLocalPayloadDynamic(branchObj, payload, cancellationToken);
						if (branchResult.IsLeft)
						{
							return Either<TError, TSuccess>.FromLeft(branchResult.Left);
						}

						payload = branchResult.Right;
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

	// Dynamic helper to handle branches with different local payload types
	private async Task<Either<TError, TPayload>> ExecuteBranchWithLocalPayloadDynamic(
		object branchObj,
		TPayload payload,
		CancellationToken cancellationToken)
	{
		// Use reflection to call the appropriate generic method
		var branchType = branchObj.GetType();
		if (branchType.IsGenericType &&
			branchType.GetGenericTypeDefinition() == typeof(BranchWithLocalPayload<,,>))
		{
			var typeArgs = branchType.GetGenericArguments();
			var localPayloadType = typeArgs[1];

			// Get the generic method and make it specific to the local payload type
			var method = GetType().GetMethod(nameof(ExecuteBranchWithLocalPayload),
				System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
			var genericMethod = method!.MakeGenericMethod(localPayloadType);

			// Invoke the method with the right generic parameter
			return (Either<TError, TPayload>)await (Task<Either<TError, TPayload>>)
				genericMethod.Invoke(this, new[] { branchObj, payload, cancellationToken })!;
		}

		// If branch type isn't recognized, just return the payload unchanged
		return Either<TError, TPayload>.FromRight(payload);
	}

	// Helper method to execute a branch with local payload
	private async Task<Either<TError, TPayload>> ExecuteBranchWithLocalPayload<TLocalPayload>(
		BranchWithLocalPayload<TPayload, TLocalPayload, TError> branch,
		TPayload payload,
		CancellationToken cancellationToken)
	{
		if (!branch.Condition(payload))
		{
			return Either<TError, TPayload>.FromRight(payload);
		}

		// Create the local payload
		var localPayload = branch.LocalPayloadFactory(payload);

		// Execute the branch activities
		foreach (var activity in branch.Activities)
		{
			var activityResult = await activity.Execute(payload, localPayload, cancellationToken);
			if (activityResult.IsLeft)
			{
				return Either<TError, TPayload>.FromLeft(activityResult.Left);
			}

			// Update both payloads
			(payload, localPayload) = activityResult.Right;
		}

		return Either<TError, TPayload>.FromRight(payload);
	}
}
