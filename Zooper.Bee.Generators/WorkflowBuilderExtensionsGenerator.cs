using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;

namespace Zooper.Bee.Generators;

/// <summary>
/// Generates extension methods for the WorkflowBuilder class to enable type-safe property operations.
/// </summary>
[Generator]
public class WorkflowBuilderExtensionsGenerator : IIncrementalGenerator
{
    /// <summary>
    /// Initializes the generator with the incremental generation context.
    /// </summary>
    /// <param name="context">The incremental generation context</param>
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        // Generate the extension methods
        context.RegisterPostInitializationOutput(ctx => GenerateExtensions(ctx));
    }

    /// <summary>
    /// Generates the extension methods for the WorkflowBuilder class.
    /// </summary>
    private static void GenerateExtensions(IncrementalGeneratorPostInitializationContext context)
    {
        const string extensionsSource = @"
using System;
using System.Linq.Expressions;
using System.Threading;
using System.Threading.Tasks;
using Zooper.Fox;

namespace Zooper.Bee
{
    /// <summary>
    /// Extension methods for the WorkflowBuilder class that provide type-safe property operations.
    /// </summary>
    public static class WorkflowBuilderExtensions
    {
        /// <summary>
        /// Extracts a property name from an expression.
        /// </summary>
        /// <typeparam name=""TClass"">The class containing the property</typeparam>
        /// <typeparam name=""TProperty"">The type of the property</typeparam>
        /// <param name=""propertySelector"">Expression selecting the property</param>
        /// <returns>The name of the property</returns>
        public static string GetPropertyName<TClass, TProperty>(
            Expression<Func<TClass, TProperty>> propertySelector)
        {
            if (propertySelector.Body is MemberExpression memberExpression)
            {
                return memberExpression.Member.Name;
            }
            
            throw new ArgumentException(""Expression must be a property accessor"", nameof(propertySelector));
        }
        
        /// <summary>
        /// Adds an activity to the workflow with type-safety using a property expression.
        /// </summary>
        /// <typeparam name=""TRequest"">The request type</typeparam>
        /// <typeparam name=""TPayload"">The payload type</typeparam>
        /// <typeparam name=""TSuccess"">The success type</typeparam>
        /// <typeparam name=""TError"">The error type</typeparam>
        /// <typeparam name=""TProperty"">The property type</typeparam>
        /// <param name=""builder"">The workflow builder</param>
        /// <param name=""propertySelector"">Expression selecting the property being modified</param>
        /// <param name=""activity"">The activity function</param>
        /// <returns>The workflow builder for method chaining</returns>
        public static WorkflowBuilder<TRequest, TPayload, TSuccess, TError> Do<TRequest, TPayload, TSuccess, TError, TProperty>(
            this WorkflowBuilder<TRequest, TPayload, TSuccess, TError> builder,
            Expression<Func<TPayload, TProperty>> propertySelector,
            Func<TPayload, CancellationToken, Task<Either<TError, TPayload>>> activity)
        {
            return builder.Do(activity);
        }
        
        /// <summary>
        /// Adds a synchronous activity to the workflow with type-safety using a property expression.
        /// </summary>
        /// <typeparam name=""TRequest"">The request type</typeparam>
        /// <typeparam name=""TPayload"">The payload type</typeparam>
        /// <typeparam name=""TSuccess"">The success type</typeparam>
        /// <typeparam name=""TError"">The error type</typeparam>
        /// <typeparam name=""TProperty"">The property type</typeparam>
        /// <param name=""builder"">The workflow builder</param>
        /// <param name=""propertySelector"">Expression selecting the property being modified</param>
        /// <param name=""activity"">The synchronous activity function</param>
        /// <returns>The workflow builder for method chaining</returns>
        public static WorkflowBuilder<TRequest, TPayload, TSuccess, TError> Do<TRequest, TPayload, TSuccess, TError, TProperty>(
            this WorkflowBuilder<TRequest, TPayload, TSuccess, TError> builder,
            Expression<Func<TPayload, TProperty>> propertySelector,
            Func<TPayload, Either<TError, TPayload>> activity)
        {
            return builder.Do(activity);
        }
        
        /// <summary>
        /// Adds a conditional activity to the workflow with type-safety using a property expression.
        /// </summary>
        /// <typeparam name=""TRequest"">The request type</typeparam>
        /// <typeparam name=""TPayload"">The payload type</typeparam>
        /// <typeparam name=""TSuccess"">The success type</typeparam>
        /// <typeparam name=""TError"">The error type</typeparam>
        /// <typeparam name=""TProperty"">The property type</typeparam>
        /// <param name=""builder"">The workflow builder</param>
        /// <param name=""condition"">The condition to evaluate</param>
        /// <param name=""propertySelector"">Expression selecting the property being modified</param>
        /// <param name=""activity"">The activity function</param>
        /// <returns>The workflow builder for method chaining</returns>
        public static WorkflowBuilder<TRequest, TPayload, TSuccess, TError> DoIf<TRequest, TPayload, TSuccess, TError, TProperty>(
            this WorkflowBuilder<TRequest, TPayload, TSuccess, TError> builder,
            Func<TPayload, bool> condition,
            Expression<Func<TPayload, TProperty>> propertySelector,
            Func<TPayload, CancellationToken, Task<Either<TError, TPayload>>> activity)
        {
            return builder.DoIf(condition, activity);
        }
        
        /// <summary>
        /// Adds a synchronous conditional activity to the workflow with type-safety using a property expression.
        /// </summary>
        /// <typeparam name=""TRequest"">The request type</typeparam>
        /// <typeparam name=""TPayload"">The payload type</typeparam>
        /// <typeparam name=""TSuccess"">The success type</typeparam>
        /// <typeparam name=""TError"">The error type</typeparam>
        /// <typeparam name=""TProperty"">The property type</typeparam>
        /// <param name=""builder"">The workflow builder</param>
        /// <param name=""condition"">The condition to evaluate</param>
        /// <param name=""propertySelector"">Expression selecting the property being modified</param>
        /// <param name=""activity"">The synchronous activity function</param>
        /// <returns>The workflow builder for method chaining</returns>
        public static WorkflowBuilder<TRequest, TPayload, TSuccess, TError> DoIf<TRequest, TPayload, TSuccess, TError, TProperty>(
            this WorkflowBuilder<TRequest, TPayload, TSuccess, TError> builder,
            Func<TPayload, bool> condition,
            Expression<Func<TPayload, TProperty>> propertySelector,
            Func<TPayload, Either<TError, TPayload>> activity)
        {
            return builder.DoIf(condition, activity);
        }
    }
}
";

        context.AddSource("WorkflowBuilderExtensions.g.cs", SourceText.From(extensionsSource, Encoding.UTF8));
    }
}