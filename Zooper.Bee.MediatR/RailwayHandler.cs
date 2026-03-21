using System;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Zooper.Fox;

// ReSharper disable IdentifierTypo

namespace Zooper.Bee.MediatR;

/// <summary>
/// Base class for railway handlers that process requests through MediatR
/// </summary>
/// <typeparam name="TRequest">The request type</typeparam>
/// <typeparam name="TPayload">The internal railway payload type</typeparam>
/// <typeparam name="TSuccess">The success result type</typeparam>
/// <typeparam name="TError">The error result type</typeparam>
public abstract class RailwayHandler<TRequest, TPayload, TSuccess, TError>
	: IRequestHandler<TRequest, Either<TError, TSuccess>>
	where TRequest : IRequest<Either<TError, TSuccess>>
{
	/// <summary>
	/// Gets the factory function to create the initial payload from the request.
	/// </summary>
	protected abstract Func<TRequest, TPayload> PayloadFactory { get; }

	/// <summary>
	/// Gets the selector function to create the success result from the final payload.
	/// </summary>
	protected abstract Func<TPayload, TSuccess> ResultSelector { get; }

	/// <summary>
	/// Configures the railway using the provided builder.
	/// </summary>
	/// <param name="builder">The railway builder to configure</param>
	protected abstract void ConfigureRailway(RailwayBuilder<TRequest, TPayload, TSuccess, TError> builder);

	/// <summary>
	/// Handles the request and returns the result.
	/// </summary>
	/// <param name="request">The request to handle</param>
	/// <param name="cancellationToken">The cancellation token</param>
	/// <returns>Either an error or success result</returns>
	public async Task<Either<TError, TSuccess>> Handle(
		TRequest request,
		CancellationToken cancellationToken)
	{
		var railway = CreateRailway();
		return await railway.Execute(request, cancellationToken);
	}

	/// <summary>
	/// Creates the railway instance.
	/// </summary>
	/// <returns>The configured railway</returns>
	// ReSharper disable once MemberCanBePrivate.Global
	protected Railway<TRequest, TSuccess, TError> CreateRailway()
	{
		var builder = new RailwayBuilder<TRequest, TPayload, TSuccess, TError>(
			PayloadFactory,
			ResultSelector
		);

		ConfigureRailway(builder);

		return builder.Build();
	}
}
