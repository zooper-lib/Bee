using System;
using System.Collections.Generic;
using System.Reflection;
using Microsoft.Extensions.DependencyInjection;

namespace Zooper.Bee.Extensions;

/// <summary>
/// Provides extension methods for registering workflow validations with dependency injection.
/// </summary>
[Obsolete("Use RailwayValidationExtensions instead. This class will be removed in a future version.")]
public static class WorkflowValidationExtensions
{
	/// <summary>
	/// Registers all workflow validations from the specified assemblies into the service collection.
	/// </summary>
	[Obsolete("Use AddRailwayValidations instead. This method will be removed in a future version.")]
	public static IServiceCollection AddWorkflowValidations(
		this IServiceCollection services,
		IEnumerable<Assembly>? assemblies = null,
		ServiceLifetime lifetime = ServiceLifetime.Scoped)
	{
		return services.AddRailwayValidations(assemblies, lifetime);
	}
}
