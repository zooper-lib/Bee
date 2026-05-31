using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Xunit;
using Zooper.Fox;

namespace Zooper.Bee.Tests;

/// <summary>
/// Tests for the Loop operator on RailwayStepsBuilder.
///
/// NOTE — negative compile assertions (task 4.17: LoopBuilder must not expose Loop, Detach, Finally):
/// These cannot be expressed as xUnit tests. The absence of these methods is enforced at compile time
/// by simply not defining them on LoopBuilder. Any attempt to call .Loop(), .Detach(), or .Finally()
/// inside a loop body will produce a CS1061 compile error.
/// </summary>
public class LoopTests
{
    // ───── shared types ─────────────────────────────────────────────────────

    private record Req(int Value);
    private record Pay(int Value, int Attempts = 0, string? Note = null);
    private record Succ(int Value, int Attempts);
    private record Err(string Code);
    private record TransientError(string Code) : Err(Code);

    private static Railway<Req, Succ, Err> Build(
        Action<RailwayStepsBuilder<Req, Pay, Succ, Err>> configure)
        => Railway.Create<Req, Pay, Succ, Err>(
            r => new Pay(r.Value),
            p => new Succ(p.Value, p.Attempts),
            configure);

    // ───── 4.1  Body short-circuit ───────────────────────────────────────────

    [Fact]
    public async Task Loop_BodyLeft_ExitsImmediately_SkipsUntilMutateExhausted()
    {
        var untilCalled = new List<int>();
        var mutateCalled = new List<int>();
        var exhaustedCalled = false;

        var railway = Build(b => b
            .Loop(
                body: lb => lb
                    .Do(p => p.Attempts == 1   // fail on 2nd iteration (index 1)
                        ? Either<Err, Pay>.FromLeft(new Err("BODY_ERR"))
                        : Either<Err, Pay>.FromRight(p with { Attempts = p.Attempts + 1 })),
                until: (p, a) => { untilCalled.Add(a); return false; },
                maxAttempts: 5,
                exhausted: (p, a) => { exhaustedCalled = true; return new Err("EXHAUSTED"); },
                mutate: (p, a) => { mutateCalled.Add(a); return p; }));

        var result = await railway.Execute(new Req(0));

        result.IsLeft.Should().BeTrue();
        result.Left!.Code.Should().Be("BODY_ERR");
        untilCalled.Should().HaveCount(1);   // ran for iteration 1 only
        mutateCalled.Should().HaveCount(1);  // ran once (between iter 1→2)
        exhaustedCalled.Should().BeFalse();
    }

    // ───── 4.2  Break on until ───────────────────────────────────────────────

    [Fact]
    public async Task Loop_UntilTrue_ExitsRight_SkipsMutateExhausted()
    {
        var mutateCalled = false;
        var exhaustedCalled = false;

        var railway = Build(b => b
            .Loop(
                body: lb => lb.Do(p => Either<Err, Pay>.FromRight(p with { Attempts = p.Attempts + 1 })),
                until: (_, a) => a == 1,  // break immediately on first iteration
                maxAttempts: 5,
                exhausted: (p, a) => { exhaustedCalled = true; return new Err("EXHAUSTED"); },
                mutate: (p, a) => { mutateCalled = true; return p; }));

        var result = await railway.Execute(new Req(42));

        result.IsRight.Should().BeTrue();
        result.Right!.Value.Should().Be(42);
        result.Right.Attempts.Should().Be(1);
        mutateCalled.Should().BeFalse();
        exhaustedCalled.Should().BeFalse();
    }

    // ───── 4.3  Exhaustion ───────────────────────────────────────────────────

    [Fact]
    public async Task Loop_Exhaustion_ExitsLeft_MutateNotCalledAfterFinalIteration()
    {
        var mutateCalls = new List<int>();

        var railway = Build(b => b
            .Loop(
                body: lb => lb.Do(p => Either<Err, Pay>.FromRight(p with { Attempts = p.Attempts + 1 })),
                until: (_, _) => false,
                maxAttempts: 3,
                exhausted: (p, a) => new Err($"EXHAUSTED:{a}"),
                mutate: (p, a) => { mutateCalls.Add(a); return p; }));

        var result = await railway.Execute(new Req(0));

        result.IsLeft.Should().BeTrue();
        result.Left!.Code.Should().Be("EXHAUSTED:3");
        mutateCalls.Should().BeEquivalentTo([1, 2], options => options.WithStrictOrdering());
    }

    // ───── 4.4  Mutate between iterations ───────────────────────────────────

    [Fact]
    public async Task Loop_Mutate_TransformsPayloadForNextIteration()
    {
        var startValues = new List<int>();

        var railway = Build(b => b
            .Loop(
                body: lb => lb.Do(p =>
                {
                    startValues.Add(p.Value);
                    return Either<Err, Pay>.FromRight(p with { Attempts = p.Attempts + 1 });
                }),
                until: (_, a) => a == 2,  // break after 2nd iteration
                maxAttempts: 5,
                exhausted: (p, a) => new Err("EXHAUSTED"),
                mutate: (p, a) => p with { Value = p.Value + 10 }));

        var result = await railway.Execute(new Req(1));

        result.IsRight.Should().BeTrue();
        startValues.Should().BeEquivalentTo([1, 11], options => options.WithStrictOrdering());
    }

    // ───── 4.5  No-mutate path ───────────────────────────────────────────────

    [Fact]
    public async Task Loop_NoMutate_NextIterationStartsWithSamePayload()
    {
        var startValues = new List<int>();

        var railway = Build(b => b
            .Loop(
                body: lb => lb.Do(p =>
                {
                    startValues.Add(p.Value);
                    return Either<Err, Pay>.FromRight(p with { Attempts = p.Attempts + 1 });
                }),
                until: (_, a) => a == 2,
                maxAttempts: 5,
                exhausted: (p, a) => new Err("EXHAUSTED")));
        // no mutate

        var result = await railway.Execute(new Req(7));

        result.IsRight.Should().BeTrue();
        startValues.Should().BeEquivalentTo([7, 7], options => options.WithStrictOrdering());
    }

    // ───── 4.6  Incoming Left passthrough ────────────────────────────────────

    [Fact]
    public async Task Loop_IncomingLeft_PassesThroughUnchanged()
    {
        var bodyCalled = false;
        var untilCalled = false;

        var railway = Build(b => b
            .Do(_ => Either<Err, Pay>.FromLeft(new Err("UPSTREAM")))
            .Loop(
                body: lb => lb.Do(p => { bodyCalled = true; return Either<Err, Pay>.FromRight(p); }),
                until: (_, _) => { untilCalled = true; return true; },
                maxAttempts: 3,
                exhausted: (p, a) => new Err("EXHAUSTED")));

        var result = await railway.Execute(new Req(0));

        result.IsLeft.Should().BeTrue();
        result.Left!.Code.Should().Be("UPSTREAM");
        bodyCalled.Should().BeFalse();
        untilCalled.Should().BeFalse();
    }

    // ───── 4.7  maxAttempts = 1 ──────────────────────────────────────────────

    [Fact]
    public async Task Loop_MaxAttemptsOne_UntilFalse_ExitsExhausted()
    {
        var railway = Build(b => b
            .Loop(
                body: lb => lb.Do(p => Either<Err, Pay>.FromRight(p with { Attempts = 1 })),
                until: (_, _) => false,
                maxAttempts: 1,
                exhausted: (p, a) => new Err($"EXHAUSTED:{a}")));

        var result = await railway.Execute(new Req(0));

        result.IsLeft.Should().BeTrue();
        result.Left!.Code.Should().Be("EXHAUSTED:1");
    }

    [Fact]
    public async Task Loop_MaxAttemptsOne_UntilTrue_ExitsRight()
    {
        var railway = Build(b => b
            .Loop(
                body: lb => lb.Do(p => Either<Err, Pay>.FromRight(p with { Attempts = 1 })),
                until: (_, _) => true,
                maxAttempts: 1,
                exhausted: (p, a) => new Err("EXHAUSTED")));

        var result = await railway.Execute(new Req(5));

        result.IsRight.Should().BeTrue();
        result.Right!.Value.Should().Be(5);
    }

    // ───── 4.8  maxAttempts validation ───────────────────────────────────────

    [Fact]
    public void Loop_MaxAttemptsZero_ThrowsAtRegistration()
    {
        Action act = () => Build(b => b
            .Loop(
                body: _ => { },
                until: (_, _) => true,
                maxAttempts: 0,
                exhausted: (p, a) => new Err("E")));

        act.Should().Throw<ArgumentOutOfRangeException>()
            .WithParameterName("maxAttempts");
    }

    [Fact]
    public void Loop_NegativeMaxAttempts_ThrowsAtRegistration()
    {
        Action act = () => Build(b => b
            .Loop(
                body: _ => { },
                until: (_, _) => true,
                maxAttempts: -5,
                exhausted: (p, a) => new Err("E")));

        act.Should().Throw<ArgumentOutOfRangeException>()
            .WithParameterName("maxAttempts");
    }

    // ───── 4.9  Empty body ───────────────────────────────────────────────────

    [Fact]
    public async Task Loop_EmptyBody_BehavesAsPurePoll()
    {
        // An empty body leaves the payload unchanged; until still receives it each iteration.
        var untilAttempts = new List<int>();

        var railway = Build(b => b
            .Loop(
                body: _ => { },  // empty
                until: (_, a) => { untilAttempts.Add(a); return a == 3; },
                maxAttempts: 10,
                exhausted: (p, a) => new Err("EXHAUSTED")));

        var result = await railway.Execute(new Req(99));

        result.IsRight.Should().BeTrue();
        result.Right!.Value.Should().Be(99);
        untilAttempts.Should().BeEquivalentTo([1, 2, 3], options => options.WithStrictOrdering());
    }

    // ───── 4.10  Recover inside body — keeps loop iterating ──────────────────

    [Fact]
    public async Task Loop_RecoverInsideBody_ConvertsLeftToRight_LoopContinues()
    {
        // Body always fails on iteration 1, Recover catches it → loop continues to iteration 2.
        var iterationCount = 0;

        var railway = Build(b => b
            .Loop(
                body: lb => lb
                    .Do(p =>
                    {
                        iterationCount++;
                        return iterationCount == 1
                            ? Either<Err, Pay>.FromLeft(new TransientError("TRANSIENT"))
                            : Either<Err, Pay>.FromRight(p with { Attempts = p.Attempts + 1 });
                    })
                    .Recover<TransientError>((err, last) => last with { Note = "recovered" }),
                until: (_, a) => a == 2,
                maxAttempts: 5,
                exhausted: (p, a) => new Err("EXHAUSTED")));

        var result = await railway.Execute(new Req(0));

        result.IsRight.Should().BeTrue();
        iterationCount.Should().Be(2);
    }

    // ───── 4.11  Recover scoping — per-iteration ─────────────────────────────

    [Fact]
    public async Task Loop_RecoverScopedToIteration_DoesNotCrossIterationBoundary()
    {
        // Recover catches the error in iteration 1, converting it to Right.
        // In iteration 2, a different Left is raised that no Recover matches — loop exits Left.
        var iterationCount = 0;

        var railway = Build(b => b
            .Loop(
                body: lb => lb
                    .Do(p =>
                    {
                        iterationCount++;
                        return iterationCount == 1
                            ? Either<Err, Pay>.FromLeft(new TransientError("TRANSIENT"))
                            : Either<Err, Pay>.FromLeft(new Err("FATAL"));
                    })
                    .Recover<TransientError>((err, last) => last),
                until: (_, _) => false,
                maxAttempts: 5,
                exhausted: (p, a) => new Err("EXHAUSTED")));

        var result = await railway.Execute(new Req(0));

        result.IsLeft.Should().BeTrue();
        result.Left!.Code.Should().Be("FATAL");
        iterationCount.Should().Be(2);
    }

    // ───── 4.12  Attempt index consistency ───────────────────────────────────

    [Fact]
    public async Task Loop_AttemptIndex_ConsistentAcrossUntilMutateExhausted()
    {
        var untilAttempts = new List<int>();
        var mutateAttempts = new List<int>();
        int exhaustedAttempt = 0;

        var railway = Build(b => b
            .Loop(
                body: lb => lb.Do(p => Either<Err, Pay>.FromRight(p)),
                until: (_, a) => { untilAttempts.Add(a); return false; },
                maxAttempts: 3,
                exhausted: (p, a) => { exhaustedAttempt = a; return new Err("EXHAUSTED"); },
                mutate: (p, a) => { mutateAttempts.Add(a); return p; }));

        await railway.Execute(new Req(0));

        // until called for iterations 1, 2, 3
        untilAttempts.Should().BeEquivalentTo([1, 2, 3], options => options.WithStrictOrdering());
        // mutate called between 1→2 and 2→3 (not after final)
        mutateAttempts.Should().BeEquivalentTo([1, 2], options => options.WithStrictOrdering());
        // exhausted called with attempt = 3
        exhaustedAttempt.Should().Be(3);
    }

    // ───── 4.13  Async hooks ─────────────────────────────────────────────────

    [Fact]
    public async Task Loop_AsyncHooks_AreAwaited_CancellationTokenPropagated()
    {
        var cts = new CancellationTokenSource();
        var untilToken = CancellationToken.None;
        var mutateToken = CancellationToken.None;

        var railway = Build(b => b
            .Loop(
                body: lb => lb.Do(async (p, ct) =>
                {
                    await Task.Yield();
                    return Either<Err, Pay>.FromRight(p with { Attempts = p.Attempts + 1 });
                }),
                until: async (p, a, ct) =>
                {
                    untilToken = ct;
                    await Task.Yield();
                    return a == 2;
                },
                maxAttempts: 5,
                exhausted: async (p, a, ct) =>
                {
                    await Task.Yield();
                    return new Err("EXHAUSTED");
                },
                mutate: async (p, a, ct) =>
                {
                    mutateToken = ct;
                    await Task.Yield();
                    return p;
                }));

        var result = await railway.Execute(new Req(0), cts.Token);

        result.IsRight.Should().BeTrue();
        untilToken.Should().Be(cts.Token);
        mutateToken.Should().Be(cts.Token);
    }

    // ───── 4.14  Mixed sync/async hooks ──────────────────────────────────────

    [Fact]
    public async Task Loop_MixedSyncAsync_CompilesAndExecutes()
    {
        // Sync until + async mutate via the async overload
        var railway = Build(b => b
            .Loop(
                body: lb => lb.Do(p => Either<Err, Pay>.FromRight(p with { Attempts = p.Attempts + 1 })),
                until: (p, a, _) => Task.FromResult(a == 2),   // sync until lifted
                maxAttempts: 5,
                exhausted: (p, a, _) => Task.FromResult(new Err("EXHAUSTED") as Err),
                mutate: async (p, a, ct) =>                     // async mutate
                {
                    await Task.Yield();
                    return p with { Value = p.Value + 1 };
                }));

        var result = await railway.Execute(new Req(0));

        result.IsRight.Should().BeTrue();
        result.Right!.Value.Should().Be(1);   // one mutate ran (between iter 1 and 2)
    }

    // ───── 4.15  Registration order ──────────────────────────────────────────

    [Fact]
    public async Task Loop_RegistrationOrder_PreservedAroundLoop()
    {
        var order = new List<string>();

        var railway = Build(b => b
            .Do(p => { order.Add("before"); return Either<Err, Pay>.FromRight(p); })
            .Loop(
                body: lb => lb.Do(p => { order.Add("body"); return Either<Err, Pay>.FromRight(p); }),
                until: (_, a) => { order.Add("until"); return a == 1; },
                maxAttempts: 3,
                exhausted: (p, a) => new Err("EXHAUSTED"))
            .Do(p => { order.Add("after"); return Either<Err, Pay>.FromRight(p); }));

        await railway.Execute(new Req(0));

        order.Should().BeEquivalentTo(["before", "body", "until", "after"],
            options => options.WithStrictOrdering());
    }

    // ───── 4.16  LoopBuilder operator surface (compile test) ─────────────────

    [Fact]
    public async Task LoopBuilder_ExposesAllDocumentedOperators()
    {
        // This test compiles and runs all allowed operators on LoopBuilder.
        var railway = Build(b => b
            .Loop(
                body: lb => lb
                    .Do(p => Either<Err, Pay>.FromRight(p))                                        // Do (sync)
                    .Do(async (p, ct) => { await Task.Yield(); return Either<Err, Pay>.FromRight(p); })  // Do (async)
                    .Tap(p => { _ = p.Value; })                                                    // Tap (sync)
                    .Tap(async (p, ct) => { await Task.Yield(); })                                 // Tap (async)
                    .TryTap(p => { _ = p.Value; })                                                 // TryTap (sync)
                    .TryTap(async (p, ct) => { await Task.Yield(); })                              // TryTap (async)
                    .Effects(fx => fx.Do(p => { _ = p.Value; }))                                   // Effects
                    .TryEffects(fx => fx.Do(p => { _ = p.Value; }))                                // TryEffects
                    .Branch(p => false, br => br.Do(p => Either<Err, Pay>.FromRight(p)))           // Branch
                    .Ensure(p => true, p => new Err("E"))                                          // Ensure
                    .Recover<Err>((err, last) => last),                                            // Recover
                until: (_, a) => a == 1,
                maxAttempts: 1,
                exhausted: (p, a) => new Err("EXHAUSTED")));

        var result = await railway.Execute(new Req(0));
        result.IsRight.Should().BeTrue();
    }

    // ───── 4.17  LoopBuilder must not expose Loop, Detach, Finally ───────────
    //
    // Negative compile assertions cannot be expressed as xUnit tests.
    // The absence of Loop, Detach, and Finally on LoopBuilder is enforced at compile
    // time — any attempt to call those methods inside a loop body produces CS1061.
    //
    // Uncomment the lines below (in a scratch file) to verify:
    //   lb.Loop(...);    // CS1061 — 'LoopBuilder<Pay, Err>' does not contain a definition for 'Loop'
    //   lb.Detach(...);  // CS1061 — 'LoopBuilder<Pay, Err>' does not contain a definition for 'Detach'
    //   lb.Finally(...); // CS1061 — 'LoopBuilder<Pay, Err>' does not contain a definition for 'Finally'

    // ───── 4.18  Inner Branch inside Loop body ───────────────────────────────

    [Fact]
    public async Task Loop_InnerBranch_RunsConditionallyPerIteration()
    {
        var branchExecutedAttempts = new List<int>();
        var attemptTracker = 0;

        var railway = Build(b => b
            .Loop(
                body: lb => lb
                    .Do(p =>
                    {
                        attemptTracker++;
                        return Either<Err, Pay>.FromRight(p with { Attempts = attemptTracker });
                    })
                    .Branch(
                        when: p => p.Attempts % 2 == 0,   // branch runs on even attempts
                        branch: br => br.Do(p =>
                        {
                            branchExecutedAttempts.Add(p.Attempts);
                            return Either<Err, Pay>.FromRight(p with { Note = "branched" });
                        })),
                until: (_, a) => a == 4,
                maxAttempts: 10,
                exhausted: (p, a) => new Err("EXHAUSTED")));

        var result = await railway.Execute(new Req(0));

        result.IsRight.Should().BeTrue();
        branchExecutedAttempts.Should().BeEquivalentTo([2, 4], options => options.WithStrictOrdering());
    }
}
