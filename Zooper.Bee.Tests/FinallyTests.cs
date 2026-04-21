using System;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Xunit;
using Zooper.Fox;

namespace Zooper.Bee.Tests;

public class FinallyTests
{
    private record Req(int Value);
    private record Pay(int Value, string? Note = null);
    private record Succ(string? Note);
    private record Err(string Code);

    private static Railway<Req, Succ, Err> Build(
        Action<RailwayStepsBuilder<Req, Pay, Succ, Err>> configure)
        => Railway.Create<Req, Pay, Succ, Err>(
            r => new Pay(r.Value),
            p => new Succ(p.Note),
            configure);

    [Fact]
    public async Task Finally_ExecutesOnSuccess()
    {
        bool called = false;
        var railway = Build(b => b
            .Do(p => Either<Err, Pay>.FromRight(p with { Note = "ok" }))
            .Finally(p => { called = true; }));

        var result = await railway.Execute(new Req(1));
        result.IsRight.Should().BeTrue();
        called.Should().BeTrue();
    }

    [Fact]
    public async Task Finally_ExecutesOnError()
    {
        bool called = false;
        var railway = Build(b => b
            .Do(_ => Either<Err, Pay>.FromLeft(new Err("E1")))
            .Finally(p => { called = true; }));

        await railway.Execute(new Req(1));
        called.Should().BeTrue();
    }

    [Fact]
    public async Task Finally_ExceptionSwallowed_SubsequentFinallyStillRuns()
    {
        bool secondCalled = false;
        var railway = Build(b => b
            .Finally(p => throw new InvalidOperationException("finally-boom"))
            .Finally(p => { secondCalled = true; }));

        await railway.Execute(new Req(1));
        secondCalled.Should().BeTrue();
    }

    [Fact]
    public async Task Finally_Async_ExecutesOnSuccess()
    {
        bool called = false;
        var railway = Build(b => b
            .Do(p => Either<Err, Pay>.FromRight(p with { Note = "ok" }))
            .Finally(async (p, ct) =>
            {
                await Task.Yield();
                called = true;
            }));

        var result = await railway.Execute(new Req(1));
        result.IsRight.Should().BeTrue();
        called.Should().BeTrue();
    }
}
