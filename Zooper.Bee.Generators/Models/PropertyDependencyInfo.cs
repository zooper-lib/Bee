using System.Collections.Generic;

namespace Zooper.Bee.Generators.Models;

/// <summary>
/// Holds information about a property in a workflow payload and its dependencies.
/// </summary>
internal class PropertyDependencyInfo
{
	/// <summary>
	/// The name of the property.
	/// </summary>
	public string Name { get; set; } = string.Empty;

	/// <summary>
	/// The type of the property as a string.
	/// </summary>
	public string TypeName { get; set; } = string.Empty;

	/// <summary>
	/// The full property declaration including type and name.
	/// </summary>
	public string Declaration { get; set; } = string.Empty;

	/// <summary>
	/// Description of the property's purpose.
	/// </summary>
	public string Description { get; set; } = string.Empty;

	/// <summary>
	/// Names of properties this property depends on.
	/// </summary>
	public List<string> Dependencies { get; set; } = new();

	/// <summary>
	/// Whether this property has any dependencies.
	/// </summary>
	public bool HasDependencies => Dependencies.Count > 0;
}