using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using Zooper.Bee.Interfaces;

// ReSharper disable MemberCanBePrivate.Global

namespace Zooper.Bee.Extensions;

/// <summary>
/// Extension methods for registering workflow activities with dependency injection
/// </summary>
public static class WorkflowActivitiesExtensions
{
	/// <summary>
	/// Adds all workflow activities from the specified assemblies, or from all loaded assemblies if none specified
	/// </summary>
	/// <param name="services">The service collection</param>
	/// <param name="assemblies">Optional list of assemblies to scan. If null or empty, scans all loaded assemblies</param>
	/// <param name="lifetime">The service lifetime (defaults to Scoped)</param>
	/// <returns>The service collection for chaining</returns>
	public static IServiceCollection AddWorkflowActivities(
		this IServiceCollection services,
		IEnumerable<Assembly>? assemblies = null,
		ServiceLifetime lifetime = ServiceLifetime.Scoped)
	{
		// If no assemblies are specified, use all loaded assemblies
		assemblies ??= AppDomain.CurrentDomain.GetAssemblies()
			.Where(a => !a.IsDynamic && !a.FullName.StartsWith("System") && !a.FullName.StartsWith("Microsoft"));

		// Register all IWorkflowActivity implementations
		services.Scan(scan => scan
			.FromAssemblies(assemblies)
			.AddClasses(classes => classes.AssignableTo(typeof(IWorkflowActivity)))
			.AsImplementedInterfaces()
			.WithLifetime(lifetime)
		);

		// Register all IWorkflowActivities implementations
		services.Scan(scan => scan
			.FromAssemblies(assemblies)
			.AddClasses(classes => classes.AssignableTo(typeof(IWorkflowActivities)))
			.AsImplementedInterfaces()
			.WithLifetime(lifetime)
		);

		return services;
	}

	/// <summary>
	/// Adds all workflow activities from the assemblies containing the specified marker types
	/// </summary>
	/// <param name="services">The service collection</param>
	/// <param name="markerTypes">Types whose assemblies will be scanned</param>
	/// <param name="lifetime">The service lifetime (defaults to Scoped)</param>
	/// <returns>The service collection for chaining</returns>
	public static IServiceCollection AddWorkflowActivitiesFromAssembliesContaining(
		this IServiceCollection services,
		IEnumerable<Type> markerTypes,
		ServiceLifetime lifetime = ServiceLifetime.Scoped)
	{
		if (markerTypes == null) throw new ArgumentNullException(nameof(markerTypes));

		var assemblies = markerTypes.Select(t => t.Assembly).Distinct();
		return services.AddWorkflowActivities(assemblies, lifetime);
	}

	/// <summary>
	/// Adds all workflow activities from the assemblies containing the specified marker types
	/// </summary>
	/// <param name="services">The service collection</param>
	/// <param name="lifetime">The service lifetime (defaults to Scoped)</param>
	/// <param name="markerTypes">Types whose assemblies will be scanned</param>
	/// <returns>The service collection for chaining</returns>
	public static IServiceCollection AddWorkflowActivitiesFromAssembliesContaining(
		this IServiceCollection services,
		ServiceLifetime lifetime,
		params Type[] markerTypes)
	{
		return services.AddWorkflowActivitiesFromAssembliesContaining(markerTypes, lifetime);
	}

	/// <summary>
	/// Adds all workflow activities from the assemblies containing the specified marker types
	/// </summary>
	/// <param name="services">The service collection</param>
	/// <param name="markerTypes">Types whose assemblies will be scanned</param>
	/// <returns>The service collection for chaining</returns>
	public static IServiceCollection AddWorkflowActivitiesFromAssembliesContaining(
		this IServiceCollection services,
		params Type[] markerTypes)
	{
		return services.AddWorkflowActivitiesFromAssembliesContaining(markerTypes, ServiceLifetime.Scoped);
	}

	/// <summary>
	/// Adds all workflow activities from the assemblies containing the specified marker types
	/// </summary>
	/// <typeparam name="T1">First marker type whose assembly will be scanned</typeparam>
	/// <param name="services">The service collection</param>
	/// <param name="lifetime">The service lifetime (defaults to Scoped)</param>
	/// <returns>The service collection for chaining</returns>
	public static IServiceCollection AddWorkflowActivitiesFromAssembliesContaining<T1>(
		this IServiceCollection services,
		ServiceLifetime lifetime = ServiceLifetime.Scoped)
	{
		return services.AddWorkflowActivitiesFromAssembliesContaining(
			[
				typeof(T1)
			],
			lifetime
		);
	}

	/// <summary>
	/// Adds all workflow activities from the assemblies containing the specified marker types
	/// </summary>
	/// <typeparam name="T1">First marker type whose assembly will be scanned</typeparam>
	/// <typeparam name="T2">Second marker type whose assembly will be scanned</typeparam>
	/// <param name="services">The service collection</param>
	/// <param name="lifetime">The service lifetime (defaults to Scoped)</param>
	/// <returns>The service collection for chaining</returns>
	public static IServiceCollection AddWorkflowActivitiesFromAssembliesContaining<T1, T2>(
		this IServiceCollection services,
		ServiceLifetime lifetime = ServiceLifetime.Scoped)
	{
		return services.AddWorkflowActivitiesFromAssembliesContaining(
			[
				typeof(T1), typeof(T2)
			],
			lifetime
		);
	}

	/// <summary>
	/// Adds all workflow activities from the assemblies containing the specified marker types
	/// </summary>
	/// <typeparam name="T1">First marker type whose assembly will be scanned</typeparam>
	/// <typeparam name="T2">Second marker type whose assembly will be scanned</typeparam>
	/// <typeparam name="T3">Third marker type whose assembly will be scanned</typeparam>
	/// <param name="services">The service collection</param>
	/// <param name="lifetime">The service lifetime (defaults to Scoped)</param>
	/// <returns>The service collection for chaining</returns>
	public static IServiceCollection AddWorkflowActivitiesFromAssembliesContaining<T1, T2, T3>(
		this IServiceCollection services,
		ServiceLifetime lifetime = ServiceLifetime.Scoped)
	{
		return services.AddWorkflowActivitiesFromAssembliesContaining(
			[
				typeof(T1), typeof(T2), typeof(T3)
			],
			lifetime
		);
	}

	/// <summary>
	/// Adds all workflow activities of specific types from the specified assemblies, or from all loaded assemblies if none specified
	/// </summary>
	/// <typeparam name="TPayload">The type of payload the activities process</typeparam>
	/// <typeparam name="TError">The type of error the activities might return</typeparam>
	/// <param name="services">The service collection</param>
	/// <param name="assemblies">Optional list of assemblies to scan. If null or empty, scans all loaded assemblies</param>
	/// <param name="lifetime">The service lifetime (defaults to Scoped)</param>
	/// <returns>The service collection for chaining</returns>
	public static IServiceCollection AddWorkflowActivities<TPayload, TError>(
		this IServiceCollection services,
		IEnumerable<Assembly>? assemblies = null,
		ServiceLifetime lifetime = ServiceLifetime.Scoped)
	{
		// If no assemblies are specified, use all loaded assemblies
		assemblies ??= AppDomain.CurrentDomain.GetAssemblies()
			.Where(a => !a.IsDynamic && !a.FullName.StartsWith("System") && !a.FullName.StartsWith("Microsoft"));

		// Register all IWorkflowActivity<TPayload, TError> implementations
		services.Scan(scan => scan
			.FromAssemblies(assemblies)
			.AddClasses(classes => classes.AssignableTo(typeof(IWorkflowActivity<TPayload, TError>)))
			.AsImplementedInterfaces()
			.WithLifetime(lifetime)
		);

		// Register all IWorkflowActivities<TPayload, TError> implementations
		services.Scan(scan => scan
			.FromAssemblies(assemblies)
			.AddClasses(classes => classes.AssignableTo(typeof(IWorkflowActivities<TPayload, TError>)))
			.AsImplementedInterfaces()
			.WithLifetime(lifetime)
		);

		return services;
	}
}