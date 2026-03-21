using System;

namespace Zooper.Bee.Features;

/// <summary>
/// Base interface for all workflow features.
/// </summary>
/// <typeparam name="TPayload">The type of the main workflow payload</typeparam>
/// <typeparam name="TError">The type of the error</typeparam>
[Obsolete("Use IRailwayFeature<TPayload, TError> instead. This interface will be removed in a future version.")]
public interface IWorkflowFeature<in TPayload, TError> : IRailwayFeature<TPayload, TError>;
