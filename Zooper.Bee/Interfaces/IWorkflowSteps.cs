using System;

namespace Zooper.Bee.Interfaces;

/// <summary>
/// Base marker interface for workflow step collections
/// </summary>
[Obsolete("Use IRailwaySteps instead. This interface will be removed in a future version.")]
public interface IWorkflowSteps : IRailwaySteps;

/// <summary>
/// Interface for a collection of workflow steps operating on the same payload and error types
/// </summary>
/// <typeparam name="TPayload">The type of payload the steps process</typeparam>
/// <typeparam name="TError">The type of error the steps might return</typeparam>
[Obsolete("Use IRailwaySteps<TPayload, TError> instead. This interface will be removed in a future version.")]
public interface IWorkflowSteps<TPayload, TError> : IRailwaySteps<TPayload, TError>;
