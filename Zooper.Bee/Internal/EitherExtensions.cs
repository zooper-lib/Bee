using Zooper.Fox;

namespace Zooper.Bee.Internal;

/// <summary>
/// Provides extension methods for the <see cref="Either{TLeft, TRight}"/> class.
/// </summary>
static internal class EitherExtensions
{
	/// <summary>
	/// Creates a new Either instance representing a successful result (Right value).
	/// </summary>
	/// <typeparam name="TError">The type of the error (Left value).</typeparam>
	/// <typeparam name="TSuccess">The type of the success result (Right value).</typeparam>
	/// <param name="success">The success value.</param>
	/// <returns>A new Either instance with the success value in the Right position.</returns>
	public static Either<TError, TSuccess> Success<TError, TSuccess>(this Either<TError, TSuccess> _, TSuccess success)
		=> Either<TError, TSuccess>.FromRight(success);

	/// <summary>
	/// Creates a new Either instance representing a failure result (Left value).
	/// </summary>
	/// <typeparam name="TError">The type of the error (Left value).</typeparam>
	/// <typeparam name="TSuccess">The type of the success result (Right value).</typeparam>
	/// <param name="error">The error value.</param>
	/// <returns>A new Either instance with the error value in the Left position.</returns>
	public static Either<TError, TSuccess> Fail<TError, TSuccess>(this Either<TError, TSuccess> _, TError error)
		=> Either<TError, TSuccess>.FromLeft(error);

	/// <summary>
	/// Gets a value indicating whether this Either represents a success result (contains a Right value).
	/// </summary>
	/// <typeparam name="TLeft">The type of the Left value.</typeparam>
	/// <typeparam name="TRight">The type of the Right value.</typeparam>
	/// <param name="either">The Either instance to check.</param>
	/// <returns>true if this Either represents a success result; otherwise, false.</returns>
	public static bool IsSuccess<TLeft, TRight>(this Either<TLeft, TRight> either)
		=> either.IsRight;

	/// <summary>
	/// Gets the success value if this Either represents a success result, or
	/// throws an exception if it represents a failure.
	/// </summary>
	/// <typeparam name="TLeft">The type of the Left value.</typeparam>
	/// <typeparam name="TRight">The type of the Right value.</typeparam>
	/// <param name="either">The Either instance to extract the value from.</param>
	/// <returns>The Right value if this Either represents a success result.</returns>
	/// <exception cref="InvalidOperationException">
	/// Thrown if this Either represents a failure result.
	/// </exception>
	public static TRight Value<TLeft, TRight>(this Either<TLeft, TRight> either)
		=> either.Right;

	/// <summary>
	/// Gets the error value if this Either represents a failure result, or
	/// throws an exception if it represents a success.
	/// </summary>
	/// <typeparam name="TLeft">The type of the Left value.</typeparam>
	/// <typeparam name="TRight">The type of the Right value.</typeparam>
	/// <param name="either">The Either instance to extract the error from.</param>
	/// <returns>The Left value if this Either represents a failure result.</returns>
	/// <exception cref="InvalidOperationException">
	/// Thrown if this Either represents a success result.
	/// </exception>
	public static TLeft Error<TLeft, TRight>(this Either<TLeft, TRight> either)
		=> either.Left;
}