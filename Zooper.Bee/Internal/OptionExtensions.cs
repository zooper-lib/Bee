using Zooper.Fox;

namespace Zooper.Bee.Internal;

/// <summary>
/// Provides extension methods for the <see cref="Option{T}"/> class.
/// </summary>
static internal class OptionExtensions
{
	/// <summary>
	/// Creates an Option instance that contains a value (Some).
	/// </summary>
	/// <typeparam name="T">The type of the value.</typeparam>
	/// <param name="value">The value to wrap.</param>
	/// <returns>An Option instance containing the provided value.</returns>
	public static Option<T> Some<T>(this T value)
		=> Option<T>.Some(value);

	/// <summary>
	/// Creates an Option instance that represents no value (None).
	/// </summary>
	/// <typeparam name="T">The type of the option.</typeparam>
	/// <returns>An Option instance representing no value.</returns>
	public static Option<T> None<T>()
		=> Option<T>.None();

	/// <summary>
	/// Converts a nullable value to an Option.
	/// </summary>
	/// <typeparam name="T">The type of the value.</typeparam>
	/// <param name="value">The nullable value to convert.</param>
	/// <returns>An Option containing the value if it's not null, or None if it is null.</returns>
	public static Option<T> ToOption<T>(this T? value) where T : class
		=> value != null ? Option<T>.Some(value) : Option<T>.None();

	/// <summary>
	/// Converts a nullable value type to an Option.
	/// </summary>
	/// <typeparam name="T">The type of the value.</typeparam>
	/// <param name="value">The nullable value to convert.</param>
	/// <returns>An Option containing the value if it has a value, or None if it doesn't.</returns>
	public static Option<T> ToOption<T>(this T? value) where T : struct
		=> value.HasValue ? Option<T>.Some(value.Value) : Option<T>.None();
}