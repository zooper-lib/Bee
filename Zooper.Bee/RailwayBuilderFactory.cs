using System;
using Zooper.Fox;

namespace Zooper.Bee;

/// <summary>
/// Provides factory methods for creating railways without requiring a request parameter.
/// </summary>
public static class RailwayBuilderFactory
{
	/// <summary>
	/// Creates a new railway builder that doesn't require a request parameter.
	/// </summary>
	/// <typeparam name="TPayload">The type of payload that will be used throughout the railway</typeparam>
	/// <typeparam name="TSuccess">The type of the success result</typeparam>
	/// <typeparam name="TError">The type of the error result</typeparam>
	/// <param name="payloadFactory">A factory function that creates the initial payload</param>
	/// <param name="resultSelector">A function that creates the success result from the final payload</param>
	/// <returns>A railway builder instance</returns>
	public static RailwayBuilder<Unit, TPayload, TSuccess, TError> Create<TPayload, TSuccess, TError>(
		Func<TPayload> payloadFactory,
		Func<TPayload, TSuccess> resultSelector)
	{
		return new RailwayBuilder<Unit, TPayload, TSuccess, TError>(
			_ => payloadFactory(),
			resultSelector);
	}

	/// <summary>
	/// Creates a new railway that doesn't require a request parameter.
	/// </summary>
	/// <typeparam name="TPayload">The type of payload that will be used throughout the railway</typeparam>
	/// <typeparam name="TSuccess">The type of the success result</typeparam>
	/// <typeparam name="TError">The type of the error result</typeparam>
	/// <param name="payloadFactory">A factory function that creates the initial payload</param>
	/// <param name="resultSelector">A function that creates the success result from the final payload</param>
	/// <param name="configure">An action that configures the railway</param>
	/// <returns>A railway instance</returns>
	public static Railway<Unit, TSuccess, TError> CreateRailway<TPayload, TSuccess, TError>(
		Func<TPayload> payloadFactory,
		Func<TPayload, TSuccess> resultSelector,
		Action<RailwayBuilder<Unit, TPayload, TSuccess, TError>> configure)
	{
		var builder = new RailwayBuilder<Unit, TPayload, TSuccess, TError>(
			_ => payloadFactory(),
			resultSelector);

		configure(builder);
		return builder.Build();
	}
}
