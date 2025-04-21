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

					// Execute branches with local payload
					foreach (var branchObj in _branchesWithLocalPayload)
					{
						var branchResult = await ExecuteBranchWithLocalPayloadDynamic(branchObj, payload, cancellationToken);
						if (branchResult.IsLeft)
						{
							return Either<TError, TSuccess>.FromLeft(branchResult.Left);
						}

						payload = branchResult.Right;
					}

					// Execute workflow features (Group, WithContext, Detach, Parallel, etc.)
					foreach (var feature in _features)
					{
						// Skip if the condition is false
						if (feature.Condition != null && !feature.Condition(payload))
						{
							continue;
						}

						// Execute the feature
						var featureResult = await ExecuteFeatureDynamic(feature, payload, cancellationToken);
						if (featureResult.IsLeft)
						{
							return Either<TError, TSuccess>.FromLeft(featureResult.Left);
						}

						if (feature.ShouldMerge)
						{
							payload = featureResult.Right;
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
				if (result.IsLeft)
				{
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
				var sourceType = source.GetType();
				var targetType = mergedPayload.GetType();

				// Get all properties from the payload type
				var properties = sourceType.GetProperties();

				foreach (var property in properties)
				{
					var sourceValue = property.GetValue(source);
					var defaultValue = property.PropertyType.IsValueType ?
						Activator.CreateInstance(property.PropertyType) : null;

					// Only copy non-default values (like Sum, Product, etc.)
					if (sourceValue != null && !sourceValue.Equals(defaultValue))
					{
						property.SetValue(mergedPayload, sourceValue);
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
			}

			// Return original payload since parallel detached execution doesn't affect the main flow
			return Either<TError, TPayload>.FromRight(payload);
		}
		// Special handling for Context<,>
		else if (feature.GetType().IsGenericType &&
			feature.GetType().GetGenericTypeDefinition() == typeof(Features.Context.Context<,,>))
		{
			var typeArgs = feature.GetType().GetGenericArguments();
			var localStateType = typeArgs[1];

			// Get the generic method and make it specific to the local state type
			var method = GetType().GetMethod(nameof(ExecuteContext),
				System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
			var genericMethod = method!.MakeGenericMethod(localStateType);

			// Invoke the method with the right generic parameter
			return (Either<TError, TPayload>)await (Task<Either<TError, TPayload>>)
				genericMethod.Invoke(this, new object[] { feature, payload, cancellationToken })!;
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
		var currentPayload = payload;

		foreach (var activity in activities)
		{
			var activityResult = await activity.Execute(currentPayload, cancellationToken);
			if (activityResult.IsLeft)
			{
				return Either<TError, TPayload>.FromLeft(activityResult.Left);
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
		// Check if the condition is met (null condition means always execute)
		if (context.Condition != null && !context.Condition(payload))
		{
			return Either<TError, TPayload>.FromRight(payload);
		}

		// Create the local state
		var localState = context.LocalStateFactory(payload);

		// Execute the context activities
		foreach (var activity in context.Activities)
		{
			var activityResult = await activity.Execute(payload, localState, cancellationToken);
			if (activityResult.IsLeft)
			{
				return Either<TError, TPayload>.FromLeft(activityResult.Left);
			}

			// Update both payload and local state
			(payload, localState) = activityResult.Right;
		}

		return Either<TError, TPayload>.FromRight(payload);
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
				genericMethod.Invoke(this, new object[] { branchObj, payload, cancellationToken })!;
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
