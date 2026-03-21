using System;

namespace Zooper.Bee.Interfaces;

/// <summary>
/// Base marker interface for all workflow guards.
/// </summary>
[Obsolete("Use IRailwayGuards instead. This interface will be removed in a future version.")]
public interface IWorkflowGuards : IRailwayGuards;

/// <summary>
/// Interface for a collection of workflow guards operating on the same request and error types.
/// </summary>
/// <typeparam name="TRequest">The type of the request.</typeparam>
/// <typeparam name="TError">The type of the error.</typeparam>
[Obsolete("Use IRailwayGuards<TRequest, TError> instead. This interface will be removed in a future version.")]
public interface IWorkflowGuards<TRequest, TError> : IRailwayGuards<TRequest, TError>;
