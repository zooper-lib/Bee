using System.Collections.Generic;

namespace Zooper.Bee.Generators.Models;

/// <summary>
/// Holds information about a workflow payload class and its properties.
/// </summary>
internal class WorkflowPayloadInfo
{
	/// <summary>
	/// The name of the workflow payload class.
	/// </summary>
	public string Name { get; set; } = string.Empty;

	/// <summary>
	/// The full namespace of the workflow payload class.
	/// </summary>
	public string Namespace { get; set; } = string.Empty;

	/// <summary>
	/// Whether the class is partial.
	/// </summary>
	public bool IsPartial { get; set; }

	/// <summary>
	/// The type parameters of the class if it's generic.
	/// </summary>
	public List<string> TypeParameters { get; set; } = new();

	/// <summary>
	/// Whether the class is generic.
	/// </summary>
	public bool IsGeneric => TypeParameters.Count > 0;

	/// <summary>
	/// The full type name including any generic type parameters.
	/// </summary>
	public string FullTypeName => IsGeneric
		? $"{Name}<{string.Join(", ", TypeParameters)}>"
		: Name;

	/// <summary>
	/// All properties in the workflow payload class.
	/// </summary>
	public List<PropertyDependencyInfo> Properties { get; set; } = new();
}