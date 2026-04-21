using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Zooper.Fox;

// ReSharper disable MemberCanBePrivate.Global

namespace Zooper.Bee;

/// <summary>
/// Builder for a conditional sub-pipeline used by
/// <see cref="RailwayStepsBuilder{TRequest,TPayload,TSuccess,TError}.Branch"/>.
/// Exposes <c>Do</c>, <c>Tap</c>, <c>Effects</c>, <c>TryTap</c>, <c>TryEffects</c>,
/// <c>Recover</c>, and <c>Ensure</c>.
/// <c>Branch</c>, <c>Detach</c>, and <c>Finally</c> are intentionally omitted to
/// keep the branch scope well-defined.
/// </summary>
/// <typeparam name="TPayload">The type of the payload.</typeparam>
/// <typeparam name="TError">The type of the error.</typeparam>
public sealed class BranchBuilder<TPayload, TError>
{
	// Internal operator list: (currentEither, lastRightSnapshot, ct) -> newEither
	internal List<Func<Either<TError, TPayload>, TPayload, CancellationToken, Task<Either<TError, TPayload>>>> Operators { get; } = [];

	/// <summary>
	/// Adds an asynchronous payload-progression step.
	/// <para>On <c>Right</c>: executes and replaces the rail state with the returned <see cref="Either{TError,TPayload}"/>.</para>
	/// <para>On <c>Left</c>: passes through unchanged without invoking the delegate.</para>
	/// </summary>
	public BranchBuilder<TPayload, TError> Do(Func<TPayload, CancellationToken, Task<Either<TError, TPayload>>> activity)
	{
		Operators.Add((current, _, ct) =>
			current.IsRight
				? activity(current.Right!, ct)
				: Task.FromResult(current));
		return this;
	}

	/// <summary>
	/// Adds a synchronous payload-progression step.
	/// <para>On <c>Right</c>: executes and replaces the rail state with the returned <see cref="Either{TError,TPayload}"/>.</para>
	/// <para>On <c>Left</c>: passes through unchanged without invoking the delegate.</para>
	/// </summary>
	public BranchBuilder<TPayload, TError> Do(Func<TPayload, Either<TError, TPayload>> activity)
	{
		Operators.Add((current, _, _ct) =>
			Task.FromResult(current.IsRight ? activity(current.Right!) : current));
		return this;
	}

	/// <summary>
	/// Adds a strict pass-through side effect that does not change the payload.
	/// <para>On <c>Right</c>: executes; a thrown exception propagates to the caller.</para>
	/// <para>On <c>Left</c>: passes through without invoking the delegate.</para>
	/// </summary>
	public BranchBuilder<TPayload, TError> Tap(Action<TPayload> effect)
	{
		Operators.Add((current, _, _ct) =>
		{
			if (!current.IsRight) return Task.FromResult(current);
			effect(current.Right!);
			return Task.FromResult(current);
		});
		return this;
	}

	/// <summary>
	/// Adds a strict asynchronous pass-through side effect that does not change the payload.
	/// <para>On <c>Right</c>: executes; exceptions propagate to the caller.</para>
	/// <para>On <c>Left</c>: passes through without invoking the delegate.</para>
	/// </summary>
	public BranchBuilder<TPayload, TError> Tap(Func<TPayload, CancellationToken, Task> effect)
	{
		Operators.Add(async (current, _, ct) =>
		{
			if (!current.IsRight) return current;
			await effect(current.Right!, ct);
			return current;
		});
		return this;
	}

	/// <summary>
	/// Adds a strict asynchronous pass-through side effect with an explicit error channel.
	/// <para>On <c>Right</c>: executes; returning <c>Left(err)</c> transitions the rail to <c>Left</c>; payload is unchanged on success.</para>
	/// <para>On <c>Left</c>: passes through without invoking the delegate.</para>
	/// </summary>
	public BranchBuilder<TPayload, TError> Tap(Func<TPayload, CancellationToken, Task<Either<TError, Unit>>> effect)
	{
		Operators.Add(async (current, _, ct) =>
		{
			if (!current.IsRight) return current;
			var result = await effect(current.Right!, ct);
			return result.IsLeft
				? Either<TError, TPayload>.FromLeft(result.Left!)
				: current;
		});
		return this;
	}

	/// <summary>
	/// Adds grouped strict pass-through side effects. The first inner failure transitions the rail to <c>Left</c>;
	/// remaining inner effects do not run. The payload is unchanged on success.
	/// <para>On <c>Left</c>: passes through without invoking any inner effect.</para>
	/// </summary>
	public BranchBuilder<TPayload, TError> Effects(Action<EffectsBuilder<TPayload, TError>> configure)
	{
		var builder = new EffectsBuilder<TPayload, TError>();
		configure(builder);
		var effects = builder.Effects;

		Operators.Add(async (current, _, ct) =>
		{
			if (!current.IsRight) return current;
			var payload = current.Right!;
			foreach (var effect in effects)
			{
				var result = await effect(payload, ct);
				if (result.IsLeft)
					return Either<TError, TPayload>.FromLeft(result.Left!);
			}
			return current;
		});
		return this;
	}

	/// <summary>
	/// Adds best-effort grouped side effects. Failures in inner effects are swallowed; every inner effect is
	/// attempted in registration order. The rail remains on <c>Right</c> regardless of inner failures.
	/// <para>On <c>Left</c>: passes through without invoking any inner effect.</para>
	/// </summary>
	public BranchBuilder<TPayload, TError> TryEffects(Action<EffectsBuilder<TPayload, TError>> configure)
	{
		var builder = new EffectsBuilder<TPayload, TError>();
		configure(builder);
		var effects = builder.Effects;

		Operators.Add(async (current, _, ct) =>
		{
			if (!current.IsRight) return current;
			var payload = current.Right!;
			foreach (var effect in effects)
			{
				try { await effect(payload, ct); }
				catch { /* swallow */ }
			}
			return current;
		});
		return this;
	}

	/// <summary>
	/// Adds a best-effort single side effect whose failure does not affect the rail.
	/// Thrown exceptions are swallowed. The payload is unchanged.
	/// <para>On <c>Left</c>: passes through without invoking the delegate.</para>
	/// </summary>
	public BranchBuilder<TPayload, TError> TryTap(Action<TPayload> effect)
	{
		Operators.Add((current, _, _ct) =>
		{
			if (!current.IsRight) return Task.FromResult(current);
			try { effect(current.Right!); } catch { /* swallow */ }
			return Task.FromResult(current);
		});
		return this;
	}

	/// <summary>
	/// Adds a best-effort asynchronous single side effect whose failure does not affect the rail.
	/// Exceptions and <c>Left</c> returns are swallowed. The payload is unchanged.
	/// <para>On <c>Left</c>: passes through without invoking the delegate.</para>
	/// </summary>
	public BranchBuilder<TPayload, TError> TryTap(Func<TPayload, CancellationToken, Task> effect)
	{
		Operators.Add(async (current, _, ct) =>
		{
			if (!current.IsRight) return current;
			try { await effect(current.Right!, ct); } catch { /* swallow */ }
			return current;
		});
		return this;
	}

	/// <summary>
	/// Adds a typed recovery operator. When the rail is <c>Left</c> and the error value
	/// is assignable to <typeparamref name="TErr"/>, the synchronous <paramref name="handler"/> runs and
	/// its returned payload becomes the new <c>Right</c>.
	/// Non-matching <c>Left</c> values and <c>Right</c> values pass through unchanged.
	/// The handler receives the pre-failure payload snapshot, not <c>default</c>.
	/// </summary>
	public BranchBuilder<TPayload, TError> Recover<TErr>(Func<TErr, TPayload, TPayload> handler)
	{
		Operators.Add((current, lastRight, _ct) =>
		{
			if (!current.IsLeft) return Task.FromResult(current);
			if (current.Left is TErr err)
				return Task.FromResult(Either<TError, TPayload>.FromRight(handler(err, lastRight)));
			return Task.FromResult(current);
		});
		return this;
	}

	/// <summary>
	/// Adds a typed recovery operator. When the rail is <c>Left</c> and the error value
	/// is assignable to <typeparamref name="TErr"/>, the asynchronous <paramref name="handler"/> runs and
	/// its returned payload becomes the new <c>Right</c>.
	/// Non-matching <c>Left</c> values and <c>Right</c> values pass through unchanged.
	/// The handler receives the pre-failure payload snapshot, not <c>default</c>.
	/// </summary>
	public BranchBuilder<TPayload, TError> Recover<TErr>(Func<TErr, TPayload, CancellationToken, Task<TPayload>> handler)
	{
		Operators.Add(async (current, lastRight, ct) =>
		{
			if (!current.IsLeft) return current;
			if (current.Left is TErr err)
				return Either<TError, TPayload>.FromRight(await handler(err, lastRight, ct));
			return current;
		});
		return this;
	}

	/// <summary>
	/// Adds a business-rule enforcement operator. When <paramref name="when"/> returns <c>true</c>
	/// the rail remains <c>Right(payload)</c> unchanged. When <paramref name="when"/> returns <c>false</c>
	/// the rail transitions to <c>Left(failWith(payload))</c>.
	/// <para>On <c>Left</c>: passes through without evaluating <paramref name="when"/> or <paramref name="failWith"/>.</para>
	/// </summary>
	public BranchBuilder<TPayload, TError> Ensure(Func<TPayload, bool> when, Func<TPayload, TError> failWith)
	{
		Operators.Add((current, _, _ct) =>
		{
			if (!current.IsRight) return Task.FromResult(current);
			var payload = current.Right!;
			return Task.FromResult(!when(payload)
				? Either<TError, TPayload>.FromLeft(failWith(payload))
				: current);
		});
		return this;
	}
}