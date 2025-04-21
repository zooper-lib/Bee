using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Zooper.Bee.Features;
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

	private readonly List<WorkflowValidation<TRequest, TError>> _validations = new();
	private readonly List<WorkflowActivity<TPayload, TError>> _activities = new();
	private readonly List<ConditionalWorkflowActivity<TPayload, TError>> _conditionalActivities = new();
	private readonly List<WorkflowActivity<TPayload, TError>> _finallyActivities = new();
	private readonly List<Branch<TPayload, TError>> _branches = new();
	private readonly List<object> _branchesWithLocalPayload = new();

	// Collections for new features
	private readonly List<IWorkflowFeature<TPayload, TError>> _features = new();

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
	[Obsolete("Use Group() method instead. This method will be removed in a future version.")]
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
	[Obsolete("Use Group() method instead. This method will be removed in a future version.")]
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
	/// Creates an unconditional branch in the workflow. (Always executes)
	/// </summary>
	/// <param name="branchConfiguration">An action that configures the branch</param>
	/// <returns>The workflow builder to continue the workflow definition</returns>
	[Obsolete("Use Group() method instead. This method will be removed in a future version.")]
	public WorkflowBuilder<TRequest, TPayload, TSuccess, TError> Branch(
		Action<BranchBuilder<TRequest, TPayload, TSuccess, TError>> branchConfiguration)
	{
		return Branch(_ => true, branchConfiguration);
	}

	/// <summary>
	/// Creates a group of activities in the workflow with an optional condition.
	/// </summary>
	/// <param name="condition">The condition to evaluate. If null, the group always executes.</param>
	/// <param name="groupConfiguration">An action that configures the group</param>
	/// <returns>The workflow builder to continue the workflow definition</returns>
	public WorkflowBuilder<TRequest, TPayload, TSuccess, TError> Group(
		Func<TPayload, bool>? condition,
		Action<Features.Group.GroupBuilder<TRequest, TPayload, TSuccess, TError>> groupConfiguration)
	{
		var group = new Features.Group.Group<TPayload, TError>(condition);
		_features.Add(group);
		var groupBuilder = new Features.Group.GroupBuilder<TRequest, TPayload, TSuccess, TError>(this, group);
		groupConfiguration(groupBuilder);
		return this;
	}

	/// <summary>
	/// Creates a group of activities in the workflow that always executes.
	/// </summary>
	/// <param name="groupConfiguration">An action that configures the group</param>
	/// <returns>The workflow builder to continue the workflow definition</returns>
	public WorkflowBuilder<TRequest, TPayload, TSuccess, TError> Group(
		Action<Features.Group.GroupBuilder<TRequest, TPayload, TSuccess, TError>> groupConfiguration)
	{
		return Group(null, groupConfiguration);
	}

	/// <summary>
	/// Creates a branch in the workflow with a local payload that will only execute if the condition is true.
	/// </summary>
	/// <typeparam name="TLocalPayload">The type of the local branch payload</typeparam>
	/// <param name="condition">The condition to evaluate</param>
	/// <param name="localPayloadFactory">The factory function that creates the local payload</param>
	/// <returns>A branch builder that allows adding activities to the branch</returns>
	[Obsolete("Use WithContext() method instead. This method will be removed in a future version.")]
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
	[Obsolete("Use WithContext() method instead. This method will be removed in a future version.")]
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
	[Obsolete("Use WithContext() method instead. This method will be removed in a future version.")]
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
	[Obsolete("Use WithContext() method instead. This method will be removed in a future version.")]
	public WorkflowBuilder<TRequest, TPayload, TSuccess, TError> BranchWithLocalPayload<TLocalPayload>(
		Func<TPayload, TLocalPayload> localPayloadFactory,
		Action<BranchWithLocalPayloadBuilder<TRequest, TPayload, TLocalPayload, TSuccess, TError>> branchConfiguration)
	{
		return BranchWithLocalPayload(_ => true, localPayloadFactory, branchConfiguration);
	}

	/// <summary>
	/// Creates a context with local state in the workflow and an optional condition.
	/// </summary>
	/// <typeparam name="TLocalState">The type of the local context state</typeparam>
	/// <param name="condition">The condition to evaluate. If null, the context always executes.</param>
	/// <param name="localStateFactory">The factory function that creates the local state</param>
	/// <param name="contextConfiguration">An action that configures the context</param>
	/// <returns>The workflow builder to continue the workflow definition</returns>
	public WorkflowBuilder<TRequest, TPayload, TSuccess, TError> WithContext<TLocalState>(
		Func<TPayload, bool>? condition,
		Func<TPayload, TLocalState> localStateFactory,
		Action<Features.Context.ContextBuilder<TRequest, TPayload, TLocalState, TSuccess, TError>> contextConfiguration)
	{
		var context = new Features.Context.Context<TPayload, TLocalState, TError>(condition, localStateFactory);
		_features.Add(context);
		var contextBuilder = new Features.Context.ContextBuilder<TRequest, TPayload, TLocalState, TSuccess, TError>(this, context);
		contextConfiguration(contextBuilder);
		return this;
	}

	/// <summary>
	/// Creates a context with local state in the workflow that always executes.
	/// </summary>
	/// <typeparam name="TLocalState">The type of the local context state</typeparam>
	/// <param name="localStateFactory">The factory function that creates the local state</param>
	/// <param name="contextConfiguration">An action that configures the context</param>
	/// <returns>The workflow builder to continue the workflow definition</returns>
	public WorkflowBuilder<TRequest, TPayload, TSuccess, TError> WithContext<TLocalState>(
		Func<TPayload, TLocalState> localStateFactory,
		Action<Features.Context.ContextBuilder<TRequest, TPayload, TLocalState, TSuccess, TError>> contextConfiguration)
	{
		return WithContext(null, localStateFactory, contextConfiguration);
	}

	/// <summary>
	/// Creates a detached group of activities in the workflow with an optional condition.
	/// Detached groups don't merge their results back into the main workflow.
	/// </summary>
	/// <param name="condition">The condition to evaluate. If null, the detached group always executes.</param>
	/// <param name="detachedConfiguration">An action that configures the detached group</param>
	/// <returns>The workflow builder to continue the workflow definition</returns>
	public WorkflowBuilder<TRequest, TPayload, TSuccess, TError> Detach(
		Func<TPayload, bool>? condition,
		Action<Features.Detached.DetachedBuilder<TRequest, TPayload, TSuccess, TError>> detachedConfiguration)
	{
		var detached = new Features.Detached.Detached<TPayload, TError>(condition);
		_features.Add(detached);
		var detachedBuilder = new Features.Detached.DetachedBuilder<TRequest, TPayload, TSuccess, TError>(this, detached);
		detachedConfiguration(detachedBuilder);
		return this;
	}

	/// <summary>
	/// Creates a detached group of activities in the workflow that always executes.
	/// Detached groups don't merge their results back into the main workflow.
	/// </summary>
	/// <param name="detachedConfiguration">An action that configures the detached group</param>
	/// <returns>The workflow builder to continue the workflow definition</returns>
	public WorkflowBuilder<TRequest, TPayload, TSuccess, TError> Detach(
		Action<Features.Detached.DetachedBuilder<TRequest, TPayload, TSuccess, TError>> detachedConfiguration)
	{
		return Detach(null, detachedConfiguration);
	}

	/// <summary>
	/// Creates a parallel execution of multiple groups with an optional condition.
	/// All groups execute in parallel and their results are merged back into the main workflow.
	/// </summary>
	/// <param name="condition">The condition to evaluate. If null, the parallel execution always occurs.</param>
	/// <param name="parallelConfiguration">An action that configures the parallel execution</param>
	/// <returns>The workflow builder to continue the workflow definition</returns>
	public WorkflowBuilder<TRequest, TPayload, TSuccess, TError> Parallel(
		Func<TPayload, bool>? condition,
		Action<Features.Parallel.ParallelBuilder<TRequest, TPayload, TSuccess, TError>> parallelConfiguration)
	{
		var parallel = new Features.Parallel.Parallel<TPayload, TError>(condition);
		_features.Add(parallel);
		var parallelBuilder = new Features.Parallel.ParallelBuilder<TRequest, TPayload, TSuccess, TError>(this, parallel);
		parallelConfiguration(parallelBuilder);
		return this;
	}

	/// <summary>
	/// Creates a parallel execution of multiple groups that always executes.
	/// All groups execute in parallel and their results are merged back into the main workflow.
	/// </summary>
	/// <param name="parallelConfiguration">An action that configures the parallel execution</param>
	/// <returns>The workflow builder to continue the workflow definition</returns>
	public WorkflowBuilder<TRequest, TPayload, TSuccess, TError> Parallel(
		Action<Features.Parallel.ParallelBuilder<TRequest, TPayload, TSuccess, TError>> parallelConfiguration)
	{
		return Parallel(null, parallelConfiguration);
	}

	/// <summary>
	/// Creates a parallel execution of multiple detached groups with an optional condition.
	/// All detached groups execute in parallel and their results are NOT merged back.
	/// </summary>
	/// <param name="condition">The condition to evaluate. If null, the parallel detached execution always occurs.</param>
	/// <param name="parallelDetachedConfiguration">An action that configures the parallel detached execution</param>
	/// <returns>The workflow builder to continue the workflow definition</returns>
	public WorkflowBuilder<TRequest, TPayload, TSuccess, TError> ParallelDetached(
		Func<TPayload, bool>? condition,
		Action<Features.Parallel.ParallelDetachedBuilder<TRequest, TPayload, TSuccess, TError>> parallelDetachedConfiguration)
	{
		var parallelDetached = new Features.Parallel.ParallelDetached<TPayload, TError>(condition);
		_features.Add(parallelDetached);
		var parallelDetachedBuilder = new Features.Parallel.ParallelDetachedBuilder<TRequest, TPayload, TSuccess, TError>(this, parallelDetached);
		parallelDetachedConfiguration(parallelDetachedBuilder);
		return this;
	}

	/// <summary>
	/// Creates a parallel execution of multiple detached groups that always executes.
	/// All detached groups execute in parallel and their results are NOT merged back.
	/// </summary>
	/// <param name="parallelDetachedConfiguration">An action that configures the parallel detached execution</param>
	/// <returns>The workflow builder to continue the workflow definition</returns>
	public WorkflowBuilder<TRequest, TPayload, TSuccess, TError> ParallelDetached(
		Action<Features.Parallel.ParallelDetachedBuilder<TRequest, TPayload, TSuccess, TError>> parallelDetachedConfiguration)
	{
		return ParallelDetached(null, parallelDetachedConfiguration);
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
					// Skip null validations
					if (validation == null)
					{
						continue;
					}

					var validationResult = await validation.Validate(request, cancellationToken);
					if (validationResult.IsSome)
					{
						var errorValue = validationResult.Value;
						// Skip if error value is null
						if (errorValue == null)
						{
							continue;
						}
						return Either<TError, TSuccess>.FromLeft(errorValue);
					}
				}

				// Create initial payload
				var payload = _contextFactory(request);

				// Skip if payload is null
				if (payload == null)
				{
					// Return a default success with default payload
					return Either<TError, TSuccess>.FromRight(_resultSelector(default!));
				}

				// Execute main activities
				try
				{
					foreach (var activity in _activities)
					{
						// Skip null activities
						if (activity == null)
						{
							continue;
						}

						var activityResult = await activity.Execute(payload, cancellationToken);
						if (activityResult == null)
						{
							continue;
						}

						if (activityResult.IsLeft)
						{
							var errorValue = activityResult.Left;
							// Skip if error value is null
							if (errorValue == null)
							{
								continue;
							}
							return Either<TError, TSuccess>.FromLeft(errorValue);
						}

						// Skip if result is null
						if (activityResult.Right == null)
						{
							continue;
						}
						payload = activityResult.Right;
					}

					// Execute conditional activities
					foreach (var conditionalActivity in _conditionalActivities)
					{
						// Skip null conditional activities
						if (conditionalActivity == null)
						{
							continue;
						}

						if (conditionalActivity.ShouldExecute(payload))
						{
							// Skip if activity is null
							if (conditionalActivity.Activity == null)
							{
								continue;
							}

							var activityResult = await conditionalActivity.Activity.Execute(payload, cancellationToken);
							if (activityResult == null)
							{
								continue;
							}

							if (activityResult.IsLeft)
							{
								var errorValue = activityResult.Left;
								// Skip if error value is null
								if (errorValue == null)
								{
									continue;
								}
								return Either<TError, TSuccess>.FromLeft(errorValue);
							}

							// Skip if result is null
							if (activityResult.Right == null)
							{
								continue;
							}
							payload = activityResult.Right;
						}
					}

					// Execute branches
					foreach (var branch in _branches)
					{
						// Skip null branches
						if (branch == null)
						{
							continue;
						}

						// Skip if condition is null
						if (branch.Condition == null)
						{
							continue;
						}

						if (branch.Condition(payload))
						{
							// Skip if activities collection is null
							if (branch.Activities == null)
							{
								continue;
							}

							foreach (var activity in branch.Activities)
							{
								// Skip null activities
								if (activity == null)
								{
									continue;
								}

								var activityResult = await activity.Execute(payload, cancellationToken);
								if (activityResult == null)
								{
									continue;
								}

								if (activityResult.IsLeft)
								{
									var errorValue = activityResult.Left;
									// Skip if error value is null
									if (errorValue == null)
									{
										continue;
									}
									return Either<TError, TSuccess>.FromLeft(errorValue);
								}

								// Skip if result is null
								if (activityResult.Right == null)
								{
									continue;
								}
								payload = activityResult.Right;
							}
						}
					}

					// Execute branches with local payload
					foreach (var branchObj in _branchesWithLocalPayload)
					{
						// Skip null branch objects
						if (branchObj == null)
						{
							continue;
						}

						var branchResult = await ExecuteBranchWithLocalPayloadDynamic(branchObj, payload, cancellationToken);
						if (branchResult == null)
						{
							continue;
						}

						if (branchResult.IsLeft)
						{
							var errorValue = branchResult.Left;
							// Skip if error value is null
							if (errorValue == null)
							{
								continue;
							}
							return Either<TError, TSuccess>.FromLeft(errorValue);
						}

						// Skip if result is null
						if (branchResult.Right == null)
						{
							continue;
						}
						payload = branchResult.Right;
					}

					// Execute workflow features (Group, WithContext, Detach, Parallel, etc.)
					foreach (var feature in _features)
					{
						// Skip null features
						if (feature == null)
						{
							continue;
						}

						// Skip if the condition is false
						if (feature.Condition != null && !feature.Condition(payload))
						{
							continue;
						}

						// Execute the feature
						var featureResult = await ExecuteFeatureDynamic(feature, payload, cancellationToken);
						if (featureResult == null)
						{
							continue;
						}

						if (featureResult.IsLeft)
						{
							var errorValue = featureResult.Left;
							// Skip if error value is null
							if (errorValue == null)
							{
								continue;
							}
							return Either<TError, TSuccess>.FromLeft(errorValue);
						}

						if (feature.ShouldMerge)
						{
							// Skip if result is null
							if (featureResult.Right == null)
							{
								continue;
							}
							payload = featureResult.Right;
						}
					}

					// Create success result
					var success = _resultSelector(payload);

					// Skip if success result is null
					if (success == null)
					{
						// Return an empty success result
						return Either<TError, TSuccess>.FromRight(default!);
					}

					return Either<TError, TSuccess>.FromRight(success);
				}
				finally
				{
					// Execute finally activities
					foreach (var finallyActivity in _finallyActivities)
					{
						// Skip null finally activities
						if (finallyActivity == null)
						{
							continue;
						}

						// Skip if payload is null
						if (payload == null)
						{
							continue;
						}

						// Ignore errors from finally activities
						_ = await finallyActivity.Execute(payload, cancellationToken);
					}
				}
			}
		);
	}

	// Dynamic helper for features that can't be handled directly
	private async Task<Either<TError, TPayload>> ExecuteFeatureDynamic(
		IWorkflowFeature<TPayload, TError> feature,
		TPayload payload,
		CancellationToken cancellationToken)
	{
		if (feature is Features.Group.Group<TPayload, TError> group)
		{
			// Execute group activities
			return await ExecuteGroupActivities(group.Activities, payload, cancellationToken);
		}
		else if (feature is Features.Detached.Detached<TPayload, TError> detached)
		{
			// Start detached activities but don't wait for them or use their results
			var detachedPayload = payload;

			// Disable the warning about not awaiting the Task.Run
#pragma warning disable CS4014
			Task.Run(async () =>
			{
				foreach (var activity in detached.Activities)
				{
					var activityResult = await activity.Execute(detachedPayload, cancellationToken);
					if (activityResult.IsLeft)
					{
						// Log or handle error if needed
						break;
					}

					detachedPayload = activityResult.Right;
				}
			}, cancellationToken);
#pragma warning restore CS4014

			// Return original payload since detached execution doesn't affect the main flow
			return Either<TError, TPayload>.FromRight(payload);
		}
		else if (feature is Features.Parallel.Parallel<TPayload, TError> parallel)
		{
			// Execute groups in parallel and merge results
			var tasks = new List<Task<Either<TError, TPayload>>>();
			foreach (var parallelGroup in parallel.Groups)
			{
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
		else if (feature is Features.Parallel.ParallelDetached<TPayload, TError> parallelDetached)
		{
			// Start detached groups in parallel but don't wait for them or use their results
			var detachedPayload = payload;
			foreach (var detachedGroup in parallelDetached.DetachedGroups)
			{
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
						var activityResult = await activity.Execute(localPayload, cancellationToken);
						if (activityResult.IsLeft)
						{
							// Log or handle error if needed
							break;
						}

						localPayload = activityResult.Right;
					}
				}, cancellationToken);
#pragma warning restore CS4014
			}

			// Return original payload since parallel detached execution doesn't affect the main flow
			return Either<TError, TPayload>.FromRight(payload);
		}
		// Special handling for Context<,>
		else if (feature != null && feature.GetType() != null && feature.GetType().IsGenericType &&
			feature.GetType().GetGenericTypeDefinition() != null &&
			feature.GetType().GetGenericTypeDefinition() == typeof(Features.Context.Context<,,>))
		{
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
				var method = GetType().GetMethod(nameof(ExecuteContext),
					System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

				// Check if method is null before using it
				if (method == null)
				{
					throw new InvalidOperationException($"Method {nameof(ExecuteContext)} not found.");
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

		// Default behavior for unknown features
		return Either<TError, TPayload>.FromRight(payload);
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

	// Helper method to execute a context
	private async Task<Either<TError, TPayload>> ExecuteContext<TLocalState>(
		Features.Context.Context<TPayload, TLocalState, TError> context,
		TPayload payload,
		CancellationToken cancellationToken)
	{
		// Check if context is null
		if (context == null)
		{
			return Either<TError, TPayload>.FromRight(payload);
		}

		// Check if the condition is met (null condition means always execute)
		if (context.Condition != null && !context.Condition(payload))
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
			if (activityResult.Right.Item1 != null)
			{
				payload = activityResult.Right.Item1;
			}
			if (activityResult.Right.Item2 != null)
			{
				localState = activityResult.Right.Item2;
			}
		}

		return Either<TError, TPayload>.FromRight(payload);
	}

	// Dynamic helper to handle branches with different local payload types
	private async Task<Either<TError, TPayload>> ExecuteBranchWithLocalPayloadDynamic(
		object branchObj,
		TPayload payload,
		CancellationToken cancellationToken)
	{
		// Skip if branch object is null
		if (branchObj == null)
		{
			return Either<TError, TPayload>.FromRight(payload);
		}

		// Use reflection to call the appropriate generic method
		var branchType = branchObj.GetType();

		// Skip if branch type is null
		if (branchType == null)
		{
			return Either<TError, TPayload>.FromRight(payload);
		}

		try
		{
			if (branchType.IsGenericType &&
				branchType.GetGenericTypeDefinition() == typeof(BranchWithLocalPayload<,,>))
			{
				var typeArgs = branchType.GetGenericArguments();
				if (typeArgs == null || typeArgs.Length < 2)
				{
					return Either<TError, TPayload>.FromRight(payload);
				}

				var localPayloadType = typeArgs[1];
				if (localPayloadType == null)
				{
					return Either<TError, TPayload>.FromRight(payload);
				}

				// Get the generic method and make it specific to the local payload type
				var method = GetType().GetMethod(nameof(ExecuteBranchWithLocalPayload),
					System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

				// Check if method is null before using it
				if (method == null)
				{
					throw new InvalidOperationException($"Method {nameof(ExecuteBranchWithLocalPayload)} not found.");
				}

				var genericMethod = method.MakeGenericMethod(localPayloadType);
				if (genericMethod == null)
				{
					return Either<TError, TPayload>.FromRight(payload);
				}

				// Ensure payload is not null before passing to the method
				if (payload == null)
				{
					payload = default!; // Use default value if null
				}

				// Invoke the method with the right generic parameter
				var result = genericMethod.Invoke(this, new object[] { branchObj, payload, cancellationToken });
				return result == null
					? throw new InvalidOperationException("Method invocation returned null.")
					: await (Task<Either<TError, TPayload>>)result;
			}
		}
		catch (Exception)
		{
			// If any reflection-related exception occurs, return the payload unchanged
			return Either<TError, TPayload>.FromRight(payload);
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
		// Check if branch is null
		if (branch == null)
		{
			return Either<TError, TPayload>.FromRight(payload);
		}

		// Check if condition is null
		if (branch.Condition == null)
		{
			return Either<TError, TPayload>.FromRight(payload);
		}

		if (!branch.Condition(payload))
		{
			return Either<TError, TPayload>.FromRight(payload);
		}

		// Check if local payload factory is null
		if (branch.LocalPayloadFactory == null)
		{
			return Either<TError, TPayload>.FromRight(payload);
		}

		// Create the local payload
		TLocalPayload? localPayload;
		try
		{
			localPayload = branch.LocalPayloadFactory(payload);
		}
		catch (Exception)
		{
			// If we can't create the local payload, return the payload unchanged
			return Either<TError, TPayload>.FromRight(payload);
		}

		// Check if activities collection is null
		if (branch.Activities == null)
		{
			return Either<TError, TPayload>.FromRight(payload);
		}

		// Execute the branch activities
		foreach (var activity in branch.Activities)
		{
			// Skip null activities
			if (activity == null)
			{
				continue;
			}

			var activityResult = await activity.Execute(payload, localPayload, cancellationToken);
			if (activityResult == null)
			{
				// Skip if the activity result is null
				continue;
			}

			if (activityResult.IsLeft)
			{
				return Either<TError, TPayload>.FromLeft(activityResult.Left);
			}

			// Update both payloads
			if (activityResult.Right.Item1 != null)
			{
				payload = activityResult.Right.Item1;
			}
			if (activityResult.Right.Item2 != null)
			{
				localPayload = activityResult.Right.Item2;
			}
		}

		return Either<TError, TPayload>.FromRight(payload);
	}
}
