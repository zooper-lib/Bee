using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Zooper.Bee.Internal;
using Zooper.Fox;

// ReSharper disable MemberCanBePrivate.Global

namespace Zooper.Bee;

/// <summary>
/// Builds the guarding phase of a railway. This is the second of three execution phases:
/// Validation (see <see cref="RailwayValidationBuilder{TRequest,TPayload,TSuccess,TError}"/>),
/// then Guarding, then Steps (see <see cref="RailwayStepsBuilder{TRequest,TPayload,TSuccess,TError}"/>).
/// Guards registered here always execute after every validation and before any step.
/// To register validations, use <see cref="RailwayValidationBuilder{TRequest,TPayload,TSuccess,TError}"/>.
/// </summary>
/// <typeparam name="TRequest">The type of the request input.</typeparam>
/// <typeparam name="TPayload">The type of the payload used to carry intermediate data.</typeparam>
/// <typeparam name="TSuccess">The type of the success result.</typeparam>
/// <typeparam name="TError">The type of the error result.</typeparam>
public sealed class RailwayGuardBuilder<TRequest, TPayload, TSuccess, TError>
{
    internal List<RailwayGuard<TRequest, TError>> Guards { get; } = [];

    internal RailwayGuardBuilder() { }

    /// <summary>
    /// Adds a guard to check if the railway can be executed.
    /// If a guard fails, the railway will not execute and will return the error.
    /// </summary>
    /// <param name="guard">The guard function that returns Either an error or Unit</param>
    /// <returns>The builder instance for method chaining</returns>
    public RailwayGuardBuilder<TRequest, TPayload, TSuccess, TError> Guard(
        Func<TRequest, CancellationToken, Task<Either<TError, Unit>>> guard)
    {
        Guards.Add(new(guard));
        return this;
    }

    /// <summary>
    /// Adds a synchronous guard to check if the railway can be executed.
    /// If a guard fails, the railway will not execute and will return the error.
    /// </summary>
    /// <param name="guard">The guard function that returns Either an error or Unit</param>
    /// <returns>The builder instance for method chaining</returns>
    public RailwayGuardBuilder<TRequest, TPayload, TSuccess, TError> Guard(
        Func<TRequest, Either<TError, Unit>> guard)
    {
        Guards.Add(new((request, _) => Task.FromResult(guard(request))));
        return this;
    }

    /// <summary>
    /// Registers a group of guards that only run when <paramref name="condition"/> returns <c>true</c>.
    /// When the condition is <c>false</c>, every guard in the group is skipped and treated as a pass.
    /// </summary>
    /// <param name="condition">Synchronous predicate over the request that gates the group.</param>
    /// <param name="configure">Configures the guards belonging to the conditional group.</param>
    /// <returns>The builder instance for method chaining</returns>
    public RailwayGuardBuilder<TRequest, TPayload, TSuccess, TError> When(
        Func<TRequest, bool> condition,
        Action<RailwayGuardBuilder<TRequest, TPayload, TSuccess, TError>> configure)
        => When((request, _) => Task.FromResult(condition(request)), configure);

    /// <summary>
    /// Registers a group of guards that only run when <paramref name="condition"/> resolves to <c>true</c>.
    /// When the condition is <c>false</c>, every guard in the group is skipped and treated as a pass.
    /// </summary>
    /// <param name="condition">Asynchronous predicate over the request that gates the group.</param>
    /// <param name="configure">Configures the guards belonging to the conditional group.</param>
    /// <returns>The builder instance for method chaining</returns>
    public RailwayGuardBuilder<TRequest, TPayload, TSuccess, TError> When(
        Func<TRequest, CancellationToken, Task<bool>> condition,
        Action<RailwayGuardBuilder<TRequest, TPayload, TSuccess, TError>> configure)
    {
        var nested = new RailwayGuardBuilder<TRequest, TPayload, TSuccess, TError>();
        configure(nested);

        foreach (var guard in nested.Guards)
        {
            // Flatten the nested guard into this builder, gated by the group condition.
            // A nested group's own condition is AND-composed with this one (short-circuiting).
            var effective = guard.When is null
                ? condition
                : Compose(condition, guard.When);

            Guards.Add(new RailwayGuard<TRequest, TError>(guard.Check, effective));
        }

        return this;
    }

    private static Func<TRequest, CancellationToken, Task<bool>> Compose(
        Func<TRequest, CancellationToken, Task<bool>> outer,
        Func<TRequest, CancellationToken, Task<bool>> inner)
        => async (request, token) =>
            await outer(request, token).ConfigureAwait(false)
            && await inner(request, token).ConfigureAwait(false);
}
