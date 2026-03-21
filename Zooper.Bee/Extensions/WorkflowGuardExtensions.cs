using System;
using System.Collections.Generic;
using System.Reflection;
using Microsoft.Extensions.DependencyInjection;

namespace Zooper.Bee.Extensions;

/// <summary>
/// Provides extension methods for registering workflow guards with dependency injection.
/// </summary>
[Obsolete("Use RailwayGuardExtensions instead. This class will be removed in a future version.")]
public static class WorkflowGuardExtensions
{
	/// <summary>
	/// Registers all workflow guards from the specified assemblies into the service collection.
	/// </summary>
	[Obsolete("Use AddRailwayGuards instead. This method will be removed in a future version.")]
	public static IServiceCollection AddWorkflowGuards(
		this IServiceCollection services,
		IEnumerable<Assembly>? assemblies = null,
		ServiceLifetime lifetime = ServiceLifetime.Scoped)
	{
		return services.AddRailwayGuards(assemblies, lifetime);
	}
}
