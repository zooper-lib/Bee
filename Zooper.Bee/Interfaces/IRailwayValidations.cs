namespace Zooper.Bee.Interfaces;

/// <summary>
/// Base marker interface for a collection of railway validations
/// </summary>
public interface IRailwayValidations;

/// <summary>
/// Interface for a collection of railway validations for a specific request and error type
/// </summary>
/// <typeparam name="TRequest">The type of request being validated</typeparam>
/// <typeparam name="TError">The type of error that might be returned</typeparam>
public interface IRailwayValidations<TRequest, TError> : IRailwayValidations;
