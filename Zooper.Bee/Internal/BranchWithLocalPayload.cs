using System;
using System.Collections.Generic;

namespace Zooper.Bee.Internal;

/// <summary>
/// Represents a branch in the workflow with its own condition, activities, and local payload.
/// </summary>
/// <typeparam name="TPayload">Type of the main workflow payload</typeparam>
/// <typeparam name="TLocalPayload">Type of the local branch payload</typeparam>
/// <typeparam name="TError">Type of the error</typeparam>
internal sealed class BranchWithLocalPayload<TPayload, TLocalPayload, TError>
{
	/// <summary>
	/// The condition that determines if this branch should execute.
	/// </summary>
	public Func<TPayload, bool> Condition { get; }

	/// <summary>
	/// The factory function that creates the local payload from the main payload.
	/// </summary>
	public Func<TPayload, TLocalPayload> LocalPayloadFactory { get; }

	/// <summary>
	/// The list of activities in this branch that operate on both the main and local payloads.
	/// </summary>
	public List<BranchActivity<TPayload, TLocalPayload, TError>> Activities { get; } = [];

	/// <summary>
	/// Creates a new branch with a local payload.
	/// </summary>
	/// <param name="condition">The condition that determines if this branch should execute</param>
	/// <param name="localPayloadFactory">The factory function that creates the local payload</param>
	public BranchWithLocalPayload(Func<TPayload, bool> condition, Func<TPayload, TLocalPayload> localPayloadFactory)
	{
		Condition = condition;
		LocalPayloadFactory = localPayloadFactory;
	}
}