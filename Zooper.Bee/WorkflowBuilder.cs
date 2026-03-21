using System;

namespace Zooper.Bee;

/// <summary>
/// Represents a builder for a workflow that processes a request and
/// either succeeds with a <typeparamref name="TSuccess"/> result
/// or fails with a <typeparamref name="TError"/>.
/// </summary>
/// <typeparam name="TRequest">The type of the request input.</typeparam>
/// <typeparam name="TPayload">The type of the payload used to carry intermediate data.</typeparam>
/// <typeparam name="TSuccess">The type of the success result.</typeparam>
/// <typeparam name="TError">The type of the error result.</typeparam>
[Obsolete("Use RailwayBuilder<TRequest, TPayload, TSuccess, TError> instead. This class will be removed in a future version.")]
public sealed class WorkflowBuilder<TRequest, TPayload, TSuccess, TError>
	: RailwayBuilder<TRequest, TPayload, TSuccess, TError>
{
	/// <summary>
	/// Initializes a new instance of the <see cref="WorkflowBuilder{TRequest, TPayload, TSuccess, TError}"/> class.
	/// </summary>
	/// <param name="contextFactory">
	/// Factory function that takes a request of type <typeparamref name="TRequest"/>
	/// and produces a context of type <typeparamref name="TPayload"/>.
	/// </param>
	/// <param name="resultSelector">
	/// Selector function that converts the final <typeparamref name="TPayload"/>
	/// into a success result of type <typeparamref name="TSuccess"/>.
	/// </param>
	public WorkflowBuilder(
		Func<TRequest, TPayload> contextFactory,
		Func<TPayload, TSuccess> resultSelector)
		: base(contextFactory, resultSelector)
	{
	}
}
