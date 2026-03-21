using System;

namespace Zooper.Bee.Interfaces;

/// <summary>
/// Base marker interface for all workflow validations
/// </summary>
[Obsolete("Use IRailwayValidation instead. This interface will be removed in a future version.")]
public interface IWorkflowValidation : IRailwayValidation;

/// <summary>
/// Interface for a validation that validates a request and potentially returns an error
/// </summary>
/// <typeparam name="TRequest">The type of request being validated</typeparam>
/// <typeparam name="TError">The type of error that might be returned</typeparam>
[Obsolete("Use IRailwayValidation<TRequest, TError> instead. This interface will be removed in a future version.")]
public interface IWorkflowValidation<in TRequest, TError> : IRailwayValidation<TRequest, TError>;
