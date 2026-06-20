using System;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Zooper.Fox;

// ReSharper disable IdentifierTypo

namespace Zooper.Bee.MediatR;

/// <summary>
/// Base class for railway handlers that process requests through MediatR.
/// Override <see cref="ConfigureSteps"/> to define the step execution phase,
/// and optionally override <see cref="ConfigureValidations"/> and/or <see cref="ConfigureGuards"/>.
/// The phases run in order: Validation, then Guarding, then Steps.
/// </summary>
/// <typeparam name="TRequest">The request type.</typeparam>
/// <typeparam name="TPayload">The internal railway payload type.</typeparam>
/// <typeparam name="TSuccess">The success result type.</typeparam>
/// <typeparam name="TError">The error result type.</typeparam>
public abstract class RailwayHandler<TRequest, TPayload, TSuccess, TError>
	: IRequestHandler<TRequest, Either<TError, TSuccess>>
	where TRequest : IRequest<Either<TError, TSuccess>>
{
	/// <summary>Gets the factory function that creates the initial payload from the request.</summary>
	protected abstract Func<TRequest, TPayload> PayloadFactory { get; }

	/// <summary>Gets the selector function that creates the success result from the final payload.</summary>
	protected abstract Func<TPayload, TSuccess> ResultSelector { get; }

	/// <summary>
	/// Configures the validation phase, which runs first. Override to add validations; the default adds none.
	/// </summary>
	protected virtual void ConfigureValidations(
		RailwayValidationBuilder<TRequest, TPayload, TSuccess, TError> builder) { }

	/// <summary>
	/// Configures the guarding phase, which runs after validations. Override to add guards; the default adds none.
	/// </summary>
	protected virtual void ConfigureGuards(
		RailwayGuardBuilder<TRequest, TPayload, TSuccess, TError> builder) { }

	/// <summary>Configures the step execution phase using the new operator vocabulary.</summary>
	protected abstract void ConfigureSteps(
		RailwayStepsBuilder<TRequest, TPayload, TSuccess, TError> builder);

	/// <summary>Handles the request and returns the result.</summary>
	public Task<Either<TError, TSuccess>> Handle(
		TRequest request,
		CancellationToken cancellationToken)
	{
		var railway = Railway.Create<TRequest, TPayload, TSuccess, TError>(
			PayloadFactory, ResultSelector, ConfigureValidations, ConfigureGuards, ConfigureSteps);
		return railway.Execute(request, cancellationToken);
	}
}
