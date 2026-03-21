using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Zooper.Fox;

namespace Zooper.Bee.Extensions;

/// <summary>
/// Provides extension methods for registering all workflow components with dependency injection.
/// </summary>
[Obsolete("Use RailwayExtensions instead. This class will be removed in a future version.")]
public static class WorkflowExtensions
{
	/// <summary>
	/// Executes a workflow that doesn't require a request parameter.
	/// </summary>
	[Obsolete("Use the Railway Execute extension method instead. This method will be removed in a future version.")]
	public static Task<Either<TError, TSuccess>> Execute<TSuccess, TError>(this Workflow<Unit, TSuccess, TError> workflow)
	{
		return workflow.Execute(Unit.Value);
	}

	/// <summary>
	/// Executes a workflow that doesn't require a request parameter.
	/// </summary>
	[Obsolete("Use the Railway Execute extension method instead. This method will be removed in a future version.")]
	public static Task<Either<TError, TSuccess>> Execute<TSuccess, TError>(
		this Workflow<Unit, TSuccess, TError> workflow,
		CancellationToken cancellationToken)
	{
		return workflow.Execute(Unit.Value, cancellationToken);
	}

	/// <summary>
	/// Registers all workflow components from the specified assemblies into the service collection.
	/// </summary>
	[Obsolete("Use AddRailways instead. This method will be removed in a future version.")]
	public static IServiceCollection AddWorkflows(
		this IServiceCollection services,
		IEnumerable<Assembly>? assemblies = null,
		ServiceLifetime lifetime = ServiceLifetime.Scoped)
	{
		return services.AddRailways(assemblies, lifetime);
	}
}
