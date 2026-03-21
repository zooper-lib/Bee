using System;
using System.Collections.Generic;
using System.Reflection;
using Microsoft.Extensions.DependencyInjection;

// ReSharper disable MemberCanBePrivate.Global

namespace Zooper.Bee.Extensions;

/// <summary>
/// Extension methods for registering workflow steps with dependency injection
/// </summary>
[Obsolete("Use RailwayStepsExtensions instead. This class will be removed in a future version.")]
public static class WorkflowStepsExtensions
{
	/// <summary>
	/// Adds all workflow steps from the specified assemblies, or from all loaded assemblies if none specified
	/// </summary>
	[Obsolete("Use AddRailwaySteps instead. This method will be removed in a future version.")]
	public static IServiceCollection AddWorkflowSteps(
		this IServiceCollection services,
		IEnumerable<Assembly>? assemblies = null,
		ServiceLifetime lifetime = ServiceLifetime.Scoped)
	{
		return services.AddRailwaySteps(assemblies, lifetime);
	}

	/// <summary>
	/// Adds all workflow steps from the assemblies containing the specified marker types
	/// </summary>
	[Obsolete("Use AddRailwayStepsFromAssembliesContaining instead. This method will be removed in a future version.")]
	public static IServiceCollection AddWorkflowStepsFromAssembliesContaining(
		this IServiceCollection services,
		IEnumerable<Type> markerTypes,
		ServiceLifetime lifetime = ServiceLifetime.Scoped)
	{
		return services.AddRailwayStepsFromAssembliesContaining(markerTypes, lifetime);
	}

	/// <summary>
	/// Adds all workflow steps from the assemblies containing the specified marker types
	/// </summary>
	[Obsolete("Use AddRailwayStepsFromAssembliesContaining instead. This method will be removed in a future version.")]
	public static IServiceCollection AddWorkflowStepsFromAssembliesContaining(
		this IServiceCollection services,
		ServiceLifetime lifetime,
		params Type[] markerTypes)
	{
		return services.AddRailwayStepsFromAssembliesContaining(lifetime, markerTypes);
	}

	/// <summary>
	/// Adds all workflow steps from the assemblies containing the specified marker types
	/// </summary>
	[Obsolete("Use AddRailwayStepsFromAssembliesContaining instead. This method will be removed in a future version.")]
	public static IServiceCollection AddWorkflowStepsFromAssembliesContaining(
		this IServiceCollection services,
		params Type[] markerTypes)
	{
		return services.AddRailwayStepsFromAssembliesContaining(markerTypes);
	}

	/// <summary>
	/// Adds all workflow steps from the assemblies containing the specified marker types
	/// </summary>
	[Obsolete("Use AddRailwayStepsFromAssembliesContaining instead. This method will be removed in a future version.")]
	public static IServiceCollection AddWorkflowStepsFromAssembliesContaining<T1>(
		this IServiceCollection services,
		ServiceLifetime lifetime = ServiceLifetime.Scoped)
	{
		return services.AddRailwayStepsFromAssembliesContaining<T1>(lifetime);
	}

	/// <summary>
	/// Adds all workflow steps from the assemblies containing the specified marker types
	/// </summary>
	[Obsolete("Use AddRailwayStepsFromAssembliesContaining instead. This method will be removed in a future version.")]
	public static IServiceCollection AddWorkflowStepsFromAssembliesContaining<T1, T2>(
		this IServiceCollection services,
		ServiceLifetime lifetime = ServiceLifetime.Scoped)
	{
		return services.AddRailwayStepsFromAssembliesContaining<T1, T2>(lifetime);
	}

	/// <summary>
	/// Adds all workflow steps from the assemblies containing the specified marker types
	/// </summary>
	[Obsolete("Use AddRailwayStepsFromAssembliesContaining instead. This method will be removed in a future version.")]
	public static IServiceCollection AddWorkflowStepsFromAssembliesContaining<T1, T2, T3>(
		this IServiceCollection services,
		ServiceLifetime lifetime = ServiceLifetime.Scoped)
	{
		return services.AddRailwayStepsFromAssembliesContaining<T1, T2, T3>(lifetime);
	}

	/// <summary>
	/// Adds all workflow steps of specific types from the specified assemblies
	/// </summary>
	[Obsolete("Use AddRailwaySteps instead. This method will be removed in a future version.")]
	public static IServiceCollection AddWorkflowSteps<TPayload, TError>(
		this IServiceCollection services,
		IEnumerable<Assembly>? assemblies = null,
		ServiceLifetime lifetime = ServiceLifetime.Scoped)
	{
		return services.AddRailwaySteps<TPayload, TError>(assemblies, lifetime);
	}
}
