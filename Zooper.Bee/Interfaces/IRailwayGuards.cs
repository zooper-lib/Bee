namespace Zooper.Bee.Interfaces;

/// <summary>
/// Base marker interface for all railway guards.
/// </summary>
public interface IRailwayGuards;

/// <summary>
/// Interface for a collection of railway guards operating on the same request and error types.
/// </summary>
/// <typeparam name="TRequest">The type of the request.</typeparam>
/// <typeparam name="TError">The type of the error.</typeparam>
public interface IRailwayGuards<TRequest, TError> : IRailwayGuards;
