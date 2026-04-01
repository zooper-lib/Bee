using System;
using Zooper.Fox;

namespace Zooper.Bee;

/// <summary>
/// Provides factory methods for creating railways using a two-phase builder pattern:
/// a guard/validation phase followed by a step execution phase.
/// </summary>
public static class Railway
{
    /// <summary>
    /// Creates a new railway with distinct guard and step phases.
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
    /// <param name="guards">
    /// Optional action to configure guards and validations that run before any step.
    /// Pass <see langword="null"/> when no guards are needed.
    /// </param>
    /// <param name="steps">
    /// Action to configure the step execution phase.
    /// </param>
    /// <returns>A built <see cref="Railway{TRequest,TSuccess,TError}"/> ready for execution.</returns>
    public static Railway<TRequest, TSuccess, TError> Create<TRequest, TPayload, TSuccess, TError>(
        Func<TRequest, TPayload> factory,
        Func<TPayload, TSuccess> selector,
        Action<RailwayGuardBuilder<TRequest, TPayload, TSuccess, TError>>? guards,
        Action<RailwayStepsBuilder<TRequest, TPayload, TSuccess, TError>> steps)
    {
        var guardBuilder = new RailwayGuardBuilder<TRequest, TPayload, TSuccess, TError>();
        guards?.Invoke(guardBuilder);

        var stepsBuilder = new RailwayStepsBuilder<TRequest, TPayload, TSuccess, TError>(
            factory, selector, guardBuilder.Guards, guardBuilder.Validations);
        steps(stepsBuilder);

        return stepsBuilder.Build();
    }

    /// <summary>
    /// Creates a new railway with only a step phase and no guards.
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
    /// <param name="steps">
    /// Action to configure the step execution phase.
    /// </param>
    /// <returns>A built <see cref="Railway{TRequest,TSuccess,TError}"/> ready for execution.</returns>
    public static Railway<TRequest, TSuccess, TError> Create<TRequest, TPayload, TSuccess, TError>(
        Func<TRequest, TPayload> factory,
        Func<TPayload, TSuccess> selector,
        Action<RailwayStepsBuilder<TRequest, TPayload, TSuccess, TError>> steps)
    {
        return Create(factory, selector, null, steps);
    }

    /// <summary>
    /// Creates a new parameterless railway (no request input) with distinct guard and step phases.
    /// </summary>
    /// <typeparam name="TPayload">The type of the payload used to carry intermediate data.</typeparam>
    /// <typeparam name="TSuccess">The type of the success result.</typeparam>
    /// <typeparam name="TError">The type of the error result.</typeparam>
    /// <param name="factory">Factory function that creates the initial payload.</param>
    /// <param name="selector">Selector function that converts the final payload into a success result.</param>
    /// <param name="guards">
    /// Optional action to configure guards and validations that run before any step.
    /// </param>
    /// <param name="steps">Action to configure the step execution phase.</param>
    /// <returns>A built <see cref="Railway{TRequest,TSuccess,TError}"/> ready for execution.</returns>
    public static Railway<Unit, TSuccess, TError> Create<TPayload, TSuccess, TError>(
        Func<TPayload> factory,
        Func<TPayload, TSuccess> selector,
        Action<RailwayGuardBuilder<Unit, TPayload, TSuccess, TError>>? guards,
        Action<RailwayStepsBuilder<Unit, TPayload, TSuccess, TError>> steps)
    {
        return Create<Unit, TPayload, TSuccess, TError>(_ => factory(), selector, guards, steps);
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
        return Create(factory, selector, null, steps);
    }
}
