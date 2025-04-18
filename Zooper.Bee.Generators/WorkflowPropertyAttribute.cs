using System;

namespace Zooper.Bee.Generators;

/// <summary>
/// Marks a property in a workflow payload class with its dependencies.
/// Used by the source generator to create validation and dependency tracking.
/// </summary>
[AttributeUsage(AttributeTargets.Property, Inherited = false, AllowMultiple = false)]
public sealed class WorkflowPropertyAttribute : Attribute
{
	/// <summary>
	/// Names of properties that this property depends on.
	/// </summary>
	public string[] DependsOn { get; set; } = Array.Empty<string>();

	/// <summary>
	/// Optional description of the property's purpose in the workflow.
	/// </summary>
	public string Description { get; set; } = string.Empty;
}