using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Zooper.Bee.Features;
using Zooper.Bee.Internal;
using Zooper.Bee.Internal.Executors;
using Zooper.Fox;

// ReSharper disable MemberCanBePrivate.Global

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

	private readonly List<WorkflowGuard<TRequest, TError>> _guards = [];
	private readonly List<WorkflowValidation<TRequest, TError>> _validations = [];
	private readonly List<WorkflowActivity<TPayload, TError>> _activities = [];
	private readonly List<ConditionalWorkflowActivity<TPayload, TError>> _conditionalActivities = [];
	private readonly List<WorkflowActivity<TPayload, TError>> _finallyActivities = [];
	private readonly List<Branch<TPayload, TError>> _branches = [];
	private readonly List<object> _branchesWithLocalPayload = [];

	// Collections for new features
	private readonly List<IWorkflowFeature<TPayload, TError>> _features = [];

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
		_validations.Add(new(validation));
		return this;
	}

	/// <summary>
	/// Adds a synchronous validation rule to the workflow.
	/// </summary>
	/// <param name="validation">The validation function</param>
	/// <returns>The builder instance for method chaining</returns>
	public WorkflowBuilder<TRequest, TPayload, TSuccess, TError> Validate(Func<TRequest, Option<TError>> validation)
	{
		_validations.Add(
			new((
					request,
					_) => Task.FromResult(validation(request))
			)
		);
		return this;
	}

	/// <summary>
	/// Adds a guard to check if the workflow can be executed.
	/// Guards are evaluated before any validations or activities.
	/// If a guard fails, the workflow will not execute and will return the error.
	/// </summary>
	/// <param name="guard">The guard function that returns Either an error or Unit</param>
	/// <returns>The builder instance for method chaining</returns>
	public WorkflowBuilder<TRequest, TPayload, TSuccess, TError> Guard(
		Func<TRequest, CancellationToken, Task<Either<TError, Unit>>> guard)
	{
		_guards.Add(new(guard));
		return this;
	}

	/// <summary>
	/// Adds a synchronous guard to check if the workflow can be executed.
	/// </summary>
	/// <param name="guard">The guard function that returns Either an error or Unit</param>
	/// <returns>The builder instance for method chaining</returns>
	public WorkflowBuilder<TRequest, TPayload, TSuccess, TError> Guard(Func<TRequest, Either<TError, Unit>> guard)
	{
		_guards.Add(
			new((
					request,
					_) => Task.FromResult(guard(request))
			)
		);
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
		_activities.Add(new(activity));
		return this;
	}

	/// <summary>
	/// Adds a synchronous activity to the workflow.
	/// </summary>
	/// <param name="activity">The activity function</param>
	/// <returns>The builder instance for method chaining</returns>
	public WorkflowBuilder<TRequest, TPayload, TSuccess, TError> Do(Func<TPayload, Either<TError, TPayload>> activity)
	{
		_activities.Add(
			new((
					payload,
					_) => Task.FromResult(activity(payload))
			)
		);
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
			_activities.Add(new(activity));
		}

		return this;
	}

	/// <summary>
	/// Adds multiple synchronous activities to the workflow.
	/// </summary>
	/// <param name="activities">The activity functions</param>
	/// <returns>The builder instance for method chaining</returns>
	public WorkflowBuilder<TRequest, TPayload, TSuccess, TError> DoAll(params Func<TPayload, Either<TError, TPayload>>[] activities)
	{
		foreach (var activity in activities)
		{
			_activities.Add(
				new((
						payload,
						_) => Task.FromResult(activity(payload))
				)
			);
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
			new(
				condition,
				new(activity)
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
			new(
				condition,
				new((
						payload,
						_) => Task.FromResult(activity(payload))
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
		return new(this, branch);
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
		return new(branch);
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
		var branchBuilder = new BranchWithLocalPayloadBuilder<TRequest, TPayload, TLocalPayload, TSuccess, TError>(branch);
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
		return new(branch);
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
	/// All groups execute in parallel, and their results are merged back into the main workflow.
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
	/// All groups execute in parallel, and their results are merged back into the main workflow.
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
	/// All detached groups execute in parallel, and their results are NOT merged back.
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
		var parallelDetachedBuilder =
			new Features.Parallel.ParallelDetachedBuilder<TRequest, TPayload, TSuccess, TError>(this, parallelDetached);
		parallelDetachedConfiguration(parallelDetachedBuilder);
		return this;
	}

	/// <summary>
	/// Creates a parallel execution of multiple detached groups that always executes.
	/// All detached groups execute in parallel, and their results are NOT merged back.
	/// </summary>
	/// <param name="parallelDetachedConfiguration">An action that configures the parallel detached execution</param>
	/// <returns>The workflow builder to continue the workflow definition</returns>
	public WorkflowBuilder<TRequest, TPayload, TSuccess, TError> ParallelDetached(
		Action<Features.Parallel.ParallelDetachedBuilder<TRequest, TPayload, TSuccess, TError>> parallelDetachedConfiguration)
	{
		return ParallelDetached(null, parallelDetachedConfiguration);
	}

	/// <summary>
	/// Adds an activity to the "finally" block that will always execute, even if the workflow fails.
	/// </summary>
	/// <param name="activity">The activity to execute</param>
	/// <returns>The builder instance for method chaining</returns>
	public WorkflowBuilder<TRequest, TPayload, TSuccess, TError> Finally(
		Func<TPayload, CancellationToken, Task<Either<TError, TPayload>>> activity)
	{
		_finallyActivities.Add(new(activity));
		return this;
	}

	/// <summary>
	/// Adds a synchronous activity to the "finally" block that will always execute, even if the workflow fails.
	/// </summary>
	/// <param name="activity">The activity to execute</param>
	/// <returns>The builder instance for method chaining</returns>
	public WorkflowBuilder<TRequest, TPayload, TSuccess, TError> Finally(Func<TPayload, Either<TError, TPayload>> activity)
	{
		_finallyActivities.Add(
			new((
					payload,
					_) => Task.FromResult(activity(payload))
			)
		);
		return this;
	}

	/// <summary>
	/// Builds a workflow that processes a request and returns either a success or an error.
	/// </summary>
	public Workflow<TRequest, TSuccess, TError> Build()
	{
		return new(ExecuteWorkflowAsync);
	}

	/// <summary>
	/// Executes the workflow: runs validations, activities, conditional logic, branches, features, and finally actions.
	/// </summary>
	/// <param name="request">The workflow request input.</param>
	/// <param name="cancellationToken">Token to observe for cancellation.</param>
	/// <returns>
	/// An Either containing the error (Left) if any stage fails, or the final success result (Right) on completion.
	/// </returns>
	private async Task<Either<TError, TSuccess>> ExecuteWorkflowAsync(
		TRequest request,
		CancellationToken cancellationToken)
	{
		// We run the validations first to ensure the request is valid before proceeding
		var validationResult = await RunValidationsAsync(request, cancellationToken);
		if (validationResult.IsLeft)
			return Either<TError, TSuccess>.FromLeft(validationResult.Left!);

		var guardResult = await RunGuardsAsync(request, cancellationToken);
		if (guardResult.IsLeft)
			return Either<TError, TSuccess>.FromLeft(guardResult.Left!);

		var payload = _contextFactory(request);
		if (payload == null)
			return Either<TError, TSuccess>.FromRight(_resultSelector(default!));

		try
		{
			var activitiesResult = await RunActivitiesAsync(payload, cancellationToken);
			if (activitiesResult.IsLeft)
				return Either<TError, TSuccess>.FromLeft(activitiesResult.Left!);

			payload = activitiesResult.Right!;

			var conditionalResult = await RunConditionalActivitiesAsync(payload, cancellationToken);
			if (conditionalResult.IsLeft)
				return Either<TError, TSuccess>.FromLeft(conditionalResult.Left!);

			payload = conditionalResult.Right!;

			var branchesResult = await RunBranchesAsync(payload, cancellationToken);
			if (branchesResult.IsLeft)
				return Either<TError, TSuccess>.FromLeft(branchesResult.Left!);

			payload = branchesResult.Right!;

			var branchLocalsResult = await RunBranchesWithLocalPayloadAsync(payload, cancellationToken);
			if (branchLocalsResult.IsLeft)
				return Either<TError, TSuccess>.FromLeft(branchLocalsResult.Left!);

			payload = branchLocalsResult.Right!;

			var featuresResult = await RunFeaturesAsync(payload, cancellationToken);
			if (featuresResult.IsLeft)
				return Either<TError, TSuccess>.FromLeft(featuresResult.Left!);

			payload = featuresResult.Right!;

			var successValue = _resultSelector(payload);
			return Either<TError, TSuccess>.FromRight(successValue ?? default!);
		}
		finally
		{
			_ = await RunFinallyActivitiesAsync(payload, cancellationToken);
		}
	}

	/// <summary>
	/// Runs all configured validations against the request.
	/// </summary>
	/// <param name="request">The workflow request.</param>
	/// <param name="cancellationToken">Token to observe for cancellation.</param>
	/// <returns>
	/// An Either with Left if any validation fails, or Right with a placeholder payload on success.
	/// </returns>
	private async Task<Either<TError, TPayload>> RunValidationsAsync(
		TRequest request,
		CancellationToken cancellationToken)
	{
		foreach (var validation in _validations)
		{
			var validationOption = await validation.Validate(request, cancellationToken);
			if (validationOption.IsSome && validationOption.Value != null)
				return Either<TError, TPayload>.FromLeft(validationOption.Value);
		}

		return Either<TError, TPayload>.FromRight(default!);
	}

	/// <summary>
	/// Runs all configured guards against the request.
	/// </summary>
	/// <param name="request">The workflow request.</param>
	/// <param name="cancellationToken">Token to observe for cancellation.</param>
	/// <returns>
	/// An Either with Left if any guard fails, or Right with Unit on success.
	/// </returns>
	private async Task<Either<TError, Unit>> RunGuardsAsync(
		TRequest request,
		CancellationToken cancellationToken)
	{
		foreach (var guard in _guards)
		{
			var result = await guard.Check(request, cancellationToken);
			if (result.IsLeft && result.Left != null)
				return Either<TError, Unit>.FromLeft(result.Left);
		}

		return Either<TError, Unit>.FromRight(Unit.Value);
	}

	/// <summary>
	/// Executes all registered activities in sequence, returning either the first encountered error or the transformed payload.
	/// </summary>
	/// <param name="payload">The initial payload to process.</param>
	/// <param name="cancellationToken">Token to observe for cancellation.</param>
	/// <returns>
	/// An Either containing the error (Left) if any activity fails, or the final payload (Right) on success.
	/// </returns>
	private async Task<Either<TError, TPayload>> RunActivitiesAsync(
		TPayload payload,
		CancellationToken cancellationToken)
	{
		foreach (var activity in _activities)
		{
			var result = await activity.Execute(payload, cancellationToken);
			if (result.IsLeft && result.Left != null)
				return Either<TError, TPayload>.FromLeft(result.Left);

			payload = result.Right!;
		}

		return Either<TError, TPayload>.FromRight(payload);
	}

	/// <summary>
	/// Executes conditional activities when their condition is met.
	/// </summary>
	/// <param name="payload">The current payload.</param>
	/// <param name="cancellationToken">Token to observe for cancellation.</param>
	/// <returns>
	/// An Either containing the error (Left) if any conditional activity fails, or the updated payload (Right) on success.
	/// </returns>
	private async Task<Either<TError, TPayload>> RunConditionalActivitiesAsync(
		TPayload payload,
		CancellationToken cancellationToken)
	{
		foreach (var conditionalActivity in _conditionalActivities)
		{
			if (!conditionalActivity.ShouldExecute(payload))
				continue;

			var result = await conditionalActivity.Activity.Execute(payload, cancellationToken);
			if (result.IsLeft && result.Left != null)
				return Either<TError, TPayload>.FromLeft(result.Left);

			payload = result.Right!;
		}

		return Either<TError, TPayload>.FromRight(payload);
	}

	/// <summary>
	/// Executes simple branches when their condition is met.
	/// </summary>
	/// <param name="payload">The current payload.</param>
	/// <param name="cancellationToken">Token to observe for cancellation.</param>
	/// <returns>
	/// An Either containing the error (Left) if any branch activity fails, or the updated payload (Right) on success.
	/// </returns>
	private async Task<Either<TError, TPayload>> RunBranchesAsync(
		TPayload payload,
		CancellationToken cancellationToken)
	{
		foreach (var branch in _branches)
		{
			if (!branch.Condition(payload))
				continue;

			foreach (var activity in branch.Activities)
			{
				var result = await activity.Execute(payload, cancellationToken);
				if (result.IsLeft && result.Left != null)
					return Either<TError, TPayload>.FromLeft(result.Left);

				payload = result.Right!;
			}
		}

		return Either<TError, TPayload>.FromRight(payload);
	}

	/// <summary>
	/// Executes branches with the local payload via reflection helper.
	/// </summary>
	/// <param name="payload">The current payload.</param>
	/// <param name="cancellationToken">Token to observe for cancellation.</param>
	/// <returns>
	/// An Either containing the error (Left) if any branch fails, or the updated payload (Right) on success.
	/// </returns>
	private async Task<Either<TError, TPayload>> RunBranchesWithLocalPayloadAsync(
		TPayload payload,
		CancellationToken cancellationToken)
	{
		foreach (var branchObject in _branchesWithLocalPayload)
		{
			var result = await ExecuteBranchWithLocalPayloadDynamic(branchObject, payload, cancellationToken);
			if (result.IsLeft && result.Left != null)
				return Either<TError, TPayload>.FromLeft(result.Left);

			payload = result.Right!;
		}

		return Either<TError, TPayload>.FromRight(payload);
	}

	/// <summary>
	/// Executes workflow features like Group, Context, Detach, Parallel, merging results when applicable.
	/// </summary>
	/// <param name="payload">The current payload.</param>
	/// <param name="cancellationToken">Token to observe for cancellation.</param>
	/// <returns>
	/// An Either containing the error (Left) if any feature fails, or the updated payload (Right) on success.
	/// </returns>
	private async Task<Either<TError, TPayload>> RunFeaturesAsync(
		TPayload payload,
		CancellationToken cancellationToken)
	{
		var factory = new FeatureExecutorFactory<TPayload, TError>();

		foreach (var feature in _features)
		{
			var result = await factory.ExecuteFeature(feature, payload, cancellationToken);
			if (result.IsLeft && result.Left != null)
				return Either<TError, TPayload>.FromLeft(result.Left);

			if (feature.ShouldMerge)
				payload = result.Right!;
		}

		return Either<TError, TPayload>.FromRight(payload);
	}

	/// <summary>
	/// Executes all the "finally" activities, ignoring errors.
	/// </summary>
	/// <param name="payload">The current payload.</param>
	/// <param name="cancellationToken">Token to observe for cancellation.</param>
	/// <returns>The original payload after the "finally" activities.</returns>
	private async Task<TPayload> RunFinallyActivitiesAsync(
		TPayload payload,
		CancellationToken cancellationToken)
	{
		foreach (var finallyActivity in _finallyActivities)
			_ = await finallyActivity.Execute(payload, cancellationToken);

		return payload;
	}

	/// <summary>
	/// Dynamically executes a BranchWithLocalPayload via reflection, invoking the generic helper for the correct local payload type.
	/// </summary>
	/// <param name="branchObject">
	/// The branch instance, expected to be BranchWithLocalPayload&lt;TPayload, TLocalPayload, TError&gt;
	/// </param>
	/// <param name="payload">The main workflow payload.</param>
	/// <param name="cancellationToken">Token to observe for cancellation.</param>
	/// <returns>
	/// An Either containing the error (Left) if execution failed,
	/// or the updated payload (Right) on success.
	/// </returns>
	private async Task<Either<TError, TPayload>> ExecuteBranchWithLocalPayloadDynamic(
		object branchObject,
		TPayload payload,
		CancellationToken cancellationToken)
	{
		var branchType = branchObject.GetType();

		if (branchType.IsGenericType &&
		    branchType.GetGenericTypeDefinition() == typeof(BranchWithLocalPayload<,,>))
		{
			var methodInfo = typeof(WorkflowBuilder<TRequest, TPayload, TSuccess, TError>)
				.GetMethod(nameof(ExecuteBranchWithLocalPayload), BindingFlags.NonPublic | BindingFlags.Instance);
			if (methodInfo == null)
				throw new InvalidOperationException($"Method {nameof(ExecuteBranchWithLocalPayload)} not found.");

			var localPayloadType = branchType.GetGenericArguments()[1];
			var genericMethod = methodInfo.MakeGenericMethod(localPayloadType);
			var task = (Task<Either<TError, TPayload>>)genericMethod.Invoke(
				this,
				[
					branchObject, payload, cancellationToken
				]
			)!;
			return await task.ConfigureAwait(false);
		}

		return Either<TError, TPayload>.FromRight(payload);
	}

	/// <summary>
	/// Executes a BranchWithLocalPayload&lt;TPayload, TLocalPayload, TError&gt; branch.
	/// </summary>
	/// <typeparam name="TLocalPayload">Type of the local payload used by this branch.</typeparam>
	/// <param name="branch">The branch configuration and activities.</param>
	/// <param name="payload">The main workflow payload.</param>
	/// <param name="cancellationToken">Token to observe for cancellation.</param>
	/// <returns>
	/// An Either containing the error (Left) if an activity fails,
	/// or the updated payload (Right) on success.
	/// </returns>
	private async Task<Either<TError, TPayload>> ExecuteBranchWithLocalPayload<TLocalPayload>(
		BranchWithLocalPayload<TPayload, TLocalPayload, TError> branch,
		TPayload payload,
		CancellationToken cancellationToken)
	{
		if (!branch.Condition(payload))
			return Either<TError, TPayload>.FromRight(payload);

		var localPayload = branch.LocalPayloadFactory(payload);

		foreach (var activity in branch.Activities)
		{
			var result = await activity.Execute(payload, localPayload, cancellationToken);
			if (result.IsLeft)
				return Either<TError, TPayload>.FromLeft(result.Left);

			(payload, localPayload) = result.Right;
		}

		return Either<TError, TPayload>.FromRight(payload);
	}
}