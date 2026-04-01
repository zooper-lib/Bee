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
/// Builds the step execution phase of a railway.
/// Obtained via <see cref="Railway.Create{TRequest,TPayload,TSuccess,TError}(System.Func{TRequest,TPayload},System.Func{TPayload,TSuccess},System.Action{RailwayGuardBuilder{TRequest,TPayload,TSuccess,TError}},System.Action{RailwayStepsBuilder{TRequest,TPayload,TSuccess,TError}})"/>.
/// All steps are executed in registration order after the guard phase completes.
/// </summary>
/// <typeparam name="TRequest">The type of the request input.</typeparam>
/// <typeparam name="TPayload">The type of the payload used to carry intermediate data.</typeparam>
/// <typeparam name="TSuccess">The type of the success result.</typeparam>
/// <typeparam name="TError">The type of the error result.</typeparam>
public sealed class RailwayStepsBuilder<TRequest, TPayload, TSuccess, TError>
{
    private readonly Func<TRequest, TPayload> _contextFactory;
    private readonly Func<TPayload, TSuccess> _resultSelector;
    private readonly List<RailwayGuard<TRequest, TError>> _guards;
    private readonly List<RailwayValidation<TRequest, TError>> _validations;

    private readonly List<Func<TPayload, CancellationToken, Task<Either<TError, TPayload>>>> _steps = [];
    private readonly FeatureExecutorFactory<TPayload, TError> _featureExecutorFactory = new();
    private readonly List<RailwayStep<TPayload, TError>> _finallyActivities = [];
    private readonly List<Branch<TPayload, TError>> _branches = [];
    private readonly List<object> _branchesWithLocalPayload = [];

    internal RailwayStepsBuilder(
        Func<TRequest, TPayload> contextFactory,
        Func<TPayload, TSuccess> resultSelector,
        List<RailwayGuard<TRequest, TError>> guards,
        List<RailwayValidation<TRequest, TError>> validations)
    {
        _contextFactory = contextFactory ?? throw new ArgumentNullException(nameof(contextFactory));
        _resultSelector = resultSelector ?? throw new ArgumentNullException(nameof(resultSelector));
        _guards = guards;
        _validations = validations;
    }

    /// <summary>
    /// Adds an activity to the railway.
    /// </summary>
    /// <param name="activity">The activity function</param>
    /// <returns>The builder instance for method chaining</returns>
    public RailwayStepsBuilder<TRequest, TPayload, TSuccess, TError> Do(
        Func<TPayload, CancellationToken, Task<Either<TError, TPayload>>> activity)
    {
        _steps.Add(activity);
        return this;
    }

    /// <summary>
    /// Adds a synchronous activity to the railway.
    /// </summary>
    /// <param name="activity">The activity function</param>
    /// <returns>The builder instance for method chaining</returns>
    public RailwayStepsBuilder<TRequest, TPayload, TSuccess, TError> Do(Func<TPayload, Either<TError, TPayload>> activity)
    {
        _steps.Add((payload, _) => Task.FromResult(activity(payload)));
        return this;
    }

    /// <summary>
    /// Adds multiple activities to the railway.
    /// </summary>
    /// <param name="activities">The activity functions</param>
    /// <returns>The builder instance for method chaining</returns>
    public RailwayStepsBuilder<TRequest, TPayload, TSuccess, TError> DoAll(
        params Func<TPayload, CancellationToken, Task<Either<TError, TPayload>>>[] activities)
    {
        foreach (var activity in activities)
        {
            _steps.Add(activity);
        }

        return this;
    }

    /// <summary>
    /// Adds multiple synchronous activities to the railway.
    /// </summary>
    /// <param name="activities">The activity functions</param>
    /// <returns>The builder instance for method chaining</returns>
    public RailwayStepsBuilder<TRequest, TPayload, TSuccess, TError> DoAll(
        params Func<TPayload, Either<TError, TPayload>>[] activities)
    {
        foreach (var activity in activities)
        {
            _steps.Add((payload, _) => Task.FromResult(activity(payload)));
        }

        return this;
    }

    /// <summary>
    /// Adds a conditional activity to the railway that will only execute if the condition returns true.
    /// </summary>
    /// <param name="condition">The condition to evaluate</param>
    /// <param name="activity">The activity to execute if the condition is true</param>
    /// <returns>The builder instance for method chaining</returns>
    public RailwayStepsBuilder<TRequest, TPayload, TSuccess, TError> DoIf(
        Func<TPayload, bool> condition,
        Func<TPayload, CancellationToken, Task<Either<TError, TPayload>>> activity)
    {
        _steps.Add((payload, ct) =>
            condition(payload)
                ? activity(payload, ct)
                : Task.FromResult(Either<TError, TPayload>.FromRight(payload)));
        return this;
    }

    /// <summary>
    /// Adds a synchronous conditional activity to the railway that will only execute if the condition returns true.
    /// </summary>
    /// <param name="condition">The condition to evaluate</param>
    /// <param name="activity">The activity to execute if the condition is true</param>
    /// <returns>The builder instance for method chaining</returns>
    public RailwayStepsBuilder<TRequest, TPayload, TSuccess, TError> DoIf(
        Func<TPayload, bool> condition,
        Func<TPayload, Either<TError, TPayload>> activity)
    {
        _steps.Add((payload, _) =>
            Task.FromResult(condition(payload)
                ? activity(payload)
                : Either<TError, TPayload>.FromRight(payload)));
        return this;
    }

    /// <summary>
    /// Creates a group of activities in the railway with an optional condition.
    /// </summary>
    /// <param name="condition">The condition to evaluate. If null, the group always executes.</param>
    /// <param name="groupConfiguration">An action that configures the group</param>
    /// <returns>The builder instance for method chaining</returns>
    public RailwayStepsBuilder<TRequest, TPayload, TSuccess, TError> Group(
        Func<TPayload, bool>? condition,
        Action<Features.Group.GroupBuilder<TRequest, TPayload, TSuccess, TError>> groupConfiguration)
    {
        var group = new Features.Group.Group<TPayload, TError>(condition);
        var groupBuilder = new Features.Group.GroupBuilder<TRequest, TPayload, TSuccess, TError>(group);
        groupConfiguration(groupBuilder);
        _steps.Add((payload, ct) => ExecuteFeatureStepAsync(group, payload, ct));
        return this;
    }

    /// <summary>
    /// Creates a group of activities in the railway that always executes.
    /// </summary>
    /// <param name="groupConfiguration">An action that configures the group</param>
    /// <returns>The builder instance for method chaining</returns>
    public RailwayStepsBuilder<TRequest, TPayload, TSuccess, TError> Group(
        Action<Features.Group.GroupBuilder<TRequest, TPayload, TSuccess, TError>> groupConfiguration)
    {
        return Group(null, groupConfiguration);
    }

    /// <summary>
    /// Creates a context with local state in the railway and an optional condition.
    /// </summary>
    /// <typeparam name="TLocalState">The type of the local context state</typeparam>
    /// <param name="condition">The condition to evaluate. If null, the context always executes.</param>
    /// <param name="localStateFactory">The factory function that creates the local state</param>
    /// <param name="contextConfiguration">An action that configures the context</param>
    /// <returns>The builder instance for method chaining</returns>
    public RailwayStepsBuilder<TRequest, TPayload, TSuccess, TError> WithContext<TLocalState>(
        Func<TPayload, bool>? condition,
        Func<TPayload, TLocalState> localStateFactory,
        Action<Features.Context.ContextBuilder<TRequest, TPayload, TLocalState, TSuccess, TError>> contextConfiguration)
    {
        var context = new Features.Context.Context<TPayload, TLocalState, TError>(condition, localStateFactory);
        var contextBuilder = new Features.Context.ContextBuilder<TRequest, TPayload, TLocalState, TSuccess, TError>(context);
        contextConfiguration(contextBuilder);
        _steps.Add((payload, ct) => ExecuteFeatureStepAsync(context, payload, ct));
        return this;
    }

    /// <summary>
    /// Creates a context with local state in the railway that always executes.
    /// </summary>
    /// <typeparam name="TLocalState">The type of the local context state</typeparam>
    /// <param name="localStateFactory">The factory function that creates the local state</param>
    /// <param name="contextConfiguration">An action that configures the context</param>
    /// <returns>The builder instance for method chaining</returns>
    public RailwayStepsBuilder<TRequest, TPayload, TSuccess, TError> WithContext<TLocalState>(
        Func<TPayload, TLocalState> localStateFactory,
        Action<Features.Context.ContextBuilder<TRequest, TPayload, TLocalState, TSuccess, TError>> contextConfiguration)
    {
        return WithContext(null, localStateFactory, contextConfiguration);
    }

    /// <summary>
    /// Creates a detached group of activities in the railway with an optional condition.
    /// Detached groups don't merge their results back into the main railway.
    /// </summary>
    /// <param name="condition">The condition to evaluate. If null, the detached group always executes.</param>
    /// <param name="detachedConfiguration">An action that configures the detached group</param>
    /// <returns>The builder instance for method chaining</returns>
    public RailwayStepsBuilder<TRequest, TPayload, TSuccess, TError> Detach(
        Func<TPayload, bool>? condition,
        Action<Features.Detached.DetachedBuilder<TRequest, TPayload, TSuccess, TError>> detachedConfiguration)
    {
        var detached = new Features.Detached.Detached<TPayload, TError>(condition);
        var detachedBuilder = new Features.Detached.DetachedBuilder<TRequest, TPayload, TSuccess, TError>(detached);
        detachedConfiguration(detachedBuilder);
        _steps.Add((payload, ct) => ExecuteFeatureStepAsync(detached, payload, ct));
        return this;
    }

    /// <summary>
    /// Creates a detached group of activities in the railway that always executes.
    /// Detached groups don't merge their results back into the main railway.
    /// </summary>
    /// <param name="detachedConfiguration">An action that configures the detached group</param>
    /// <returns>The builder instance for method chaining</returns>
    public RailwayStepsBuilder<TRequest, TPayload, TSuccess, TError> Detach(
        Action<Features.Detached.DetachedBuilder<TRequest, TPayload, TSuccess, TError>> detachedConfiguration)
    {
        return Detach(null, detachedConfiguration);
    }

    /// <summary>
    /// Creates a parallel execution of multiple groups with an optional condition.
    /// All groups execute in parallel, and their results are merged back into the main railway.
    /// </summary>
    /// <param name="condition">The condition to evaluate. If null, the parallel execution always occurs.</param>
    /// <param name="parallelConfiguration">An action that configures the parallel execution</param>
    /// <returns>The builder instance for method chaining</returns>
    public RailwayStepsBuilder<TRequest, TPayload, TSuccess, TError> Parallel(
        Func<TPayload, bool>? condition,
        Action<Features.Parallel.ParallelBuilder<TRequest, TPayload, TSuccess, TError>> parallelConfiguration)
    {
        var parallel = new Features.Parallel.Parallel<TPayload, TError>(condition);
        var parallelBuilder = new Features.Parallel.ParallelBuilder<TRequest, TPayload, TSuccess, TError>(parallel);
        parallelConfiguration(parallelBuilder);
        _steps.Add((payload, ct) => ExecuteFeatureStepAsync(parallel, payload, ct));
        return this;
    }

    /// <summary>
    /// Creates a parallel execution of multiple groups that always executes.
    /// All groups execute in parallel, and their results are merged back into the main railway.
    /// </summary>
    /// <param name="parallelConfiguration">An action that configures the parallel execution</param>
    /// <returns>The builder instance for method chaining</returns>
    public RailwayStepsBuilder<TRequest, TPayload, TSuccess, TError> Parallel(
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
    /// <returns>The builder instance for method chaining</returns>
    public RailwayStepsBuilder<TRequest, TPayload, TSuccess, TError> ParallelDetached(
        Func<TPayload, bool>? condition,
        Action<Features.Parallel.ParallelDetachedBuilder<TRequest, TPayload, TSuccess, TError>> parallelDetachedConfiguration)
    {
        var parallelDetached = new Features.Parallel.ParallelDetached<TPayload, TError>(condition);
        var parallelDetachedBuilder =
            new Features.Parallel.ParallelDetachedBuilder<TRequest, TPayload, TSuccess, TError>(parallelDetached);
        parallelDetachedConfiguration(parallelDetachedBuilder);
        _steps.Add((payload, ct) => ExecuteFeatureStepAsync(parallelDetached, payload, ct));
        return this;
    }

    /// <summary>
    /// Creates a parallel execution of multiple detached groups that always executes.
    /// All detached groups execute in parallel, and their results are NOT merged back.
    /// </summary>
    /// <param name="parallelDetachedConfiguration">An action that configures the parallel detached execution</param>
    /// <returns>The builder instance for method chaining</returns>
    public RailwayStepsBuilder<TRequest, TPayload, TSuccess, TError> ParallelDetached(
        Action<Features.Parallel.ParallelDetachedBuilder<TRequest, TPayload, TSuccess, TError>> parallelDetachedConfiguration)
    {
        return ParallelDetached(null, parallelDetachedConfiguration);
    }

    /// <summary>
    /// Adds an activity to the "finally" block that will always execute, even if the railway fails.
    /// </summary>
    /// <param name="activity">The activity to execute</param>
    /// <returns>The builder instance for method chaining</returns>
    public RailwayStepsBuilder<TRequest, TPayload, TSuccess, TError> Finally(
        Func<TPayload, CancellationToken, Task<Either<TError, TPayload>>> activity)
    {
        _finallyActivities.Add(new(activity));
        return this;
    }

    /// <summary>
    /// Adds a synchronous activity to the "finally" block that will always execute, even if the railway fails.
    /// </summary>
    /// <param name="activity">The activity to execute</param>
    /// <returns>The builder instance for method chaining</returns>
    public RailwayStepsBuilder<TRequest, TPayload, TSuccess, TError> Finally(
        Func<TPayload, Either<TError, TPayload>> activity)
    {
        _finallyActivities.Add(new((payload, _) => Task.FromResult(activity(payload))));
        return this;
    }

    /// <summary>
    /// Builds a railway that processes a request and returns either a success or an error.
    /// </summary>
    public Railway<TRequest, TSuccess, TError> Build()
    {
        return new(ExecuteRailwayAsync);
    }

    private async Task<Either<TError, TSuccess>> ExecuteRailwayAsync(
        TRequest request,
        CancellationToken cancellationToken)
    {
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
            var stepsResult = await RunStepsAsync(payload, cancellationToken);
            if (stepsResult.IsLeft)
                return Either<TError, TSuccess>.FromLeft(stepsResult.Left!);

            payload = stepsResult.Right!;

            var branchesResult = await RunBranchesAsync(payload, cancellationToken);
            if (branchesResult.IsLeft)
                return Either<TError, TSuccess>.FromLeft(branchesResult.Left!);

            payload = branchesResult.Right!;

            var branchLocalsResult = await RunBranchesWithLocalPayloadAsync(payload, cancellationToken);
            if (branchLocalsResult.IsLeft)
                return Either<TError, TSuccess>.FromLeft(branchLocalsResult.Left!);

            payload = branchLocalsResult.Right!;

            var successValue = _resultSelector(payload);
            return Either<TError, TSuccess>.FromRight(successValue ?? default!);
        }
        finally
        {
            _ = await RunFinallyActivitiesAsync(payload, cancellationToken);
        }
    }

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

    private async Task<Either<TError, TPayload>> RunStepsAsync(
        TPayload payload,
        CancellationToken cancellationToken)
    {
        foreach (var step in _steps)
        {
            var result = await step(payload, cancellationToken);
            if (result.IsLeft && result.Left != null)
                return Either<TError, TPayload>.FromLeft(result.Left);

            payload = result.Right!;
        }

        return Either<TError, TPayload>.FromRight(payload);
    }

    private async Task<Either<TError, TPayload>> ExecuteFeatureStepAsync(
        IRailwayFeature<TPayload, TError> feature,
        TPayload payload,
        CancellationToken cancellationToken)
    {
        var result = await _featureExecutorFactory.ExecuteFeature(feature, payload, cancellationToken);
        if (result.IsLeft && result.Left != null)
            return Either<TError, TPayload>.FromLeft(result.Left);

        return feature.ShouldMerge
            ? Either<TError, TPayload>.FromRight(result.Right!)
            : Either<TError, TPayload>.FromRight(payload);
    }

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

    private async Task<TPayload> RunFinallyActivitiesAsync(
        TPayload payload,
        CancellationToken cancellationToken)
    {
        foreach (var finallyActivity in _finallyActivities)
            _ = await finallyActivity.Execute(payload, cancellationToken);

        return payload;
    }

    private async Task<Either<TError, TPayload>> ExecuteBranchWithLocalPayloadDynamic(
        object branchObject,
        TPayload payload,
        CancellationToken cancellationToken)
    {
        var branchType = branchObject.GetType();

        if (branchType.IsGenericType &&
            branchType.GetGenericTypeDefinition() == typeof(BranchWithLocalPayload<,,>))
        {
            var methodInfo = typeof(RailwayStepsBuilder<TRequest, TPayload, TSuccess, TError>)
                .GetMethod(nameof(ExecuteBranchWithLocalPayload), BindingFlags.NonPublic | BindingFlags.Instance);
            if (methodInfo == null)
                throw new InvalidOperationException($"Method {nameof(ExecuteBranchWithLocalPayload)} not found.");

            var localPayloadType = branchType.GetGenericArguments()[1];
            var genericMethod = methodInfo.MakeGenericMethod(localPayloadType);
            var task = (Task<Either<TError, TPayload>>)genericMethod.Invoke(
                this,
                [branchObject, payload, cancellationToken]
            )!;
            return await task.ConfigureAwait(false);
        }

        return Either<TError, TPayload>.FromRight(payload);
    }

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
