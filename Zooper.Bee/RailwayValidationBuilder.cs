using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Zooper.Bee.Internal;
using Zooper.Fox;

// ReSharper disable MemberCanBePrivate.Global

namespace Zooper.Bee;

/// <summary>
/// Builds the validation phase of a railway. This is the first of three execution phases:
/// Validation, then Guarding (see <see cref="RailwayGuardBuilder{TRequest,TPayload,TSuccess,TError}"/>),
/// then Steps (see <see cref="RailwayStepsBuilder{TRequest,TPayload,TSuccess,TError}"/>).
/// Validations registered here always execute before any guard and before any step.
/// </summary>
/// <typeparam name="TRequest">The type of the request input.</typeparam>
/// <typeparam name="TPayload">The type of the payload used to carry intermediate data.</typeparam>
/// <typeparam name="TSuccess">The type of the success result.</typeparam>
/// <typeparam name="TError">The type of the error result.</typeparam>
public sealed class RailwayValidationBuilder<TRequest, TPayload, TSuccess, TError>
{
    internal List<RailwayValidation<TRequest, TError>> Validations { get; } = [];

    internal RailwayValidationBuilder() { }

    /// <summary>
    /// Adds a validation rule to the railway.
    /// If a validation fails, the railway will not execute and will return the error.
    /// </summary>
    /// <param name="validation">The validation function that returns an optional error</param>
    /// <returns>The builder instance for method chaining</returns>
    public RailwayValidationBuilder<TRequest, TPayload, TSuccess, TError> Validate(
        Func<TRequest, CancellationToken, Task<Option<TError>>> validation)
    {
        Validations.Add(new(validation));
        return this;
    }

    /// <summary>
    /// Adds a synchronous validation rule to the railway.
    /// If a validation fails, the railway will not execute and will return the error.
    /// </summary>
    /// <param name="validation">The validation function that returns an optional error</param>
    /// <returns>The builder instance for method chaining</returns>
    public RailwayValidationBuilder<TRequest, TPayload, TSuccess, TError> Validate(
        Func<TRequest, Option<TError>> validation)
    {
        Validations.Add(new((request, _) => Task.FromResult(validation(request))));
        return this;
    }
}
