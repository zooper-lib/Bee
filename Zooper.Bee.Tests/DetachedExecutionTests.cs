using System;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Xunit;
using Zooper.Fox;

namespace Zooper.Bee.Tests;

public class DetachedExecutionTests
{
    private record Req(int Value);
    private record Pay(int Value, string? Result = null);
    private record Succ(string? Result);
    private record Err(string Code);

    private static Railway<Req, Succ, Err> Build(
        Action<RailwayStepsBuilder<Req, Pay, Succ, Err>> configure)
        => Railway.Create<Req, Pay, Succ, Err>(
            r => new Pay(r.Value),
            p => new Succ(p.Result),
            configure);

    [Fact]
    public async Task Detach_DoesNotBlockPipeline()
    {
        var railway = Build(b => b
            .Detach(eff => eff
                .Do(async (p, ct) =>
                {
                    await Task.Delay(50, ct);
                }))
            .Do(p => Either<Err, Pay>.FromRight(p with { Result = "main-done" })));

        var result = await railway.Execute(new Req(1));
        result.Right.Result.Should().Be("main-done");
    }

    [Fact]
    public async Task Detach_SkipsOnLeft()
    {
        bool detachCalled = false;
        var railway = Build(b => b
            .Do(_ => Either<Err, Pay>.FromLeft(new Err("E1")))
            .Detach(eff => eff
                .Do(p => { detachCalled = true; })));

        await railway.Execute(new Req(1));
        detachCalled.Should().BeFalse();
    }

    [Fact]
    public async Task Detach_ExceptionInEffect_DoesNotFaultPipeline()
    {
        var railway = Build(b => b
            .Detach(eff => eff
                .Do(async (p, ct) =>
                {
                    await Task.Yield();
                    throw new InvalidOperationException("detach boom");
                }))
            .Do(p => Either<Err, Pay>.FromRight(p with { Result = "ok" })));

        var result = await railway.Execute(new Req(1));
        result.IsRight.Should().BeTrue();
    }
}
