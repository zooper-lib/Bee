using System;
using System.Collections.Generic;
using System.Reflection;
using Microsoft.Extensions.DependencyInjection;

// ReSharper disable MemberCanBePrivate.Global

namespace Zooper.Bee.Extensions;

/// <summary>
/// Extension methods for registering workflow activities with dependency injection
/// </summary>
[Obsolete("Use RailwayStepsExtensions instead. This class will be removed in a future version.")]
public static class WorkflowActivitiesExtensions
{
	/// <summary>
	/// Adds all workflow activities from the specified assemblies, or from all loaded assemblies if none specified
	/// </summary>
	[Obsolete("Use AddRailwaySteps instead. This method will be removed in a future version.")]
	public static IServiceCollection AddWorkflowActivities(
		this IServiceCollection services,
		IEnumerable<Assembly>? assemblies = null,
		ServiceLifetime lifetime = ServiceLifetime.Scoped)
	{
		return services.AddRailwaySteps(assemblies, lifetime);
	}

	[Obsolete("Use AddRailwayStepsFromAssembliesContaining instead. This method will be removed in a future version.")]
	public static IServiceCollection AddWorkflowActivitiesFromAssembliesContaining(
		this IServiceCollection services,
		IEnumerable<Type> markerTypes,
		ServiceLifetime lifetime = ServiceLifetime.Scoped)
	{
		return services.AddRailwayStepsFromAssembliesContaining(markerTypes, lifetime);
	}

	[Obsolete("Use AddRailwayStepsFromAssembliesContaining instead. This method will be removed in a future version.")]
	public static IServiceCollection AddWorkflowActivitiesFromAssembliesContaining(
		this IServiceCollection services,
		ServiceLifetime lifetime,
		params Type[] markerTypes)
	{
		return services.AddRailwayStepsFromAssembliesContaining(lifetime, markerTypes);
	}

	[Obsolete("Use AddRailwayStepsFromAssembliesContaining instead. This method will be removed in a future version.")]
	public static IServiceCollection AddWorkflowActivitiesFromAssembliesContaining(
		this IServiceCollection services,
		params Type[] markerTypes)
	{
		return services.AddRailwayStepsFromAssembliesContaining(markerTypes);
	}

	[Obsolete("Use AddRailwayStepsFromAssembliesContaining instead. This method will be removed in a future version.")]
	public static IServiceCollection AddWorkflowActivitiesFromAssembliesContaining<T1>(
		this IServiceCollection services,
		ServiceLifetime lifetime = ServiceLifetime.Scoped)
	{
		return services.AddRailwayStepsFromAssembliesContaining<T1>(lifetime);
	}

	[Obsolete("Use AddRailwayStepsFromAssembliesContaining instead. This method will be removed in a future version.")]
	public static IServiceCollection AddWorkflowActivitiesFromAssembliesContaining<T1, T2>(
		this IServiceCollection services,
		ServiceLifetime lifetime = ServiceLifetime.Scoped)
	{
		return services.AddRailwayStepsFromAssembliesContaining<T1, T2>(lifetime);
	}

	[Obsolete("Use AddRailwayStepsFromAssembliesContaining instead. This method will be removed in a future version.")]
	public static IServiceCollection AddWorkflowActivitiesFromAssembliesContaining<T1, T2, T3>(
		this IServiceCollection services,
		ServiceLifetime lifetime = ServiceLifetime.Scoped)
	{
		return services.AddRailwayStepsFromAssembliesContaining<T1, T2, T3>(lifetime);
	}

	[Obsolete("Use AddRailwaySteps instead. This method will be removed in a future version.")]
	public static IServiceCollection AddWorkflowActivities<TPayload, TError>(
		this IServiceCollection services,
		IEnumerable<Assembly>? assemblies = null,
		ServiceLifetime lifetime = ServiceLifetime.Scoped)
	{
		return services.AddRailwaySteps<TPayload, TError>(assemblies, lifetime);
	}
}
