using System;
using System.Collections.Generic;

namespace Zooper.Bee.Internal;

/// <summary>
/// Represents a branch in the workflow with its own condition and activities.
/// </summary>
/// <typeparam name="TPayload">Type of the payload</typeparam>
/// <typeparam name="TError">Type of the error</typeparam>
internal sealed class Branch<TPayload, TError>
{
	public Func<TPayload, bool> Condition { get; }
	public List<WorkflowActivity<TPayload, TError>> Activities { get; } = [];

	public Branch(Func<TPayload, bool> condition)
	{
		Condition = condition;
	}
}