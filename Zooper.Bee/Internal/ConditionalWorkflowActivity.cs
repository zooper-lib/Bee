using System;

namespace Zooper.Bee.Internal;

/// <summary>
/// Represents a conditional activity in the workflow that only executes if a condition is met.
/// </summary>
/// <typeparam name="TPayload">Type of the payload</typeparam>
/// <typeparam name="TError">Type of the error</typeparam>
internal sealed class ConditionalWorkflowActivity<TPayload, TError>
{
	private readonly Func<TPayload, bool> _condition;

	public WorkflowActivity<TPayload, TError> Activity { get; }

	public ConditionalWorkflowActivity(
		Func<TPayload, bool> condition,
		WorkflowActivity<TPayload, TError> activity)
	{
		_condition = condition;
		Activity = activity;
	}

	public bool ShouldExecute(TPayload payload)
	{
		return _condition(payload);
	}
}