using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Zooper.Bee.Internal;
using Zooper.Fox;

// ReSharper disable MemberCanBePrivate.Global

namespace Zooper.Bee;

/// <summary>
/// Builds the guard and validation phase of a railway.
/// Obtained via <see cref="Railway.Create{TRequest,TPayload,TSuccess,TError}(System.Func{TRequest,TPayload},System.Func{TPayload,TSuccess},System.Action{RailwayGuardBuilder{TRequest,TPayload,TSuccess,TError}},System.Action{RailwayStepsBuilder{TRequest,TPayload,TSuccess,TError}})"/>.
/// Guards and validations registered here always execute before any step registered on
/// <see cref="RailwayStepsBuilder{TRequest,TPayload,TSuccess,TError}"/>.
/// </summary>
/// <typeparam name="TRequest">The type of the request input.</typeparam>
/// <typeparam name="TPayload">The type of the payload used to carry intermediate data.</typeparam>
/// <typeparam name="TSuccess">The type of the success result.</typeparam>
/// <typeparam name="TError">The type of the error result.</typeparam>
public sealed class RailwayGuardBuilder<TRequest, TPayload, TSuccess, TError>
{
    internal List<RailwayGuard<TRequest, TError>> Guards { get; } = [];
    internal List<RailwayValidation<TRequest, TError>> Validations { get; } = [];

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
    /// Adds a validation rule to the railway.
    /// </summary>
    /// <param name="validation">The validation function</param>
    /// <returns>The builder instance for method chaining</returns>
    public RailwayGuardBuilder<TRequest, TPayload, TSuccess, TError> Validate(
        Func<TRequest, CancellationToken, Task<Option<TError>>> validation)
    {
        Validations.Add(new(validation));
        return this;
    }

    /// <summary>
    /// Adds a synchronous validation rule to the railway.
    /// </summary>
    /// <param name="validation">The validation function</param>
    /// <returns>The builder instance for method chaining</returns>
    public RailwayGuardBuilder<TRequest, TPayload, TSuccess, TError> Validate(
        Func<TRequest, Option<TError>> validation)
    {
        Validations.Add(new((request, _) => Task.FromResult(validation(request))));
        return this;
    }
}
