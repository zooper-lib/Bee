using System;
using System.Collections.Generic;

namespace Zooper.Bee.Features.Context;

/// <summary>
/// Represents a context in the workflow with its own local state and an optional condition.
/// </summary>
/// <typeparam name="TPayload">Type of the main workflow payload</typeparam>
/// <typeparam name="TLocalState">Type of the local context state</typeparam>
/// <typeparam name="TError">Type of the error</typeparam>
internal sealed class Context<TPayload, TLocalState, TError> : IWorkflowFeature<TPayload, TError>
{
	/// <summary>
	/// The condition that determines if this context should execute.
	/// </summary>
	public Func<TPayload, bool>? Condition { get; }

	/// <summary>
	/// Contexts always merge back into the main workflow.
	/// </summary>
	public bool ShouldMerge => true;

	/// <summary>
	/// The factory function that creates the local state from the main payload.
	/// </summary>
	public Func<TPayload, TLocalState> LocalStateFactory { get; }

	/// <summary>
	/// The list of activities in this context that operate on both the main and local states.
	/// </summary>
	public List<ContextActivity<TPayload, TLocalState, TError>> Activities { get; } = new();

	/// <summary>
	/// Creates a new context with an optional condition.
	/// </summary>
	/// <param name="condition">The condition that determines if this context should execute. If null, the context always executes.</param>
	/// <param name="localStateFactory">The factory function that creates the local state</param>
	public Context(Func<TPayload, bool>? condition, Func<TPayload, TLocalState> localStateFactory)
	{
		Condition = condition;
		LocalStateFactory = localStateFactory ?? throw new ArgumentNullException(nameof(localStateFactory));
	}
}