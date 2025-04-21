using System;
using System.Collections.Generic;
using Zooper.Bee.Internal;

namespace Zooper.Bee.Features.Detached;

/// <summary>
/// Represents a detached group of activities in the workflow that doesn't merge back.
/// </summary>
/// <typeparam name="TPayload">Type of the main workflow payload</typeparam>
/// <typeparam name="TError">Type of the error</typeparam>
internal sealed class Detached<TPayload, TError> : IWorkflowFeature<TPayload, TError>
{
	/// <summary>
	/// The condition that determines if this detached group should execute.
	/// </summary>
	public Func<TPayload, bool>? Condition { get; }

	/// <summary>
	/// Detached groups never merge back into the main workflow.
	/// </summary>
	public bool ShouldMerge => false;

	/// <summary>
	/// The list of activities in this detached group.
	/// </summary>
	public List<WorkflowActivity<TPayload, TError>> Activities { get; } = new();

	/// <summary>
	/// Creates a new detached group with an optional condition.
	/// </summary>
	/// <param name="condition">The condition that determines if this detached group should execute. If null, the group always executes.</param>
	public Detached(Func<TPayload, bool>? condition = null)
	{
		Condition = condition;
	}
}