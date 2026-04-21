using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Zooper.Fox;

// ReSharper disable MemberCanBePrivate.Global

namespace Zooper.Bee;

/// <summary>
/// Builder for grouped side effects used by
/// <see cref="RailwayStepsBuilder{TRequest,TPayload,TSuccess,TError}.Effects"/> and
/// <see cref="RailwayStepsBuilder{TRequest,TPayload,TSuccess,TError}.TryEffects"/> and
/// <see cref="RailwayStepsBuilder{TRequest,TPayload,TSuccess,TError}.Detach"/>.
/// Exposes only <c>Do</c> overloads. Each inner delegate is a side-effect that does
/// not return a new payload.
/// </summary>
/// <typeparam name="TPayload">The type of the payload.</typeparam>
/// <typeparam name="TError">The type of the error.</typeparam>
public sealed class EffectsBuilder<TPayload, TError>
{
    internal List<Func<TPayload, CancellationToken, Task<Either<TError, Unit>>>> Effects { get; } = [];

    /// <summary>
    /// Adds a synchronous side-effect to the group.
    /// Exceptions thrown by <paramref name="effect"/> propagate to the caller when used inside
    /// <c>Effects</c> (strict) or are swallowed when used inside <c>TryEffects</c> or <c>Detach</c> (best-effort).
    /// </summary>
    /// <param name="effect">The synchronous side-effect delegate.</param>
    /// <returns>The builder instance for method chaining.</returns>
    public EffectsBuilder<TPayload, TError> Do(Action<TPayload> effect)
    {
        Effects.Add((payload, _) =>
        {
            effect(payload);
            return Task.FromResult(Either<TError, Unit>.FromRight(Unit.Value));
        });
        return this;
    }

    /// <summary>
    /// Adds an asynchronous side-effect to the group with no error channel.
    /// Exceptions thrown by <paramref name="effect"/> propagate to the caller when used inside
    /// <c>Effects</c> (strict) or are swallowed when used inside <c>TryEffects</c> or <c>Detach</c> (best-effort).
    /// </summary>
    /// <param name="effect">The asynchronous side-effect delegate.</param>
    /// <returns>The builder instance for method chaining.</returns>
    public EffectsBuilder<TPayload, TError> Do(Func<TPayload, CancellationToken, Task> effect)
    {
        Effects.Add(async (payload, ct) =>
        {
            await effect(payload, ct);
            return Either<TError, Unit>.FromRight(Unit.Value);
        });
        return this;
    }

    /// <summary>
    /// Adds an asynchronous side-effect to the group with an explicit error channel.
    /// Returning <c>Left(err)</c> fails the group according to the enclosing operator's policy:
    /// the failure is propagated by <c>Effects</c> and swallowed by <c>TryEffects</c> or <c>Detach</c>.
    /// </summary>
    /// <param name="effect">The asynchronous side-effect delegate with an error channel.</param>
    /// <returns>The builder instance for method chaining.</returns>
    public EffectsBuilder<TPayload, TError> Do(Func<TPayload, CancellationToken, Task<Either<TError, Unit>>> effect)
    {
        Effects.Add(effect);
        return this;
    }
}
