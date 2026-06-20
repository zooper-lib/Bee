using System;
using Zooper.Fox;

namespace Zooper.Bee;

/// <summary>
/// Provides factory methods for creating railways using a three-phase builder pattern:
/// a validation phase, then a guarding phase, then a step execution phase.
/// </summary>
public static class Railway
{
    /// <summary>
    /// Creates a new railway with distinct validation, guarding, and step phases.
    /// </summary>
    /// <typeparam name="TRequest">The type of the request input.</typeparam>
    /// <typeparam name="TPayload">The type of the payload used to carry intermediate data.</typeparam>
    /// <typeparam name="TSuccess">The type of the success result.</typeparam>
    /// <typeparam name="TError">The type of the error result.</typeparam>
    /// <param name="factory">
    /// Factory function that takes a <typeparamref name="TRequest"/> and produces
    /// the initial <typeparamref name="TPayload"/>.
    /// </param>
    /// <param name="selector">
    /// Selector function that converts the final <typeparamref name="TPayload"/>
    /// into a success result of type <typeparamref name="TSuccess"/>.
    /// </param>
    /// <param name="validations">
    /// Optional action to configure validations that run first, before any guard or step.
    /// Pass <see langword="null"/> when no validations are needed.
    /// </param>
    /// <param name="guards">
    /// Optional action to configure guards that run after all validations and before any step.
    /// Pass <see langword="null"/> when no guards are needed.
    /// </param>
    /// <param name="steps">
    /// Action to configure the step execution phase.
    /// </param>
    /// <returns>A built <see cref="Railway{TRequest,TSuccess,TError}"/> ready for execution.</returns>
    public static Railway<TRequest, TSuccess, TError> Create<TRequest, TPayload, TSuccess, TError>(
        Func<TRequest, TPayload> factory,
        Func<TPayload, TSuccess> selector,
        Action<RailwayValidationBuilder<TRequest, TPayload, TSuccess, TError>>? validations,
        Action<RailwayGuardBuilder<TRequest, TPayload, TSuccess, TError>>? guards,
        Action<RailwayStepsBuilder<TRequest, TPayload, TSuccess, TError>> steps)
    {
        var validationBuilder = new RailwayValidationBuilder<TRequest, TPayload, TSuccess, TError>();
        validations?.Invoke(validationBuilder);

        var guardBuilder = new RailwayGuardBuilder<TRequest, TPayload, TSuccess, TError>();
        guards?.Invoke(guardBuilder);

        var stepsBuilder = new RailwayStepsBuilder<TRequest, TPayload, TSuccess, TError>(
            factory, selector, guardBuilder.Guards, validationBuilder.Validations);
        steps(stepsBuilder);

        return stepsBuilder.Build();
    }

    /// <summary>
    /// Creates a new railway with guarding and step phases (no validations).
    /// </summary>
    /// <typeparam name="TRequest">The type of the request input.</typeparam>
    /// <typeparam name="TPayload">The type of the payload used to carry intermediate data.</typeparam>
    /// <typeparam name="TSuccess">The type of the success result.</typeparam>
    /// <typeparam name="TError">The type of the error result.</typeparam>
    /// <param name="factory">Factory function that produces the initial payload from the request.</param>
    /// <param name="selector">Selector function that converts the final payload into a success result.</param>
    /// <param name="guards">Action to configure guards that run before any step.</param>
    /// <param name="steps">Action to configure the step execution phase.</param>
    /// <returns>A built <see cref="Railway{TRequest,TSuccess,TError}"/> ready for execution.</returns>
    public static Railway<TRequest, TSuccess, TError> Create<TRequest, TPayload, TSuccess, TError>(
        Func<TRequest, TPayload> factory,
        Func<TPayload, TSuccess> selector,
        Action<RailwayGuardBuilder<TRequest, TPayload, TSuccess, TError>> guards,
        Action<RailwayStepsBuilder<TRequest, TPayload, TSuccess, TError>> steps)
    {
        return Create(factory, selector, null, guards, steps);
    }

    /// <summary>
    /// Creates a new railway with only a step phase (no validations or guards).
    /// </summary>
    /// <typeparam name="TRequest">The type of the request input.</typeparam>
    /// <typeparam name="TPayload">The type of the payload used to carry intermediate data.</typeparam>
    /// <typeparam name="TSuccess">The type of the success result.</typeparam>
    /// <typeparam name="TError">The type of the error result.</typeparam>
    /// <param name="factory">Factory function that produces the initial payload from the request.</param>
    /// <param name="selector">Selector function that converts the final payload into a success result.</param>
    /// <param name="steps">Action to configure the step execution phase.</param>
    /// <returns>A built <see cref="Railway{TRequest,TSuccess,TError}"/> ready for execution.</returns>
    public static Railway<TRequest, TSuccess, TError> Create<TRequest, TPayload, TSuccess, TError>(
        Func<TRequest, TPayload> factory,
        Func<TPayload, TSuccess> selector,
        Action<RailwayStepsBuilder<TRequest, TPayload, TSuccess, TError>> steps)
    {
        return Create(factory, selector, null, null, steps);
    }

    /// <summary>
    /// Creates a new parameterless railway (no request input) with distinct validation, guarding, and step phases.
    /// </summary>
    /// <typeparam name="TPayload">The type of the payload used to carry intermediate data.</typeparam>
    /// <typeparam name="TSuccess">The type of the success result.</typeparam>
    /// <typeparam name="TError">The type of the error result.</typeparam>
    /// <param name="factory">Factory function that creates the initial payload.</param>
    /// <param name="selector">Selector function that converts the final payload into a success result.</param>
    /// <param name="validations">Optional action to configure validations that run first.</param>
    /// <param name="guards">Optional action to configure guards that run before any step.</param>
    /// <param name="steps">Action to configure the step execution phase.</param>
    /// <returns>A built <see cref="Railway{TRequest,TSuccess,TError}"/> ready for execution.</returns>
    public static Railway<Unit, TSuccess, TError> Create<TPayload, TSuccess, TError>(
        Func<TPayload> factory,
        Func<TPayload, TSuccess> selector,
        Action<RailwayValidationBuilder<Unit, TPayload, TSuccess, TError>>? validations,
        Action<RailwayGuardBuilder<Unit, TPayload, TSuccess, TError>>? guards,
        Action<RailwayStepsBuilder<Unit, TPayload, TSuccess, TError>> steps)
    {
        return Create<Unit, TPayload, TSuccess, TError>(_ => factory(), selector, validations, guards, steps);
    }

    /// <summary>
    /// Creates a new parameterless railway (no request input) with guarding and step phases.
    /// </summary>
    /// <typeparam name="TPayload">The type of the payload used to carry intermediate data.</typeparam>
    /// <typeparam name="TSuccess">The type of the success result.</typeparam>
    /// <typeparam name="TError">The type of the error result.</typeparam>
    /// <param name="factory">Factory function that creates the initial payload.</param>
    /// <param name="selector">Selector function that converts the final payload into a success result.</param>
    /// <param name="guards">Action to configure guards that run before any step.</param>
    /// <param name="steps">Action to configure the step execution phase.</param>
    /// <returns>A built <see cref="Railway{TRequest,TSuccess,TError}"/> ready for execution.</returns>
    public static Railway<Unit, TSuccess, TError> Create<TPayload, TSuccess, TError>(
        Func<TPayload> factory,
        Func<TPayload, TSuccess> selector,
        Action<RailwayGuardBuilder<Unit, TPayload, TSuccess, TError>> guards,
        Action<RailwayStepsBuilder<Unit, TPayload, TSuccess, TError>> steps)
    {
        return Create<Unit, TPayload, TSuccess, TError>(_ => factory(), selector, null, guards, steps);
    }

    /// <summary>
    /// Creates a new parameterless railway (no request input) with only a step phase.
    /// </summary>
    /// <typeparam name="TPayload">The type of the payload used to carry intermediate data.</typeparam>
    /// <typeparam name="TSuccess">The type of the success result.</typeparam>
    /// <typeparam name="TError">The type of the error result.</typeparam>
    /// <param name="factory">Factory function that creates the initial payload.</param>
    /// <param name="selector">Selector function that converts the final payload into a success result.</param>
    /// <param name="steps">Action to configure the step execution phase.</param>
    /// <returns>A built <see cref="Railway{TRequest,TSuccess,TError}"/> ready for execution.</returns>
    public static Railway<Unit, TSuccess, TError> Create<TPayload, TSuccess, TError>(
        Func<TPayload> factory,
        Func<TPayload, TSuccess> selector,
        Action<RailwayStepsBuilder<Unit, TPayload, TSuccess, TError>> steps)
    {
        return Create<Unit, TPayload, TSuccess, TError>(_ => factory(), selector, null, null, steps);
    }
}
