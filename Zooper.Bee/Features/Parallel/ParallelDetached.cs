using System;
using System.Collections.Generic;
using Zooper.Bee.Features.Detached;

namespace Zooper.Bee.Features.Parallel;

/// <summary>
/// Represents a parallel execution of multiple detached groups in the workflow that don't merge back.
/// </summary>
/// <typeparam name="TPayload">Type of the main workflow payload</typeparam>
/// <typeparam name="TError">Type of the error</typeparam>
internal sealed class ParallelDetached<TPayload, TError> : IWorkflowFeature<TPayload, TError>
{
	/// <summary>
	/// Parallel detached execution can have a condition, but typically runs unconditionally.
	/// </summary>
	public Func<TPayload, bool>? Condition { get; }

	/// <summary>
	/// Parallel detached execution never merges back into the main workflow.
	/// </summary>
	public bool ShouldMerge => false;

	/// <summary>
	/// The list of detached groups to execute in parallel.
	/// </summary>
	public List<Detached<TPayload, TError>> DetachedGroups { get; } = new();

	/// <summary>
	/// Creates a new parallel detached execution group with an optional condition.
	/// </summary>
	/// <param name="condition">The condition that determines if this parallel detached group should execute. If null, it always executes.</param>
	public ParallelDetached(Func<TPayload, bool>? condition = null)
	{
		Condition = condition;
	}
}