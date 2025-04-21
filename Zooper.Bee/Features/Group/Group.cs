using System;
using System.Collections.Generic;
using Zooper.Bee.Internal;

namespace Zooper.Bee.Features.Group;

/// <summary>
/// Represents a group of activities in the workflow with an optional condition.
/// </summary>
/// <typeparam name="TPayload">Type of the main workflow payload</typeparam>
/// <typeparam name="TError">Type of the error</typeparam>
internal sealed class Group<TPayload, TError> : IWorkflowFeature<TPayload, TError>
{
	/// <summary>
	/// The condition that determines if this group should execute.
	/// </summary>
	public Func<TPayload, bool>? Condition { get; }

	/// <summary>
	/// Groups always merge back into the main workflow.
	/// </summary>
	public bool ShouldMerge => true;

	/// <summary>
	/// The list of activities in this group.
	/// </summary>
	public List<WorkflowActivity<TPayload, TError>> Activities { get; } = new();

	/// <summary>
	/// Creates a new group with an optional condition.
	/// </summary>
	/// <param name="condition">The condition that determines if this group should execute. If null, the group always executes.</param>
	public Group(Func<TPayload, bool>? condition = null)
	{
		Condition = condition;
	}
}