using System.Threading;
using System.Threading.Tasks;
using Zooper.Fox;

namespace Zooper.Bee.Interfaces;

/// <summary>
/// Base marker interface for all workflow validations
/// </summary>
public interface IWorkflowValidation;

/// <summary>
/// Interface for a validation that validates a request and potentially returns an error
/// </summary>
/// <typeparam name="TRequest">The type of request being validated</typeparam>
/// <typeparam name="TError">The type of error that might be returned</typeparam>
public interface IWorkflowValidation<in TRequest, TError> : IWorkflowValidation
{
	/// <summary>
	/// Validates the request
	/// </summary>
	/// <param name="request">The request to validate</param>
	/// <param name="cancellationToken">Cancellation token</param>
	/// <returns>An option containing an error if validation fails, or None if validation succeeds</returns>
	Task<Option<TError>> Validate(
		TRequest request,
		CancellationToken cancellationToken);
}