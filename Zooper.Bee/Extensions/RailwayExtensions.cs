using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Zooper.Fox;

namespace Zooper.Bee.Extensions;

/// <summary>
/// Provides extension methods for registering all railway components (validations, steps, and railway classes)
/// with dependency injection. These methods simplify the configuration process by centralizing the registration
/// of all railway-related services.
/// </summary>
public static class RailwayExtensions
{
	/// <summary>
	/// Executes a railway that doesn't require a request parameter.
	/// </summary>
	/// <typeparam name="TSuccess">The type of the success result</typeparam>
	/// <typeparam name="TError">The type of the error result</typeparam>
	/// <param name="railway">The railway to execute</param>
	/// <returns>The result of the railway execution</returns>
	public static Task<Either<TError, TSuccess>> Execute<TSuccess, TError>(this Railway<Unit, TSuccess, TError> railway)
	{
		return railway.Execute(Unit.Value);
	}

	/// <summary>
	/// Executes a railway that doesn't require a request parameter.
	/// </summary>
	/// <typeparam name="TSuccess">The type of the success result</typeparam>
	/// <typeparam name="TError">The type of the error result</typeparam>
	/// <param name="railway">The railway to execute</param>
	/// <param name="cancellationToken">A cancellation token to observe while waiting for the task to complete</param>
	/// <returns>The result of the railway execution</returns>
	public static Task<Either<TError, TSuccess>> Execute<TSuccess, TError>(
		this Railway<Unit, TSuccess, TError> railway,
		CancellationToken cancellationToken)
	{
		return railway.Execute(Unit.Value, cancellationToken);
	}

	/// <summary>
	/// Registers all railway components from the specified assemblies into the service collection.
	/// This includes railway validations, railway steps, and concrete railway classes.
	/// </summary>
	/// <param name="services">The service collection to add the registrations to</param>
	/// <param name="assemblies">Optional list of assemblies to scan for railway components. If null or empty, all non-system
	/// assemblies in the current AppDomain will be scanned</param>
	/// <param name="lifetime">The service lifetime to use for the registered services (defaults to Scoped)</param>
	/// <returns>The service collection for chaining additional registrations</returns>
	/// <remarks>
	/// This method provides a comprehensive registration of all railway-related components:
	/// - Railway validations (via AddRailwayValidations)
	/// - Railway steps (via AddRailwaySteps)
	/// - Railway guards (via AddRailwayGuards)
	/// - Concrete railway classes (classes ending with "Railway" or "Workflow")
	///
	/// Railway classes are registered as themselves (not by interface) to support direct injection.
	/// System and Microsoft assemblies are excluded by default when no specific assemblies are provided.
	/// </remarks>
	public static IServiceCollection AddRailways(
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

		// Register all railway validations
		services.AddRailwayValidations(assembliesToScan, lifetime);

		// Register all railway guards
		services.AddRailwayGuards(assembliesToScan, lifetime);

		// Register all railway steps
		services.AddRailwaySteps(assembliesToScan, lifetime);

		// Then register all classes ending with Railway or Workflow (for backward compat)
		services.Scan(scan => scan
			.FromAssemblies(assembliesToScan)
			.AddClasses(classes =>
				classes.Where(type =>
					(type.Name.EndsWith("Railway") || type.Name.EndsWith("Workflow"))
					&& type is { IsAbstract: false, IsInterface: false })
			)
			.AsSelf()
			.WithLifetime(lifetime)
		);

		return services;
	}
}
