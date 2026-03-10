using System;

namespace Zooper.Bee.Internal;

/// <summary>
/// Represents a conditional step in the workflow that only executes if a condition is met.
/// </summary>
/// <typeparam name="TPayload">Type of the payload</typeparam>
/// <typeparam name="TError">Type of the error</typeparam>
internal sealed class ConditionalWorkflowStep<TPayload, TError>
{
	private readonly Func<TPayload, bool> _condition;

	public WorkflowStep<TPayload, TError> Activity { get; }

	public ConditionalWorkflowStep(
		Func<TPayload, bool> condition,
		WorkflowStep<TPayload, TError> activity)
	{
		_condition = condition;
		Activity = activity;
	}

	public bool ShouldExecute(TPayload payload)
	{
		return _condition(payload);
	}
}
