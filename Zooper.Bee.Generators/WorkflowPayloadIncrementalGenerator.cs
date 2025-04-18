using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using Zooper.Bee.Generators.Models;

namespace Zooper.Bee.Generators;

/// <summary>
/// Generates extension methods and validation code for workflow payloads.
/// </summary>
[Generator]
public class WorkflowPayloadIncrementalGenerator : IIncrementalGenerator
{
	/// <summary>
	/// Initializes the generator with the incremental generation context.
	/// </summary>
	/// <param name="context">The incremental generation context</param>
	public void Initialize(IncrementalGeneratorInitializationContext context)
	{
		// Register the attribute source
		context.RegisterPostInitializationOutput(ctx => RegisterAttributes(ctx));

		// Create a pipeline for workflow payload classes
		var payloads = context.SyntaxProvider
			.CreateSyntaxProvider(
				predicate: static (node, _) => IsPayloadCandidate(node),
				transform: static (ctx, _) => GetPayloadInfo(ctx))
			.Where(static m => m is not null);

		// Register the source output
		context.RegisterSourceOutput(payloads,
			static (spc, payload) => GenerateExtensions(spc, payload!));
	}

	/// <summary>
	/// Register the attributes used for source generation.
	/// </summary>
	private static void RegisterAttributes(IncrementalGeneratorPostInitializationContext context)
	{
		// Workflow payload attribute source
		const string workflowPayloadAttributeSource = @"
using System;

namespace Zooper.Bee.Generators
{
    [AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = false)]
    public sealed class WorkflowPayloadAttribute : Attribute
    {
    }
}
";

		// Workflow property attribute source
		const string workflowPropertyAttributeSource = @"
using System;

namespace Zooper.Bee.Generators
{
    [AttributeUsage(AttributeTargets.Property, Inherited = false, AllowMultiple = false)]
    public sealed class WorkflowPropertyAttribute : Attribute
    {
        public string[] DependsOn { get; set; } = Array.Empty<string>();
        public string Description { get; set; } = string.Empty;
    }
}
";

		// Add both attributes to the compilation
		context.AddSource("WorkflowPayloadAttribute.g.cs", SourceText.From(workflowPayloadAttributeSource, Encoding.UTF8));
		context.AddSource("WorkflowPropertyAttribute.g.cs", SourceText.From(workflowPropertyAttributeSource, Encoding.UTF8));
	}

	/// <summary>
	/// Check if a syntax node is a candidate for payload processing.
	/// </summary>
	private static bool IsPayloadCandidate(SyntaxNode node)
	{
		// Look for record or class declarations that might have our attribute
		return node is RecordDeclarationSyntax { AttributeLists.Count: > 0 } ||
			   node is ClassDeclarationSyntax { AttributeLists.Count: > 0 };
	}

	/// <summary>
	/// Extract payload information from a syntax node.
	/// </summary>
	private static WorkflowPayloadInfo? GetPayloadInfo(GeneratorSyntaxContext context)
	{
		// Get the declaration syntax
		var declarationSyntax = (TypeDeclarationSyntax)context.Node;

		// Get the semantic model
		var semanticModel = context.SemanticModel;

		// Get the symbol for the class
		var symbol = semanticModel.GetDeclaredSymbol(declarationSyntax) as INamedTypeSymbol;
		if (symbol == null)
			return null;

		// Check if it has our attribute
		var hasAttribute = symbol.GetAttributes()
			.Any(attr => attr.AttributeClass?.ToDisplayString() == "Zooper.Bee.Generators.WorkflowPayloadAttribute");

		if (!hasAttribute)
			return null;

		// Create the payload info
		var payload = new WorkflowPayloadInfo
		{
			Name = symbol.Name,
			Namespace = symbol.ContainingNamespace.ToDisplayString(),
			IsPartial = declarationSyntax.Modifiers.Any(m => m.IsKind(SyntaxKind.PartialKeyword))
		};

		// Add type parameters if generic
		if (symbol.IsGenericType)
		{
			foreach (var typeParameter in symbol.TypeParameters)
			{
				payload.TypeParameters.Add(typeParameter.Name);
			}
		}

		// Process all properties
		foreach (var member in symbol.GetMembers().OfType<IPropertySymbol>())
		{
			// Skip non-writable properties
			if (member.IsReadOnly || !member.IsVirtual && !member.IsOverride && !member.DeclaringSyntaxReferences.Any())
				continue;

			// Create property info
			var property = new PropertyDependencyInfo
			{
				Name = member.Name,
				TypeName = member.Type.ToDisplayString(),
				Declaration = $"{member.Type.ToDisplayString()} {member.Name}"
			};

			// Check for the WorkflowProperty attribute
			var propAttribute = member.GetAttributes()
				.FirstOrDefault(attr => attr.AttributeClass?.ToDisplayString() == "Zooper.Bee.Generators.WorkflowPropertyAttribute");

			if (propAttribute != null)
			{
				// Extract dependencies
				var dependsOnArg = propAttribute.NamedArguments
					.FirstOrDefault(arg => arg.Key == "DependsOn").Value;

				if (dependsOnArg.Value is ImmutableArray<TypedConstant> dependsOn)
				{
					foreach (var dep in dependsOn)
					{
						if (dep.Value is string depName)
						{
							property.Dependencies.Add(depName);
						}
					}
				}

				// Extract description
				var descriptionArg = propAttribute.NamedArguments
					.FirstOrDefault(arg => arg.Key == "Description").Value;

				if (descriptionArg.Value is string description)
				{
					property.Description = description;
				}
			}

			payload.Properties.Add(property);
		}

		return payload;
	}

	/// <summary>
	/// Generate the source code for payload extensions.
	/// </summary>
	private static void GenerateExtensions(SourceProductionContext context, WorkflowPayloadInfo payload)
	{
		if (!payload.IsPartial)
		{
			// Report a diagnostic that the class must be partial
			var diagnostic = Diagnostic.Create(
				new DiagnosticDescriptor(
					id: "ZOOPERBEE001",
					title: "Workflow payload class must be partial",
					messageFormat: "The workflow payload class '{0}' must be declared as partial",
					category: "WorkflowPayloadGenerator",
					defaultSeverity: DiagnosticSeverity.Error,
					isEnabledByDefault: true),
				location: null,
				payload.Name);

			context.ReportDiagnostic(diagnostic);
			return;
		}

		var code = GeneratePayloadExtensions(payload);
		context.AddSource($"{payload.Name}.g.cs", SourceText.From(code, Encoding.UTF8));
	}

	/// <summary>
	/// Generate the extension class for a workflow payload.
	/// </summary>
	private static string GeneratePayloadExtensions(WorkflowPayloadInfo payload)
	{
		var sb = new StringBuilder();

		// Add the namespace and usings
		sb.AppendLine("using System;");
		sb.AppendLine("using System.Linq.Expressions;");
		sb.AppendLine("using System.Reflection;");
		sb.AppendLine("using System.Threading;");
		sb.AppendLine("using System.Threading.Tasks;");
		sb.AppendLine("using Zooper.Bee;");
		sb.AppendLine("using Zooper.Fox;");
		sb.AppendLine();

		// Open namespace
		sb.AppendLine($"namespace {payload.Namespace}");
		sb.AppendLine("{");

		// Generate partial class implementation
		GeneratePartialClass(sb, payload);

		// Generate extension methods class
		GenerateExtensionMethods(sb, payload);

		// Close namespace
		sb.AppendLine("}");

		return sb.ToString();
	}

	/// <summary>
	/// Generate the partial class implementation.
	/// </summary>
	private static void GeneratePartialClass(StringBuilder sb, WorkflowPayloadInfo payload)
	{
		// Start the partial class declaration
		sb.Append($"    public partial {(payload.Name.Contains("record") ? "record" : "class")} {payload.Name}");

		// Add type parameters if generic
		if (payload.IsGeneric)
		{
			sb.Append($"<{string.Join(", ", payload.TypeParameters)}>");
		}

		sb.AppendLine();
		sb.AppendLine("    {");

		// Generate the property state checking methods
		foreach (var property in payload.Properties)
		{
			// Generate XML documentation
			sb.AppendLine($"        /// <summary>");
			sb.AppendLine($"        /// Checks if the '{property.Name}' property has been set to a non-default value.");
			sb.AppendLine($"        /// </summary>");
			sb.AppendLine($"        /// <returns>True if the property has been set, otherwise false.</returns>");

			// Generate the Has* property
			sb.AppendLine($"        public bool Has{property.Name} => {property.Name} != default;");
			sb.AppendLine();
		}

		// Generate the validation methods for each property with dependencies
		foreach (var property in payload.Properties.Where(p => p.HasDependencies))
		{
			// Generate XML documentation
			sb.AppendLine($"        /// <summary>");
			sb.AppendLine($"        /// Validates that all dependencies for the '{property.Name}' property are satisfied.");
			sb.AppendLine($"        /// </summary>");
			sb.AppendLine($"        /// <typeparam name=\"TError\">The type of error to return if validation fails.</typeparam>");
			sb.AppendLine($"        /// <param name=\"createError\">Function to create an error when a dependency is not satisfied.</param>");
			sb.AppendLine($"        /// <returns>Either this payload if valid, or an error if dependencies are not met.</returns>");

			// Generate the validation method
			sb.AppendLine($"        public Either<TError, {payload.FullTypeName}> ValidateFor{property.Name}<TError>(");
			sb.AppendLine($"            Func<string, string, TError> createError)");
			sb.AppendLine("        {");

			// Check each dependency
			foreach (var dependency in property.Dependencies)
			{
				sb.AppendLine($"            if (!Has{dependency})");
				sb.AppendLine($"                return Either<TError, {payload.FullTypeName}>.FromLeft(");
				sb.AppendLine($"                    createError(\"{property.Name}\", \"{dependency}\"));");
				sb.AppendLine();
			}

			// Return the valid result
			sb.AppendLine($"            return Either<TError, {payload.FullTypeName}>.FromRight(this);");
			sb.AppendLine("        }");
			sb.AppendLine();
		}

		// Add Properties collection for reflection-free property access
		sb.AppendLine("        /// <summary>");
		sb.AppendLine("        /// Static property tokens for type-safe property references.");
		sb.AppendLine("        /// </summary>");
		sb.AppendLine("        public static class Properties");
		sb.AppendLine("        {");

		foreach (var property in payload.Properties)
		{
			sb.AppendLine($"            /// <summary>Token for the {property.Name} property.</summary>");
			sb.AppendLine($"            public static readonly PropertyToken<{payload.FullTypeName}, {property.TypeName}> {property.Name} = ");
			sb.AppendLine($"                new(nameof({property.Name}), p => p.{property.Name});");
		}

		sb.AppendLine("        }");

		// Close the class
		sb.AppendLine("    }");
		sb.AppendLine();

		// Generate the PropertyToken class if not already provided
		sb.AppendLine("    /// <summary>");
		sb.AppendLine("    /// Represents a strongly-typed property reference for a specific type.");
		sb.AppendLine("    /// </summary>");
		sb.AppendLine("    /// <typeparam name=\"TClass\">The class containing the property</typeparam>");
		sb.AppendLine("    /// <typeparam name=\"TProperty\">The type of the property</typeparam>");
		sb.AppendLine("    public sealed class PropertyToken<TClass, TProperty>");
		sb.AppendLine("    {");
		sb.AppendLine("        /// <summary>");
		sb.AppendLine("        /// The name of the property.");
		sb.AppendLine("        /// </summary>");
		sb.AppendLine("        public string Name { get; }");
		sb.AppendLine();
		sb.AppendLine("        /// <summary>");
		sb.AppendLine("        /// Expression to access the property.");
		sb.AppendLine("        /// </summary>");
		sb.AppendLine("        public Expression<Func<TClass, TProperty>> Accessor { get; }");
		sb.AppendLine();
		sb.AppendLine("        /// <summary>");
		sb.AppendLine("        /// Creates a new property token.");
		sb.AppendLine("        /// </summary>");
		sb.AppendLine("        /// <param name=\"name\">The name of the property</param>");
		sb.AppendLine("        /// <param name=\"accessor\">Expression to access the property</param>");
		sb.AppendLine("        public PropertyToken(string name, Expression<Func<TClass, TProperty>> accessor)");
		sb.AppendLine("        {");
		sb.AppendLine("            Name = name;");
		sb.AppendLine("            Accessor = accessor;");
		sb.AppendLine("        }");
		sb.AppendLine();
		sb.AppendLine("        /// <summary>");
		sb.AppendLine("        /// Implicitly converts the token to the property name.");
		sb.AppendLine("        /// </summary>");
		sb.AppendLine("        public static implicit operator string(PropertyToken<TClass, TProperty> token) => token.Name;");
		sb.AppendLine("    }");
	}

	/// <summary>
	/// Generate extension methods for the workflow builder.
	/// </summary>
	private static void GenerateExtensionMethods(StringBuilder sb, WorkflowPayloadInfo payload)
	{
		// Add the extension methods class
		sb.AppendLine();
		sb.AppendLine($"    /// <summary>");
		sb.AppendLine($"    /// Extension methods for type-safe workflow operations with {payload.Name}.");
		sb.AppendLine($"    /// </summary>");
		sb.AppendLine($"    public static class {payload.Name}Extensions");
		sb.AppendLine("    {");

		// First, generate the dependency checking wrapper
		sb.AppendLine("        /// <summary>");
		sb.AppendLine("        /// Wraps an activity with automatic dependency checking.");
		sb.AppendLine("        /// </summary>");
		sb.AppendLine("        /// <typeparam name=\"TError\">The error type</typeparam>");
		sb.AppendLine("        /// <typeparam name=\"TProperty\">The property type</typeparam>");
		sb.AppendLine("        /// <param name=\"activity\">The activity function to wrap</param>");
		sb.AppendLine("        /// <param name=\"property\">Expression selecting the property being set</param>");
		sb.AppendLine("        /// <returns>A wrapped activity that performs dependency checking</returns>");
		sb.AppendLine($"        public static Func<{payload.FullTypeName}, CancellationToken, Task<Either<TError, {payload.FullTypeName}>>>");
		sb.AppendLine($"            WithDependencyChecking<TError, TProperty>(");
		sb.AppendLine($"                this Func<{payload.FullTypeName}, CancellationToken, Task<Either<TError, {payload.FullTypeName}>>> activity,");
		sb.AppendLine($"                Expression<Func<{payload.FullTypeName}, TProperty>> property)");
		sb.AppendLine("        {");
		sb.AppendLine("            // Extract property name from the expression");
		sb.AppendLine("            string propertyName = ((MemberExpression)property.Body).Member.Name;");
		sb.AppendLine();
		sb.AppendLine("            return async (payload, cancellationToken) =>");
		sb.AppendLine("            {");
		sb.AppendLine("                // Use reflection to find the validation method if it exists");
		sb.AppendLine($"                var validationMethod = typeof({payload.FullTypeName}).GetMethod($\"ValidateFor{{propertyName}}\",");
		sb.AppendLine("                    BindingFlags.Public | BindingFlags.Instance)");
		sb.AppendLine("                    ?.MakeGenericMethod(typeof(TError));");
		sb.AppendLine();
		sb.AppendLine("                if (validationMethod != null)");
		sb.AppendLine("                {");
		sb.AppendLine("                    try");
		sb.AppendLine("                    {");
		sb.AppendLine("                        // Create error factory");
		sb.AppendLine("                        Func<string, string, TError> createError = (prop, dep) =>");
		sb.AppendLine("                            (TError)Activator.CreateInstance(typeof(TError), ");
		sb.AppendLine("                                \"DEPENDENCY_ERROR\", ");
		sb.AppendLine("                                $\"{prop} depends on {dep} which has not been set\");");
		sb.AppendLine();
		sb.AppendLine("                        // Invoke the validation method");
		sb.AppendLine($"                        var validationResult = (Either<TError, {payload.FullTypeName}>)validationMethod");
		sb.AppendLine("                            .Invoke(payload, new object[] { createError });");
		sb.AppendLine();
		sb.AppendLine("                        // Return error if validation failed");
		sb.AppendLine("                        if (validationResult.IsLeft)");
		sb.AppendLine("                            return validationResult;");
		sb.AppendLine("                    }");
		sb.AppendLine("                    catch (Exception ex)");
		sb.AppendLine("                    {");
		sb.AppendLine("                        // Handle any reflection exceptions");
		sb.AppendLine($"                        return Either<TError, {payload.FullTypeName}>.FromLeft(");
		sb.AppendLine("                            (TError)Activator.CreateInstance(typeof(TError), ");
		sb.AppendLine("                                \"VALIDATION_ERROR\", ");
		sb.AppendLine("                                $\"Error validating {propertyName}: {ex.Message}\"));");
		sb.AppendLine("                    }");
		sb.AppendLine("                }");
		sb.AppendLine();
		sb.AppendLine("                // If validation passes or there's no validation method, execute the activity");
		sb.AppendLine("                return await activity(payload, cancellationToken);");
		sb.AppendLine("            };");
		sb.AppendLine("        }");
		sb.AppendLine();

		// Generate extension method for WorkflowBuilder - async version
		sb.AppendLine("        /// <summary>");
		sb.AppendLine("        /// Adds an activity to the workflow with automatic dependency checking.");
		sb.AppendLine("        /// </summary>");
		sb.AppendLine("        /// <typeparam name=\"TRequest\">The request type</typeparam>");
		sb.AppendLine("        /// <typeparam name=\"TSuccess\">The success type</typeparam>");
		sb.AppendLine("        /// <typeparam name=\"TError\">The error type</typeparam>");
		sb.AppendLine("        /// <typeparam name=\"TProperty\">The property type</typeparam>");
		sb.AppendLine("        /// <param name=\"builder\">The workflow builder</param>");
		sb.AppendLine("        /// <param name=\"property\">Expression selecting the property being modified</param>");
		sb.AppendLine("        /// <param name=\"activity\">The activity function</param>");
		sb.AppendLine("        /// <returns>The workflow builder for method chaining</returns>");
		sb.AppendLine($"        public static WorkflowBuilder<TRequest, {payload.FullTypeName}, TSuccess, TError> Do<TRequest, TSuccess, TError, TProperty>(");
		sb.AppendLine($"            this WorkflowBuilder<TRequest, {payload.FullTypeName}, TSuccess, TError> builder,");
		sb.AppendLine($"            Expression<Func<{payload.FullTypeName}, TProperty>> property,");
		sb.AppendLine($"            Func<{payload.FullTypeName}, CancellationToken, Task<Either<TError, {payload.FullTypeName}>>> activity)");
		sb.AppendLine("        {");
		sb.AppendLine("            // Wrap with dependency checking");
		sb.AppendLine("            var wrappedActivity = activity.WithDependencyChecking(property);");
		sb.AppendLine();
		sb.AppendLine("            // Add to workflow");
		sb.AppendLine("            return builder.Do(wrappedActivity);");
		sb.AppendLine("        }");
		sb.AppendLine();

		// Generate extension method for WorkflowBuilder - sync version
		sb.AppendLine("        /// <summary>");
		sb.AppendLine("        /// Adds a synchronous activity to the workflow with automatic dependency checking.");
		sb.AppendLine("        /// </summary>");
		sb.AppendLine("        /// <typeparam name=\"TRequest\">The request type</typeparam>");
		sb.AppendLine("        /// <typeparam name=\"TSuccess\">The success type</typeparam>");
		sb.AppendLine("        /// <typeparam name=\"TError\">The error type</typeparam>");
		sb.AppendLine("        /// <typeparam name=\"TProperty\">The property type</typeparam>");
		sb.AppendLine("        /// <param name=\"builder\">The workflow builder</param>");
		sb.AppendLine("        /// <param name=\"property\">Expression selecting the property being modified</param>");
		sb.AppendLine("        /// <param name=\"activity\">The synchronous activity function</param>");
		sb.AppendLine("        /// <returns>The workflow builder for method chaining</returns>");
		sb.AppendLine($"        public static WorkflowBuilder<TRequest, {payload.FullTypeName}, TSuccess, TError> Do<TRequest, TSuccess, TError, TProperty>(");
		sb.AppendLine($"            this WorkflowBuilder<TRequest, {payload.FullTypeName}, TSuccess, TError> builder,");
		sb.AppendLine($"            Expression<Func<{payload.FullTypeName}, TProperty>> property,");
		sb.AppendLine($"            Func<{payload.FullTypeName}, Either<TError, {payload.FullTypeName}>> activity)");
		sb.AppendLine("        {");
		sb.AppendLine("            // Create async version of the activity");
		sb.AppendLine($"            Func<{payload.FullTypeName}, CancellationToken, Task<Either<TError, {payload.FullTypeName}>>> asyncActivity =");
		sb.AppendLine("                (payload, _) => Task.FromResult(activity(payload));");
		sb.AppendLine();
		sb.AppendLine("            // Add to workflow with dependency checking");
		sb.AppendLine("            return builder.Do(asyncActivity.WithDependencyChecking(property));");
		sb.AppendLine("        }");
		sb.AppendLine();

		// Generate extension method for conditional activities - async version
		sb.AppendLine("        /// <summary>");
		sb.AppendLine("        /// Adds a conditional activity to the workflow with automatic dependency checking.");
		sb.AppendLine("        /// </summary>");
		sb.AppendLine("        /// <typeparam name=\"TRequest\">The request type</typeparam>");
		sb.AppendLine("        /// <typeparam name=\"TSuccess\">The success type</typeparam>");
		sb.AppendLine("        /// <typeparam name=\"TError\">The error type</typeparam>");
		sb.AppendLine("        /// <typeparam name=\"TProperty\">The property type</typeparam>");
		sb.AppendLine("        /// <param name=\"builder\">The workflow builder</param>");
		sb.AppendLine("        /// <param name=\"condition\">The condition to evaluate</param>");
		sb.AppendLine("        /// <param name=\"property\">Expression selecting the property being modified</param>");
		sb.AppendLine("        /// <param name=\"activity\">The activity function</param>");
		sb.AppendLine("        /// <returns>The workflow builder for method chaining</returns>");
		sb.AppendLine($"        public static WorkflowBuilder<TRequest, {payload.FullTypeName}, TSuccess, TError> DoIf<TRequest, TSuccess, TError, TProperty>(");
		sb.AppendLine($"            this WorkflowBuilder<TRequest, {payload.FullTypeName}, TSuccess, TError> builder,");
		sb.AppendLine($"            Func<{payload.FullTypeName}, bool> condition,");
		sb.AppendLine($"            Expression<Func<{payload.FullTypeName}, TProperty>> property,");
		sb.AppendLine($"            Func<{payload.FullTypeName}, CancellationToken, Task<Either<TError, {payload.FullTypeName}>>> activity)");
		sb.AppendLine("        {");
		sb.AppendLine("            // Wrap with dependency checking");
		sb.AppendLine("            var wrappedActivity = activity.WithDependencyChecking(property);");
		sb.AppendLine();
		sb.AppendLine("            // Add to workflow");
		sb.AppendLine("            return builder.DoIf(condition, wrappedActivity);");
		sb.AppendLine("        }");
		sb.AppendLine();

		// Generate extension method for conditional activities - sync version
		sb.AppendLine("        /// <summary>");
		sb.AppendLine("        /// Adds a synchronous conditional activity to the workflow with automatic dependency checking.");
		sb.AppendLine("        /// </summary>");
		sb.AppendLine("        /// <typeparam name=\"TRequest\">The request type</typeparam>");
		sb.AppendLine("        /// <typeparam name=\"TSuccess\">The success type</typeparam>");
		sb.AppendLine("        /// <typeparam name=\"TError\">The error type</typeparam>");
		sb.AppendLine("        /// <typeparam name=\"TProperty\">The property type</typeparam>");
		sb.AppendLine("        /// <param name=\"builder\">The workflow builder</param>");
		sb.AppendLine("        /// <param name=\"condition\">The condition to evaluate</param>");
		sb.AppendLine("        /// <param name=\"property\">Expression selecting the property being modified</param>");
		sb.AppendLine("        /// <param name=\"activity\">The synchronous activity function</param>");
		sb.AppendLine("        /// <returns>The workflow builder for method chaining</returns>");
		sb.AppendLine($"        public static WorkflowBuilder<TRequest, {payload.FullTypeName}, TSuccess, TError> DoIf<TRequest, TSuccess, TError, TProperty>(");
		sb.AppendLine($"            this WorkflowBuilder<TRequest, {payload.FullTypeName}, TSuccess, TError> builder,");
		sb.AppendLine($"            Func<{payload.FullTypeName}, bool> condition,");
		sb.AppendLine($"            Expression<Func<{payload.FullTypeName}, TProperty>> property,");
		sb.AppendLine($"            Func<{payload.FullTypeName}, Either<TError, {payload.FullTypeName}>> activity)");
		sb.AppendLine("        {");
		sb.AppendLine("            // Create async version of the activity");
		sb.AppendLine($"            Func<{payload.FullTypeName}, CancellationToken, Task<Either<TError, {payload.FullTypeName}>>> asyncActivity =");
		sb.AppendLine("                (payload, _) => Task.FromResult(activity(payload));");
		sb.AppendLine();
		sb.AppendLine("            // Add to workflow with dependency checking");
		sb.AppendLine("            return builder.DoIf(condition, asyncActivity.WithDependencyChecking(property));");
		sb.AppendLine("        }");

		// Close the extension class
		sb.AppendLine("    }");
	}
}