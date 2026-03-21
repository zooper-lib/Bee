using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using Zooper.Bee.Interfaces;

namespace Zooper.Bee.Extensions;

/// <summary>
/// Provides extension methods for registering railway guards with dependency injection.
/// </summary>
public static class RailwayGuardExtensions
{
	/// <summary>
	/// Registers all railway guards from the specified assemblies into the service collection.
	/// This includes both individual railway guards (IRailwayGuard) and guard collections (IRailwayGuards).
	/// </summary>
	/// <param name="services">The service collection to add the registrations to</param>
	/// <param name="assemblies">Optional list of assemblies to scan for railway guards. If null or empty, all non-system
	/// assemblies in the current AppDomain will be scanned</param>
	/// <param name="lifetime">The service lifetime to use for the registered services (defaults to Scoped)</param>
	/// <returns>The service collection for chaining additional registrations</returns>
	public static IServiceCollection AddRailwayGuards(
		this IServiceCollection services,
		IEnumerable<Assembly>? assemblies = null,
		ServiceLifetime lifetime = ServiceLifetime.Scoped)
	{
		// If no assemblies are specified, use all loaded assemblies
		var assembliesToScan = assemblies != null
			? assemblies.ToArray()
			: AppDomain.CurrentDomain.GetAssemblies()
				.Where(a => !a.IsDynamic && !a.FullName.StartsWith("System") && !a.FullName.StartsWith("Microsoft"))
				.ToArray();

		// Register all IRailwayGuard implementations (also finds IWorkflowGuard since it inherits IRailwayGuard)
		services.Scan(scan => scan
			.FromAssemblies(assembliesToScan)
			.AddClasses(classes => classes.AssignableTo(typeof(IRailwayGuard)))
			.AsImplementedInterfaces()
			.WithLifetime(lifetime)
		);

		// Register all IRailwayGuards implementations (also finds IWorkflowGuards since it inherits IRailwayGuards)
		services.Scan(scan => scan
			.FromAssemblies(assembliesToScan)
			.AddClasses(classes => classes.AssignableTo(typeof(IRailwayGuards)))
			.AsImplementedInterfaces()
			.WithLifetime(lifetime)
		);

		return services;
	}
}
