using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using Zooper.Bee.Interfaces;

namespace Zooper.Bee.Extensions;

/// <summary>
/// Provides extension methods for registering workflow validations with dependency injection.
/// These methods use assembly scanning to automatically discover and register all implementations
/// of workflow validation interfaces within specified assemblies.
/// </summary>
public static class WorkflowValidationExtensions
{
	/// <summary>
	/// Registers all workflow validations from the specified assemblies into the service collection.
	/// This includes both individual workflow validations (IWorkflowValidation) and validation collections (IWorkflowValidations).
	/// </summary>
	/// <param name="services">The service collection to add the registrations to</param>
	/// <param name="assemblies">Optional list of assemblies to scan for workflow validations. If null or empty, all non-system 
	/// assemblies in the current AppDomain will be scanned</param>
	/// <param name="lifetime">The service lifetime to use for the registered services (defaults to Scoped)</param>
	/// <returns>The service collection for chaining additional registrations</returns>
	/// <remarks>
	/// This method uses Scrutor to scan assemblies and register classes based on their implemented interfaces.
	/// System and Microsoft assemblies are excluded by default when no specific assemblies are provided.
	/// </remarks>
	public static IServiceCollection AddWorkflowValidations(
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

		// Register all IWorkflowValidation implementations
		services.Scan(scan => scan
			.FromAssemblies(assembliesToScan)
			.AddClasses(classes => classes.AssignableTo(typeof(IWorkflowValidation)))
			.AsImplementedInterfaces()
			.WithLifetime(lifetime)
		);

		// Register all IWorkflowValidations implementations
		services.Scan(scan => scan
			.FromAssemblies(assembliesToScan)
			.AddClasses(classes => classes.AssignableTo(typeof(IWorkflowValidations)))
			.AsImplementedInterfaces()
			.WithLifetime(lifetime)
		);

		return services;
	}
}