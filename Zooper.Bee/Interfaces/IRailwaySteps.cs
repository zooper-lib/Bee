namespace Zooper.Bee.Interfaces;

/// <summary>
/// Base marker interface for railway step collections
/// </summary>
public interface IRailwaySteps;

/// <summary>
/// Interface for a collection of railway steps operating on the same payload and error types
/// </summary>
/// <typeparam name="TPayload">The type of payload the steps process</typeparam>
/// <typeparam name="TError">The type of error the steps might return</typeparam>
public interface IRailwaySteps<TPayload, TError> : IRailwaySteps;
