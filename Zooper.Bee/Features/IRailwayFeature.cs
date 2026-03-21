using System;

namespace Zooper.Bee.Features;

/// <summary>
/// Base interface for all railway features.
/// </summary>
/// <typeparam name="TPayload">The type of the main railway payload</typeparam>
/// <typeparam name="TError">The type of the error</typeparam>
public interface IRailwayFeature<in TPayload, TError>
{
	/// <summary>
	/// Gets the condition that determines if this feature should execute.
	/// </summary>
	Func<TPayload, bool>? Condition { get; }

	/// <summary>
	/// Whether this feature should merge back into the main railway.
	/// </summary>
	bool ShouldMerge { get; }
}
