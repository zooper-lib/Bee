using System;
using System.Threading;
using System.Threading.Tasks;
using Zooper.Fox;

namespace Zooper.Bee.Internal;

/// <summary>
/// Represents a validation step that operates on a request.
/// </summary>
/// <typeparam name="TRequest">Type of the request</typeparam>
/// <typeparam name="TError">Type of the error</typeparam>
internal sealed class WorkflowValidation<TRequest, TError>
{
	private readonly Func<TRequest, CancellationToken, Task<Option<TError>>> _validation;
	private readonly string? _name;

	public WorkflowValidation(
		Func<TRequest, CancellationToken, Task<Option<TError>>> validation,
		string? name = null)
	{
		_validation = validation;
		_name = name;
	}

	public Task<Option<TError>> Validate(TRequest request, CancellationToken token)
	{
		return _validation(request, token);
	}
}