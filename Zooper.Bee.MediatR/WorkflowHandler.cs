using System;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Zooper.Fox;

// ReSharper disable IdentifierTypo

namespace Zooper.Bee.MediatR;

/// <summary>
/// Obsolete workflow handler base class. Use <see cref="RailwayHandler{TRequest,TPayload,TSuccess,TError}"/> instead.
/// </summary>
[Obsolete("Use RailwayHandler<TRequest, TPayload, TSuccess, TError> instead. This class has been removed in 4.0.0.")]
public abstract class WorkflowHandler<TRequest, TPayload, TSuccess, TError>
	: RailwayHandler<TRequest, TPayload, TSuccess, TError>
	where TRequest : IRequest<Either<TError, TSuccess>>
{
	/// <summary>
	/// Configures the workflow. Override <see cref="RailwayHandler{TRequest,TPayload,TSuccess,TError}.ConfigureSteps"/> instead.
	/// </summary>
	[Obsolete("Override ConfigureSteps on RailwayHandler instead.")]
	protected virtual void ConfigureWorkflow(
		RailwayStepsBuilder<TRequest, TPayload, TSuccess, TError> builder) { }

	/// <inheritdoc/>
	protected override void ConfigureSteps(
		RailwayStepsBuilder<TRequest, TPayload, TSuccess, TError> builder)
		=> ConfigureWorkflow(builder);
}
