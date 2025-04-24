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
/// Provides extension methods for registering all workflow components (validations, activities, and workflow classes)
/// with dependency injection. These methods simplify the configuration process by centralizing the registration
/// of all workflow-related services.
/// </summary>
public static class WorkflowExtensions
{
	/// <summary>
	/// Executes a workflow that doesn't require a request parameter.
	/// </summary>
	/// <typeparam name="TSuccess">The type of the success result</typeparam>
	/// <typeparam name="TError">The type of the error result</typeparam>
	/// <param name="workflow">The workflow to execute</param>
	/// <returns>The result of the workflow execution</returns>
	public static Task<Either<TError, TSuccess>> Execute<TSuccess, TError>(this Workflow<Unit, TSuccess, TError> workflow)
	{
		return workflow.Execute(Unit.Value);
	}

	/// <summary>
	/// Executes a workflow that doesn't require a request parameter.
	/// </summary>
	/// <typeparam name="TSuccess">The type of the success result</typeparam>
	/// <typeparam name="TError">The type of the error result</typeparam>
	/// <param name="workflow">The workflow to execute</param>
	/// <param name="cancellationToken">A cancellation token to observe while waiting for the task to complete</param>
	/// <returns>The result of the workflow execution</returns>
	public static Task<Either<TError, TSuccess>> Execute<TSuccess, TError>(
		this Workflow<Unit, TSuccess, TError> workflow,
		CancellationToken cancellationToken)
	{
		return workflow.Execute(Unit.Value, cancellationToken);
	}

	/// <summary>
	/// Registers all workflow components from the specified assemblies into the service collection.
	/// This includes workflow validations, workflow activities, and concrete workflow classes.
	/// </summary>
	/// <param name="services">The service collection to add the registrations to</param>
	/// <param name="assemblies">Optional list of assemblies to scan for workflow components. If null or empty, all non-system 
	/// assemblies in the current AppDomain will be scanned</param>
	/// <param name="lifetime">The service lifetime to use for the registered services (defaults to Scoped)</param>
	/// <returns>The service collection for chaining additional registrations</returns>
	/// <remarks>
	/// This method provides a comprehensive registration of all workflow-related components:
	/// - Workflow validations (via AddWorkflowValidations)
	/// - Workflow activities (via AddWorkflowActivities)
	/// - Concrete workflow classes (classes ending with "Workflow")
	/// 
	/// Workflow classes are registered as themselves (not by interface) to support direct injection.
	/// System and Microsoft assemblies are excluded by default when no specific assemblies are provided.
	/// </remarks>
	public static IServiceCollection AddWorkflows(
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

		// Register all workflow validations
		services.AddWorkflowValidations(assembliesToScan, lifetime);

		// Register all workflow activities
		services.AddWorkflowActivities(assembliesToScan, lifetime);

		// Then register all classes ending with Workflow
		services.Scan(scan => scan
			.FromAssemblies(assembliesToScan)
			.AddClasses(classes =>
				classes.Where(type => type.Name.EndsWith("Workflow") && type is { IsAbstract: false, IsInterface: false })
			)
			.AsSelf()
			.WithLifetime(lifetime)
		);

		return services;
	}
}