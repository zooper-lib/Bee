using System;

namespace Zooper.Bee.Interfaces;

/// <summary>
/// Base marker interface for all workflow guards.
/// </summary>
[Obsolete("Use IRailwayGuard instead. This interface will be removed in a future version.")]
public interface IWorkflowGuard : IRailwayGuard;

/// <summary>
/// Represents a guard that checks if a workflow can be executed.
/// </summary>
/// <typeparam name="TRequest">The type of the request.</typeparam>
/// <typeparam name="TError">The type of the error.</typeparam>
[Obsolete("Use IRailwayGuard<TRequest, TError> instead. This interface will be removed in a future version.")]
public interface IWorkflowGuard<in TRequest, TError> : IRailwayGuard<TRequest, TError>;
