using System;
using System.Collections.Generic;
using System.Reflection;
using Microsoft.Extensions.DependencyInjection;

// ReSharper disable MemberCanBePrivate.Global

namespace Zooper.Bee.Extensions;

/// <summary>
/// Extension methods for registering workflow activities with dependency injection
/// </summary>
[Obsolete("Use WorkflowStepsExtensions instead. This class will be removed in a future version.")]
public static class WorkflowActivitiesExtensions
{
	/// <summary>
	/// Adds all workflow activities from the specified assemblies, or from all loaded assemblies if none specified
	/// </summary>
	[Obsolete("Use AddWorkflowSteps instead. This method will be removed in a future version.")]
	public static IServiceCollection AddWorkflowActivities(
		this IServiceCollection services,
		IEnumerable<Assembly>? assemblies = null,
		ServiceLifetime lifetime = ServiceLifetime.Scoped)
	{
		return services.AddWorkflowSteps(assemblies, lifetime);
	}

	/// <summary>
	/// Adds all workflow activities from the assemblies containing the specified marker types
	/// </summary>
	[Obsolete("Use AddWorkflowStepsFromAssembliesContaining instead. This method will be removed in a future version.")]
	public static IServiceCollection AddWorkflowActivitiesFromAssembliesContaining(
		this IServiceCollection services,
		IEnumerable<Type> markerTypes,
		ServiceLifetime lifetime = ServiceLifetime.Scoped)
	{
		return services.AddWorkflowStepsFromAssembliesContaining(markerTypes, lifetime);
	}

	/// <summary>
	/// Adds all workflow activities from the assemblies containing the specified marker types
	/// </summary>
	[Obsolete("Use AddWorkflowStepsFromAssembliesContaining instead. This method will be removed in a future version.")]
	public static IServiceCollection AddWorkflowActivitiesFromAssembliesContaining(
		this IServiceCollection services,
		ServiceLifetime lifetime,
		params Type[] markerTypes)
	{
		return services.AddWorkflowStepsFromAssembliesContaining(lifetime, markerTypes);
	}

	/// <summary>
	/// Adds all workflow activities from the assemblies containing the specified marker types
	/// </summary>
	[Obsolete("Use AddWorkflowStepsFromAssembliesContaining instead. This method will be removed in a future version.")]
	public static IServiceCollection AddWorkflowActivitiesFromAssembliesContaining(
		this IServiceCollection services,
		params Type[] markerTypes)
	{
		return services.AddWorkflowStepsFromAssembliesContaining(markerTypes);
	}

	/// <summary>
	/// Adds all workflow activities from the assemblies containing the specified marker types
	/// </summary>
	[Obsolete("Use AddWorkflowStepsFromAssembliesContaining instead. This method will be removed in a future version.")]
	public static IServiceCollection AddWorkflowActivitiesFromAssembliesContaining<T1>(
		this IServiceCollection services,
		ServiceLifetime lifetime = ServiceLifetime.Scoped)
	{
		return services.AddWorkflowStepsFromAssembliesContaining<T1>(lifetime);
	}

	/// <summary>
	/// Adds all workflow activities from the assemblies containing the specified marker types
	/// </summary>
	[Obsolete("Use AddWorkflowStepsFromAssembliesContaining instead. This method will be removed in a future version.")]
	public static IServiceCollection AddWorkflowActivitiesFromAssembliesContaining<T1, T2>(
		this IServiceCollection services,
		ServiceLifetime lifetime = ServiceLifetime.Scoped)
	{
		return services.AddWorkflowStepsFromAssembliesContaining<T1, T2>(lifetime);
	}

	/// <summary>
	/// Adds all workflow activities from the assemblies containing the specified marker types
	/// </summary>
	[Obsolete("Use AddWorkflowStepsFromAssembliesContaining instead. This method will be removed in a future version.")]
	public static IServiceCollection AddWorkflowActivitiesFromAssembliesContaining<T1, T2, T3>(
		this IServiceCollection services,
		ServiceLifetime lifetime = ServiceLifetime.Scoped)
	{
		return services.AddWorkflowStepsFromAssembliesContaining<T1, T2, T3>(lifetime);
	}

	/// <summary>
	/// Adds all workflow activities of specific types from the specified assemblies, or from all loaded assemblies if none specified
	/// </summary>
	[Obsolete("Use AddWorkflowSteps instead. This method will be removed in a future version.")]
	public static IServiceCollection AddWorkflowActivities<TPayload, TError>(
		this IServiceCollection services,
		IEnumerable<Assembly>? assemblies = null,
		ServiceLifetime lifetime = ServiceLifetime.Scoped)
	{
		return services.AddWorkflowSteps<TPayload, TError>(assemblies, lifetime);
	}
}
