using System;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Zooper.Fox;

// ReSharper disable IdentifierTypo

namespace Zooper.Bee.MediatR;

/// <summary>
/// Base class for workflow handlers that process requests through MediatR
/// </summary>
/// <typeparam name="TRequest">The request type</typeparam>
/// <typeparam name="TPayload">The internal workflow payload type</typeparam>
/// <typeparam name="TSuccess">The success result type</typeparam>
/// <typeparam name="TError">The error result type</typeparam>
public abstract class WorkflowHandler<TRequest, TPayload, TSuccess, TError>
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
	/// Configures the workflow using the provided builder.
	/// </summary>
	/// <param name="builder">The workflow builder to configure</param>
	protected abstract void ConfigureWorkflow(WorkflowBuilder<TRequest, TPayload, TSuccess, TError> builder);

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
		var workflow = CreateWorkflow();
		return await workflow.Execute(request, cancellationToken);
	}

	/// <summary>
	/// Creates the workflow instance.
	/// </summary>
	/// <returns>The configured workflow</returns>
	// ReSharper disable once MemberCanBePrivate.Global
	protected Workflow<TRequest, TSuccess, TError> CreateWorkflow()
	{
		var builder = new WorkflowBuilder<TRequest, TPayload, TSuccess, TError>(
			PayloadFactory,
			ResultSelector
		);

		ConfigureWorkflow(builder);

		return builder.Build();
	}
}