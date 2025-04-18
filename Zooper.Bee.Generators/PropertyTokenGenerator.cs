using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;

namespace Zooper.Bee.Generators;

/// <summary>
/// Generates a PropertyToken class for use with type-safe property references.
/// </summary>
[Generator]
public class PropertyTokenGenerator : IIncrementalGenerator
{
	/// <summary>
	/// Initializes the generator with the incremental generation context.
	/// </summary>
	/// <param name="context">The incremental generation context</param>
	public void Initialize(IncrementalGeneratorInitializationContext context)
	{
		// Generate the PropertyToken class in the Zooper.Bee namespace
		context.RegisterPostInitializationOutput(ctx => GeneratePropertyToken(ctx));
	}

	/// <summary>
	/// Generates the PropertyToken class for type-safe property references.
	/// </summary>
	private static void GeneratePropertyToken(IncrementalGeneratorPostInitializationContext context)
	{
		const string propertyTokenSource = @"
using System;
using System.Linq.Expressions;

namespace Zooper.Bee
{
    /// <summary>
    /// Represents a strongly-typed property reference for a specific type.
    /// </summary>
    /// <typeparam name=""TClass"">The class containing the property</typeparam>
    /// <typeparam name=""TProperty"">The type of the property</typeparam>
    public sealed class PropertyToken<TClass, TProperty>
    {
        /// <summary>
        /// The name of the property.
        /// </summary>
        public string Name { get; }

        /// <summary>
        /// Expression to access the property.
        /// </summary>
        public Expression<Func<TClass, TProperty>> Accessor { get; }

        /// <summary>
        /// Creates a new property token.
        /// </summary>
        /// <param name=""name"">The name of the property</param>
        /// <param name=""accessor"">Expression to access the property</param>
        public PropertyToken(string name, Expression<Func<TClass, TProperty>> accessor)
        {
            Name = name;
            Accessor = accessor;
        }

        /// <summary>
        /// Implicitly converts the token to the property name.
        /// </summary>
        public static implicit operator string(PropertyToken<TClass, TProperty> token) => token.Name;
    }
}
";

		context.AddSource("PropertyToken.g.cs", SourceText.From(propertyTokenSource, Encoding.UTF8));
	}
}