using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using Zooper.Bee.Interfaces;

namespace Zooper.Bee.Extensions;

/// <summary>
/// Provides extension methods for registering workflow guards with dependency injection.
/// </summary>
public static class WorkflowGuardExtensions
{
	/// <summary>
	/// Registers all workflow guards from the specified assemblies into the service collection.
	/// This includes both individual workflow guards (IWorkflowGuard) and guard collections (IWorkflowGuards).
	/// </summary>
	/// <param name="services">The service collection to add the registrations to</param>
	/// <param name="assemblies">Optional list of assemblies to scan for workflow guards. If null or empty, all non-system 
	/// assemblies in the current AppDomain will be scanned</param>
	/// <param name="lifetime">The service lifetime to use for the registered services (defaults to Scoped)</param>
	/// <returns>The service collection for chaining additional registrations</returns>
	public static IServiceCollection AddWorkflowGuards(
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

		// Register all IWorkflowGuard implementations
		services.Scan(scan => scan
			.FromAssemblies(assembliesToScan)
			.AddClasses(classes => classes.AssignableTo(typeof(IWorkflowGuard)))
			.AsImplementedInterfaces()
			.WithLifetime(lifetime)
		);

		// Register all IWorkflowGuards implementations
		services.Scan(scan => scan
			.FromAssemblies(assembliesToScan)
			.AddClasses(classes => classes.AssignableTo(typeof(IWorkflowGuards)))
			.AsImplementedInterfaces()
			.WithLifetime(lifetime)
		);

		return services;
	}
}