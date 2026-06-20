using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Zooper.Bee.Internal;
using Zooper.Fox;

// ReSharper disable MemberCanBePrivate.Global

namespace Zooper.Bee;

/// <summary>
/// Fluent builder for the steps phase of a railway pipeline.
/// Operators are appended in registration order and executed sequentially,
/// each receiving the full <see cref="Zooper.Fox.Either{TLeft,TRight}"/> state produced by the previous step.
/// </summary>
/// <typeparam name="TRequest">The type of the incoming request used to initialise the payload.</typeparam>
/// <typeparam name="TPayload">The mutable working payload that flows through the pipeline.</typeparam>
/// <typeparam name="TSuccess">The type returned when the pipeline completes on the <c>Right</c> rail.</typeparam>
/// <typeparam name="TError">The type returned when the pipeline terminates on the <c>Left</c> rail.</typeparam>
public sealed class RailwayStepsBuilder<TRequest, TPayload, TSuccess, TError>
{
    private readonly Func<TRequest, TPayload> _contextFactory;
    private readonly Func<TPayload, TSuccess> _resultSelector;
    private readonly List<RailwayGuard<TRequest, TError>> _guards;
    private readonly List<RailwayValidation<TRequest, TError>> _validations;
    private readonly List<Func<Either<TError, TPayload>, TPayload, CancellationToken, Task<Either<TError, TPayload>>>> _operators = [];
    private readonly List<Func<TPayload, CancellationToken, Task>> _finallyActivities = [];

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
    /// Adds an asynchronous payload-progression step.
    /// <para>The result <strong>is fed back</strong> into the pipeline — the returned
    /// <see cref="Either{TError,TPayload}"/> becomes the new pipeline state seen by downstream operators.</para>
    /// <para>On <c>Right</c>: executes the delegate; its result becomes the new state.</para>
    /// <para>On <c>Left</c>: skips — the existing error propagates unchanged.</para>
    /// </summary>
    /// <param name="activity">The asynchronous delegate that transforms the payload or returns an error.</param>
    public RailwayStepsBuilder<TRequest, TPayload, TSuccess, TError> Do(
        Func<TPayload, CancellationToken, Task<Either<TError, TPayload>>> activity)
    {
        _operators.Add((current, _, ct) =>
            current.IsRight ? activity(current.Right!, ct) : Task.FromResult(current));
        return this;
    }

    /// <summary>
    /// Adds a synchronous payload-progression step.
    /// <para>The result <strong>is fed back</strong> into the pipeline — the returned
    /// <see cref="Either{TError,TPayload}"/> becomes the new pipeline state seen by downstream operators.</para>
    /// <para>On <c>Right</c>: executes the delegate; its result becomes the new state.</para>
    /// <para>On <c>Left</c>: skips — the existing error propagates unchanged.</para>
    /// </summary>
    /// <param name="activity">The synchronous delegate that transforms the payload or returns an error.</param>
    public RailwayStepsBuilder<TRequest, TPayload, TSuccess, TError> Do(
        Func<TPayload, Either<TError, TPayload>> activity)
    {
        _operators.Add((current, _, _ct) =>
            Task.FromResult(current.IsRight ? activity(current.Right!) : current));
        return this;
    }

    /// <summary>
    /// Adds a business-rule assertion. Fails the pipeline when <paramref name="when"/> returns <c>false</c>.
    /// <para>The result is <strong>not</strong> fed back — the payload is never replaced; only the rail can change.</para>
    /// <para>On <c>Right</c>: if <paramref name="when"/> returns <c>false</c>, transitions to
    /// <c>Left(failWith(payload))</c>; if <c>true</c>, the existing state passes through unchanged.</para>
    /// <para>On <c>Left</c>: skips without evaluating the predicate.</para>
    /// </summary>
    /// <param name="when">Predicate that must return <c>true</c> for the pipeline to continue.</param>
    /// <param name="failWith">Produces the error value when the assertion fails.</param>
    public RailwayStepsBuilder<TRequest, TPayload, TSuccess, TError> Ensure(
        Func<TPayload, bool> when,
        Func<TPayload, TError> failWith)
    {
        _operators.Add((current, _, _ct) =>
        {
            if (!current.IsRight) return Task.FromResult(current);
            var payload = current.Right!;
            return Task.FromResult(!when(payload)
                ? Either<TError, TPayload>.FromLeft(failWith(payload))
                : current);
        });
        return this;
    }

    /// <summary>
    /// Conditionally enters a sub-pipeline when <paramref name="condition"/> returns <c>true</c>.
    /// <para>The result <strong>is fed back</strong> — the sub-pipeline's final
    /// <see cref="Either{TError,TPayload}"/> state replaces the main pipeline state.</para>
    /// <para>On <c>Right</c>, predicate <c>true</c>: runs the sub-pipeline; its result becomes the new state.</para>
    /// <para>On <c>Right</c>, predicate <c>false</c>: no-op, existing state passes through unchanged.</para>
    /// <para>On <c>Left</c>: skips — predicate is not evaluated.</para>
    /// <remarks><c>When</c>, <c>Detach</c>, and <c>Finally</c> are intentionally excluded from the inner
    /// <see cref="BranchBuilder{TPayload,TError}"/> to keep the sub-pipeline scope well-defined.</remarks>
    /// </summary>
    /// <param name="condition">Predicate evaluated against the current payload to decide whether to enter the sub-pipeline.</param>
    /// <param name="configure">Action that configures the sub-pipeline operators.</param>
    public RailwayStepsBuilder<TRequest, TPayload, TSuccess, TError> When(
        Func<TPayload, bool> condition,
        Action<BranchBuilder<TPayload, TError>> configure)
    {
        var branchBuilder = new BranchBuilder<TPayload, TError>();
        configure(branchBuilder);
        var branchOps = branchBuilder.Operators;

        _operators.Add(async (current, _, ct) =>
        {
            if (!current.IsRight) return current;
            if (!condition(current.Right!)) return current;

            var branchCurrent = current;
            var branchLastRight = current.Right!;
            foreach (var op in branchOps)
            {
                if (branchCurrent.IsRight) branchLastRight = branchCurrent.Right!;
                branchCurrent = await op(branchCurrent, branchLastRight, ct);
            }
            return branchCurrent;
        });
        return this;
    }

    /// <summary>
    /// Deprecated alias for <see cref="When(System.Func{TPayload,bool},System.Action{BranchBuilder{TPayload,TError}})"/>.
    /// </summary>
    /// <param name="when">Predicate evaluated against the current payload to decide whether to enter the sub-pipeline.</param>
    /// <param name="branch">Action that configures the sub-pipeline operators.</param>
    [Obsolete("Use When instead. Branch will be removed in the next major version.")]
    public RailwayStepsBuilder<TRequest, TPayload, TSuccess, TError> Branch(
        Func<TPayload, bool> when,
        Action<BranchBuilder<TPayload, TError>> branch)
        => When(when, branch);

    /// <summary>
    /// Adds a strict synchronous pass-through side effect. The payload is never replaced.
    /// <para>The result is <strong>not</strong> fed back — the existing state passes through unchanged.</para>
    /// <para>On <c>Right</c>: executes the effect; exceptions propagate to the caller.</para>
    /// <para>On <c>Left</c>: skips without invoking the delegate.</para>
    /// </summary>
    /// <param name="effect">The synchronous side-effect delegate.</param>
    public RailwayStepsBuilder<TRequest, TPayload, TSuccess, TError> Tap(Action<TPayload> effect)
    {
        _operators.Add((current, _, _ct) =>
        {
            if (!current.IsRight) return Task.FromResult(current);
            effect(current.Right!);
            return Task.FromResult(current);
        });
        return this;
    }

    /// <summary>
    /// Adds a strict asynchronous pass-through side effect. The payload is never replaced.
    /// <para>The result is <strong>not</strong> fed back — the existing state passes through unchanged.</para>
    /// <para>On <c>Right</c>: executes the effect; exceptions propagate to the caller.</para>
    /// <para>On <c>Left</c>: skips without invoking the delegate.</para>
    /// </summary>
    /// <param name="effect">The asynchronous side-effect delegate.</param>
    public RailwayStepsBuilder<TRequest, TPayload, TSuccess, TError> Tap(
        Func<TPayload, CancellationToken, Task> effect)
    {
        _operators.Add(async (current, _, ct) =>
        {
            if (!current.IsRight) return current;
            await effect(current.Right!, ct);
            return current;
        });
        return this;
    }

    /// <summary>
    /// Adds a strict asynchronous pass-through side effect with an explicit error channel.
    /// <para>The payload is never replaced, but the rail can change.</para>
    /// <para>The result is <strong>not</strong> fed back as a new payload — however, a <c>Left</c> return value
    /// switches the pipeline to the error rail.</para>
    /// <para>On <c>Right</c>: executes the effect; returning <c>Left</c> transitions the pipeline to the error rail;
    /// returning <c>Right(Unit)</c> leaves the existing state unchanged.</para>
    /// <para>On <c>Left</c>: skips without invoking the delegate.</para>
    /// </summary>
    /// <param name="effect">The asynchronous side-effect delegate that can signal failure via <c>Left</c>.</param>
    public RailwayStepsBuilder<TRequest, TPayload, TSuccess, TError> Tap(
        Func<TPayload, CancellationToken, Task<Either<TError, Unit>>> effect)
    {
        _operators.Add(async (current, _, ct) =>
        {
            if (!current.IsRight) return current;
            var result = await effect(current.Right!, ct);
            return result.IsLeft ? Either<TError, TPayload>.FromLeft(result.Left!) : current;
        });
        return this;
    }

    /// <summary>
    /// Adds grouped strict pass-through side effects via an inner <see cref="EffectsBuilder{TPayload,TError}"/>.
    /// <para>The result is <strong>not</strong> fed back — inner effects signal success/failure via
    /// <c>Either&lt;TError, Unit&gt;</c> and cannot produce a new payload.</para>
    /// <para>On <c>Right</c>: runs each inner effect in order; the first <c>Left</c> result short-circuits
    /// the group and switches the pipeline to the error rail; on success the existing state passes through unchanged.</para>
    /// <para>On <c>Left</c>: skips the entire group.</para>
    /// </summary>
    /// <param name="configure">Action that registers inner effects on the <see cref="EffectsBuilder{TPayload,TError}"/>.</param>
    public RailwayStepsBuilder<TRequest, TPayload, TSuccess, TError> Effects(
        Action<EffectsBuilder<TPayload, TError>> configure)
    {
        var builder = new EffectsBuilder<TPayload, TError>();
        configure(builder);
        var effects = builder.Effects;

        _operators.Add(async (current, _, ct) =>
        {
            if (!current.IsRight) return current;
            var payload = current.Right!;
            foreach (var effect in effects)
            {
                var result = await effect(payload, ct);
                if (result.IsLeft) return Either<TError, TPayload>.FromLeft(result.Left!);
            }
            return current;
        });
        return this;
    }

    /// <summary>
    /// Adds best-effort grouped side effects. All inner effects are always attempted; failures are swallowed.
    /// <para>The result is <strong>not</strong> fed back — the existing state passes through unchanged regardless
    /// of inner failures.</para>
    /// <para>On <c>Right</c>: runs all inner effects in order; exceptions and <c>Left</c> returns are swallowed.</para>
    /// <para>On <c>Left</c>: skips the entire group.</para>
    /// </summary>
    /// <param name="configure">Action that registers inner effects on the <see cref="EffectsBuilder{TPayload,TError}"/>.</param>
    public RailwayStepsBuilder<TRequest, TPayload, TSuccess, TError> TryEffects(
        Action<EffectsBuilder<TPayload, TError>> configure)
    {
        var builder = new EffectsBuilder<TPayload, TError>();
        configure(builder);
        var effects = builder.Effects;

        _operators.Add(async (current, _, ct) =>
        {
            if (!current.IsRight) return current;
            var payload = current.Right!;
            foreach (var effect in effects)
            {
                try { await effect(payload, ct); } catch { }
            }
            return current;
        });
        return this;
    }

    /// <summary>
    /// Adds a best-effort synchronous pass-through side effect. Exceptions are swallowed.
    /// <para>The result is <strong>not</strong> fed back — the existing state passes through unchanged.</para>
    /// <para>On <c>Right</c>: executes the effect; exceptions are silently swallowed.</para>
    /// <para>On <c>Left</c>: skips without invoking the delegate.</para>
    /// </summary>
    /// <param name="effect">The synchronous side-effect delegate.</param>
    public RailwayStepsBuilder<TRequest, TPayload, TSuccess, TError> TryTap(Action<TPayload> effect)
    {
        _operators.Add((current, _, _ct) =>
        {
            if (!current.IsRight) return Task.FromResult(current);
            try { effect(current.Right!); } catch { }
            return Task.FromResult(current);
        });
        return this;
    }

    /// <summary>
    /// Adds a best-effort asynchronous pass-through side effect. Exceptions are swallowed.
    /// <para>The result is <strong>not</strong> fed back — the existing state passes through unchanged.</para>
    /// <para>On <c>Right</c>: executes the effect; exceptions are silently swallowed.</para>
    /// <para>On <c>Left</c>: skips without invoking the delegate.</para>
    /// </summary>
    /// <param name="effect">The asynchronous side-effect delegate.</param>
    public RailwayStepsBuilder<TRequest, TPayload, TSuccess, TError> TryTap(
        Func<TPayload, CancellationToken, Task> effect)
    {
        _operators.Add(async (current, _, ct) =>
        {
            if (!current.IsRight) return current;
            try { await effect(current.Right!, ct); } catch { }
            return current;
        });
        return this;
    }

    /// <summary>
    /// Fires a group of effects in the background without awaiting their completion.
    /// <para>The result is <strong>discarded entirely</strong> — inner effects run on background threads via
    /// <c>Task.Run</c> and are never awaited. Their success or failure has no effect on the pipeline.</para>
    /// <para>On <c>Right</c>: schedules each inner effect; returns immediately with the existing state unchanged.</para>
    /// <para>On <c>Left</c>: skips — nothing is scheduled.</para>
    /// <para>Exceptions inside detached tasks are always swallowed.</para>
    /// </summary>
    /// <param name="configure">Action that registers inner effects on the <see cref="EffectsBuilder{TPayload,TError}"/>.</param>
    public RailwayStepsBuilder<TRequest, TPayload, TSuccess, TError> Detach(
        Action<EffectsBuilder<TPayload, TError>> configure)
    {
        var builder = new EffectsBuilder<TPayload, TError>();
        configure(builder);
        var effects = builder.Effects;

        _operators.Add((current, _lastRight, ct) =>
        {
            if (!current.IsRight) return Task.FromResult(current);
            var payload = current.Right!;
            foreach (var effect in effects)
            {
                var t = Task.Run(async () =>
                {
                    try { await effect(payload, ct); } catch { }
                });
                _ = t;
            }
            return Task.FromResult(current);
        });
        return this;
    }

    /// <summary>
    /// Adds a typed synchronous recovery operator.
    /// <para>The result <strong>is fed back</strong> — the handler's returned payload replaces the pipeline state,
    /// transitioning from <c>Left</c> back to <c>Right</c>.</para>
    /// <para>On <c>Left</c> where the error is assignable to <typeparamref name="TErr"/>: runs the handler with
    /// <c>(error, lastRightSnapshot)</c>; the returned payload becomes the new <c>Right</c> state.</para>
    /// <para>On <c>Left</c> with a non-matching error type: passes through unchanged.</para>
    /// <para>On <c>Right</c>: skips — the existing state passes through unchanged.</para>
    /// </summary>
    /// <typeparam name="TErr">The specific error type this recovery handles.</typeparam>
    /// <param name="handler">Receives the matched error and the last known <c>Right</c> payload snapshot;
    /// returns the recovered payload.</param>
    public RailwayStepsBuilder<TRequest, TPayload, TSuccess, TError> Recover<TErr>(
        Func<TErr, TPayload, TPayload> handler)
    {
        _operators.Add((current, lastRight, _ct) =>
        {
            if (!current.IsLeft) return Task.FromResult(current);
            if (current.Left is TErr err)
                return Task.FromResult(Either<TError, TPayload>.FromRight(handler(err, lastRight)));
            return Task.FromResult(current);
        });
        return this;
    }

    /// <summary>
    /// Adds a typed asynchronous recovery operator.
    /// <para>The result <strong>is fed back</strong> — the handler's returned payload replaces the pipeline state,
    /// transitioning from <c>Left</c> back to <c>Right</c>.</para>
    /// <para>On <c>Left</c> where the error is assignable to <typeparamref name="TErr"/>: runs the handler with
    /// <c>(error, lastRightSnapshot)</c>; the returned payload becomes the new <c>Right</c> state.</para>
    /// <para>On <c>Left</c> with a non-matching error type: passes through unchanged.</para>
    /// <para>On <c>Right</c>: skips — the existing state passes through unchanged.</para>
    /// </summary>
    /// <typeparam name="TErr">The specific error type this recovery handles.</typeparam>
    /// <param name="handler">Receives the matched error and the last known <c>Right</c> payload snapshot;
    /// returns the recovered payload asynchronously.</param>
    public RailwayStepsBuilder<TRequest, TPayload, TSuccess, TError> Recover<TErr>(
        Func<TErr, TPayload, CancellationToken, Task<TPayload>> handler)
    {
        _operators.Add(async (current, lastRight, ct) =>
        {
            if (!current.IsLeft) return current;
            if (current.Left is TErr err)
                return Either<TError, TPayload>.FromRight(await handler(err, lastRight, ct));
            return current;
        });
        return this;
    }

    /// <summary>
    /// Registers an asynchronous cleanup activity that always executes regardless of pipeline success or failure.
    /// <para>The result is <strong>discarded entirely</strong> — <c>Finally</c> runs outside the pipeline state
    /// machine and cannot affect the final <see cref="Either{TError,TSuccess}"/> result.</para>
    /// <para>Receives the last known <c>Right</c> payload (the pre-failure snapshot if the pipeline failed).</para>
    /// <para>Exceptions are swallowed so that subsequent <c>Finally</c> registrations always get a chance to run.</para>
    /// </summary>
    /// <param name="activity">The asynchronous cleanup delegate.</param>
    public RailwayStepsBuilder<TRequest, TPayload, TSuccess, TError> Finally(
        Func<TPayload, CancellationToken, Task> activity)
    {
        _finallyActivities.Add(activity);
        return this;
    }

    /// <summary>
    /// Registers a synchronous cleanup activity that always executes regardless of pipeline success or failure.
    /// <para>The result is <strong>discarded entirely</strong> — <c>Finally</c> runs outside the pipeline state
    /// machine and cannot affect the final <see cref="Either{TError,TSuccess}"/> result.</para>
    /// <para>Receives the last known <c>Right</c> payload (the pre-failure snapshot if the pipeline failed).</para>
    /// <para>Exceptions are swallowed so that subsequent <c>Finally</c> registrations always get a chance to run.</para>
    /// </summary>
    /// <param name="activity">The synchronous cleanup delegate.</param>
    public RailwayStepsBuilder<TRequest, TPayload, TSuccess, TError> Finally(Action<TPayload> activity)
    {
        _finallyActivities.Add((payload, _) => { activity(payload); return Task.CompletedTask; });
        return this;
    }

    /// <summary>
    /// Executes a bounded iteration on the right rail.
    /// <para>
    /// When the rail is <c>Right(payload)</c>, runs <paramref name="body"/> repeatedly
    /// until <paramref name="until"/> returns <c>true</c> or <paramref name="maxAttempts"/>
    /// iterations have been exhausted without <paramref name="until"/> being satisfied.
    /// </para>
    /// <para><strong>Iteration order per attempt (1-indexed):</strong></para>
    /// <list type="number">
    ///   <item>Run the body. If the body returns <c>Left</c>, the loop exits immediately with that <c>Left</c>.</item>
    ///   <item>Evaluate <paramref name="until"/>. If <c>true</c>, exit with <c>Right(payload)</c>.</item>
    ///   <item>If <c>attempt == maxAttempts</c>, exit with <c>Left(exhausted(payload, attempt))</c>.</item>
    ///   <item>Apply <paramref name="mutate"/> (if provided) to produce the starting payload for the next iteration.</item>
    ///   <item>Advance to the next iteration.</item>
    /// </list>
    /// <para><strong>Left passthrough:</strong> when the rail is <c>Left</c> at the start of <c>Loop</c>,
    /// the <c>Left</c> passes through unchanged; <paramref name="body"/>, <paramref name="until"/>,
    /// <paramref name="mutate"/>, and <paramref name="exhausted"/> are not invoked.</para>
    /// <para><strong>mutate cannot fail the rail:</strong> <paramref name="mutate"/> is a pure payload
    /// transform. Fallible work should be expressed as a <c>Do</c> at the start of the next body.</para>
    /// <para><strong>Recover scoping:</strong> a <c>Recover</c> inside the body is scoped to errors raised
    /// within the same iteration. A <c>Left</c> not caught by a body-level <c>Recover</c> exits the entire loop.</para>
    /// <para><strong>exhausted is the caller's delegate:</strong> no default error is provided because
    /// <typeparamref name="TError"/> is open; the caller chooses what <c>Left</c> represents exhaustion.</para>
    /// </summary>
    /// <param name="body">Configures the inner sub-pipeline that runs on each iteration.</param>
    /// <param name="until">Break condition evaluated after each iteration; receives <c>(payload, attempt)</c>. Loop exits <c>Right</c> when <c>true</c>.</param>
    /// <param name="maxAttempts">Hard upper bound on iterations. Must be <c>&gt;= 1</c>.</param>
    /// <param name="exhausted">Produces the <c>Left</c> value when <paramref name="maxAttempts"/> is reached without <paramref name="until"/> being satisfied.</param>
    /// <param name="mutate">Optional inter-iteration payload transform applied after <paramref name="until"/> is <c>false</c> and before the next iteration. Never runs before the first iteration or after the loop exits.</param>
    /// <exception cref="ArgumentOutOfRangeException">Thrown at registration time when <paramref name="maxAttempts"/> is less than 1.</exception>
    public RailwayStepsBuilder<TRequest, TPayload, TSuccess, TError> Loop(
        Action<LoopBuilder<TPayload, TError>> body,
        Func<TPayload, int, bool> until,
        int maxAttempts,
        Func<TPayload, int, TError> exhausted,
        Func<TPayload, int, TPayload>? mutate = null)
    {
        if (maxAttempts < 1)
            throw new ArgumentOutOfRangeException(nameof(maxAttempts), maxAttempts,
                "maxAttempts must be >= 1.");

        // Lift sync delegates to async so we share one core implementation.
        Func<TPayload, int, CancellationToken, Task<bool>> untilAsync =
            (p, a, _) => Task.FromResult(until(p, a));
        Func<TPayload, int, CancellationToken, Task<TError>> exhaustedAsync =
            (p, a, _) => Task.FromResult(exhausted(p, a));
        Func<TPayload, int, CancellationToken, Task<TPayload>>? mutateAsync =
            mutate == null ? null : (TPayload p, int a, CancellationToken _) => Task.FromResult(mutate(p, a));

        return LoopCore(body, untilAsync, maxAttempts, exhaustedAsync, mutateAsync);
    }

    /// <summary>
    /// Executes a bounded iteration on the right rail using asynchronous hooks.
    /// <para>
    /// When the rail is <c>Right(payload)</c>, runs <paramref name="body"/> repeatedly
    /// until <paramref name="until"/> returns <c>true</c> or <paramref name="maxAttempts"/>
    /// iterations have been exhausted without <paramref name="until"/> being satisfied.
    /// </para>
    /// <para><strong>Iteration order per attempt (1-indexed):</strong></para>
    /// <list type="number">
    ///   <item>Run the body. If the body returns <c>Left</c>, the loop exits immediately with that <c>Left</c>.</item>
    ///   <item>Evaluate <paramref name="until"/>. If <c>true</c>, exit with <c>Right(payload)</c>.</item>
    ///   <item>If <c>attempt == maxAttempts</c>, exit with <c>Left(exhausted(payload, attempt))</c>.</item>
    ///   <item>Apply <paramref name="mutate"/> (if provided) to produce the starting payload for the next iteration.</item>
    ///   <item>Advance to the next iteration.</item>
    /// </list>
    /// <para><strong>Left passthrough:</strong> when the rail is <c>Left</c> at the start of <c>Loop</c>,
    /// the <c>Left</c> passes through unchanged; <paramref name="body"/>, <paramref name="until"/>,
    /// <paramref name="mutate"/>, and <paramref name="exhausted"/> are not invoked.</para>
    /// <para><strong>CancellationToken:</strong> the token is threaded into all async delegates.
    /// <c>OperationCanceledException</c> propagates from the body per existing executor behaviour.</para>
    /// <para><strong>Mixed sync/async:</strong> to combine a sync hook with async ones, pass the async
    /// overload directly and wrap sync values in <c>Task.FromResult</c>.</para>
    /// <para><strong>mutate cannot fail the rail:</strong> <paramref name="mutate"/> is a pure payload
    /// transform. Fallible work should be expressed as a <c>Do</c> at the start of the next body.</para>
    /// <para><strong>Recover scoping:</strong> a <c>Recover</c> inside the body is scoped to errors raised
    /// within the same iteration. A <c>Left</c> not caught by a body-level <c>Recover</c> exits the entire loop.</para>
    /// <para><strong>exhausted is the caller's delegate:</strong> no default error is provided because
    /// <typeparamref name="TError"/> is open; the caller chooses what <c>Left</c> represents exhaustion.</para>
    /// </summary>
    /// <param name="body">Configures the inner sub-pipeline that runs on each iteration.</param>
    /// <param name="until">Async break condition evaluated after each iteration; receives <c>(payload, attempt, ct)</c>. Loop exits <c>Right</c> when <c>true</c>.</param>
    /// <param name="maxAttempts">Hard upper bound on iterations. Must be <c>&gt;= 1</c>.</param>
    /// <param name="exhausted">Async delegate that produces the <c>Left</c> value when <paramref name="maxAttempts"/> is reached without <paramref name="until"/> being satisfied.</param>
    /// <param name="mutate">Optional async inter-iteration payload transform. Never runs before the first iteration or after the loop exits.</param>
    /// <exception cref="ArgumentOutOfRangeException">Thrown at registration time when <paramref name="maxAttempts"/> is less than 1.</exception>
    public RailwayStepsBuilder<TRequest, TPayload, TSuccess, TError> Loop(
        Action<LoopBuilder<TPayload, TError>> body,
        Func<TPayload, int, CancellationToken, Task<bool>> until,
        int maxAttempts,
        Func<TPayload, int, CancellationToken, Task<TError>> exhausted,
        Func<TPayload, int, CancellationToken, Task<TPayload>>? mutate = null)
    {
        if (maxAttempts < 1)
            throw new ArgumentOutOfRangeException(nameof(maxAttempts), maxAttempts,
                "maxAttempts must be >= 1.");

        return LoopCore(body, until, maxAttempts, exhausted, mutate);
    }

    private RailwayStepsBuilder<TRequest, TPayload, TSuccess, TError> LoopCore(
        Action<LoopBuilder<TPayload, TError>> body,
        Func<TPayload, int, CancellationToken, Task<bool>> until,
        int maxAttempts,
        Func<TPayload, int, CancellationToken, Task<TError>> exhausted,
        Func<TPayload, int, CancellationToken, Task<TPayload>>? mutate)
    {
        var loopBuilder = new LoopBuilder<TPayload, TError>();
        body(loopBuilder);
        var loopOps = loopBuilder.Operators;

        _operators.Add(async (current, _, ct) =>
        {
            // Left passthrough: do not invoke any loop hook.
            if (!current.IsRight) return current;

            var state = current.Right!;

            for (var attempt = 1; ; attempt++)
            {
                // 1. Run the body on a fresh Right(state) for this iteration.
                var bodyCurrent = Either<TError, TPayload>.FromRight(state);
                var bodyLastRight = state;
                foreach (var op in loopOps)
                {
                    if (bodyCurrent.IsRight) bodyLastRight = bodyCurrent.Right!;
                    bodyCurrent = await op(bodyCurrent, bodyLastRight, ct);
                }

                // 2. Body short-circuit: exit immediately with the Left.
                if (!bodyCurrent.IsRight) return bodyCurrent;

                state = bodyCurrent.Right!;

                // 3. Check break condition.
                if (await until(state, attempt, ct))
                    return Either<TError, TPayload>.FromRight(state);

                // 4. Check exhaustion (mutate does NOT run after the final iteration).
                if (attempt == maxAttempts)
                    return Either<TError, TPayload>.FromLeft(await exhausted(state, attempt, ct));

                // 5. Inter-iteration mutation.
                if (mutate != null)
                    state = await mutate(state, attempt, ct);
            }
        });
        return this;
    }

    /// <summary>
    /// Builds and returns the configured <see cref="Railway{TRequest,TSuccess,TError}"/> ready for execution.
    /// </summary>
    public Railway<TRequest, TSuccess, TError> Build() => new(ExecuteRailwayAsync);

    private async Task<Either<TError, TSuccess>> ExecuteRailwayAsync(
        TRequest request,
        CancellationToken cancellationToken)
    {
        var validationResult = await RunValidationsAsync(request, cancellationToken);
        if (validationResult.IsLeft) return Either<TError, TSuccess>.FromLeft(validationResult.Left!);

        var guardResult = await RunGuardsAsync(request, cancellationToken);
        if (guardResult.IsLeft) return Either<TError, TSuccess>.FromLeft(guardResult.Left!);

        var payload = _contextFactory(request);
        if (payload == null) return Either<TError, TSuccess>.FromRight(_resultSelector(default!));

        var lastRight = payload;
        try
        {
            var (result, finalLastRight) = await RunOperatorsAsync(payload, cancellationToken);
            lastRight = finalLastRight;
            if (result.IsLeft) return Either<TError, TSuccess>.FromLeft(result.Left!);
            return Either<TError, TSuccess>.FromRight(_resultSelector(result.Right!) ?? default!);
        }
        finally
        {
            await RunFinallyActivitiesAsync(lastRight, cancellationToken);
        }
    }

    private async Task<(Either<TError, TPayload> result, TPayload lastRight)> RunOperatorsAsync(
        TPayload initialPayload,
        CancellationToken cancellationToken)
    {
        var current = Either<TError, TPayload>.FromRight(initialPayload);
        var lastRight = initialPayload;

        foreach (var op in _operators)
        {
            if (current.IsRight) lastRight = current.Right!;
            current = await op(current, lastRight, cancellationToken);
        }

        if (current.IsRight) lastRight = current.Right!;
        return (current, lastRight);
    }

    private async Task<Either<TError, TPayload>> RunValidationsAsync(
        TRequest request, CancellationToken cancellationToken)
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
        TRequest request, CancellationToken cancellationToken)
    {
        foreach (var guard in _guards)
        {
            // Conditional guards run only when their predicate holds; otherwise skip-as-pass.
            if (guard.When != null && !await guard.When(request, cancellationToken)) continue;

            var result = await guard.Check(request, cancellationToken);
            if (result.IsLeft && result.Left != null) return Either<TError, Unit>.FromLeft(result.Left);
        }
        return Either<TError, Unit>.FromRight(Unit.Value);
    }

    private async Task RunFinallyActivitiesAsync(
        TPayload lastPayload, CancellationToken cancellationToken)
    {
        foreach (var activity in _finallyActivities)
        {
            try { await activity(lastPayload, cancellationToken); }
            catch { /* swallow to allow subsequent Finally activities to run */ }
        }
    }
}
