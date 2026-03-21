using System;

namespace Zooper.Bee.Interfaces;

/// <summary>
/// Base marker interface for all workflow activities
/// </summary>
[Obsolete("Use IRailwayStep instead. This interface will be removed in a future version.")]
public interface IWorkflowActivity : IRailwayStep;

/// <summary>
/// Interface for an activity that works with a specific payload and error type
/// </summary>
/// <typeparam name="TPayload">The type of payload the activity processes</typeparam>
/// <typeparam name="TError">The type of error the activity might return</typeparam>
[Obsolete("Use IRailwayStep<TPayload, TError> instead. This interface will be removed in a future version.")]
public interface IWorkflowActivity<TPayload, TError> : IRailwayStep<TPayload, TError>;
