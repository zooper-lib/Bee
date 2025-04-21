using System;

namespace Zooper.Bee.Features;

/// <summary>
/// Base interface for all workflow features.
/// </summary>
/// <typeparam name="TPayload">The type of the main workflow payload</typeparam>
/// <typeparam name="TError">The type of the error</typeparam>
public interface IWorkflowFeature<TPayload, TError>
{
	/// <summary>
	/// Gets the condition that determines if this feature should execute.
	/// </summary>
	Func<TPayload, bool>? Condition { get; }

	/// <summary>
	/// Whether this feature should merge back into the main workflow.
	/// </summary>
	bool ShouldMerge { get; }
}