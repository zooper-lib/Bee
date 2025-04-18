using System;
using System.Threading;
using System.Threading.Tasks;
using Zooper.Fox;

namespace Zooper.Bee;

/// <summary>
/// Represents a workflow that processes a request and either succeeds with a result of type <typeparamref name="TSuccess"/>
/// or fails with an error of type <typeparamref name="TError"/>.
/// </summary>
/// <typeparam name="TRequest">The type of the request</typeparam>
/// <typeparam name="TSuccess">The type of the success result</typeparam>
/// <typeparam name="TError">The type of the error result</typeparam>
public sealed class Workflow<TRequest, TSuccess, TError>
{
	private readonly Func<TRequest, CancellationToken, Task<Either<TError, TSuccess>>> _executor;

	internal Workflow(Func<TRequest, CancellationToken, Task<Either<TError, TSuccess>>> executor)
	{
		_executor = executor;
	}

	/// <summary>
	/// Executes the workflow with the specified request.
	/// </summary>
	/// <param name="request">The request to process</param>
	/// <param name="cancellationToken">A cancellation token to abort the operation</param>
	/// <returns>
	/// A task that represents the asynchronous operation. The task result contains
	/// either a success value of type <typeparamref name="TSuccess"/> or an error of type <typeparamref name="TError"/>.
	/// </returns>
	public Task<Either<TError, TSuccess>> Execute(TRequest request, CancellationToken cancellationToken = default)
	{
		return _executor(request, cancellationToken);
	}
}