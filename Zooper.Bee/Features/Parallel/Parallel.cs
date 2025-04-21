using System;
using System.Collections.Generic;
using Zooper.Bee.Features.Group;

namespace Zooper.Bee.Features.Parallel;

/// <summary>
/// Represents a parallel execution of multiple groups in the workflow.
/// </summary>
/// <typeparam name="TPayload">Type of the main workflow payload</typeparam>
/// <typeparam name="TError">Type of the error</typeparam>
internal sealed class Parallel<TPayload, TError> : IWorkflowFeature<TPayload, TError>
{
	/// <summary>
	/// Parallel execution can have a condition, but typically runs unconditionally.
	/// </summary>
	public Func<TPayload, bool>? Condition { get; }

	/// <summary>
	/// Parallel execution always merges back into the main workflow.
	/// </summary>
	public bool ShouldMerge => true;

	/// <summary>
	/// The list of groups to execute in parallel.
	/// </summary>
	public List<Group<TPayload, TError>> Groups { get; } = new();

	/// <summary>
	/// Creates a new parallel execution group with an optional condition.
	/// </summary>
	/// <param name="condition">The condition that determines if this parallel group should execute. If null, it always executes.</param>
	public Parallel(Func<TPayload, bool>? condition = null)
	{
		Condition = condition;
	}
}