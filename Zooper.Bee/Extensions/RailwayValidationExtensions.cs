using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using Zooper.Bee.Interfaces;

namespace Zooper.Bee.Extensions;

/// <summary>
/// Provides extension methods for registering railway validations with dependency injection.
/// </summary>
public static class RailwayValidationExtensions
{
	/// <summary>
	/// Registers all railway validations from the specified assemblies into the service collection.
	/// This includes both individual railway validations (IRailwayValidation) and validation collections (IRailwayValidations).
	/// </summary>
	/// <param name="services">The service collection to add the registrations to</param>
	/// <param name="assemblies">Optional list of assemblies to scan for railway validations. If null or empty, all non-system
	/// assemblies in the current AppDomain will be scanned</param>
	/// <param name="lifetime">The service lifetime to use for the registered services (defaults to Scoped)</param>
	/// <returns>The service collection for chaining additional registrations</returns>
	public static IServiceCollection AddRailwayValidations(
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

		// Register all IRailwayValidation implementations (also finds IWorkflowValidation since it inherits IRailwayValidation)
		services.Scan(scan => scan
			.FromAssemblies(assembliesToScan)
			.AddClasses(classes => classes.AssignableTo(typeof(IRailwayValidation)))
			.AsImplementedInterfaces()
			.WithLifetime(lifetime)
		);

		// Register all IRailwayValidations implementations (also finds IWorkflowValidations since it inherits IRailwayValidations)
		services.Scan(scan => scan
			.FromAssemblies(assembliesToScan)
			.AddClasses(classes => classes.AssignableTo(typeof(IRailwayValidations)))
			.AsImplementedInterfaces()
			.WithLifetime(lifetime)
		);

		return services;
	}
}
