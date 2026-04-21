using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Xunit;
using Zooper.Fox;

namespace Zooper.Bee.Tests;

public class EffectsTests
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
    public async Task Effects_Sync_AllRunAndPassThrough()
    {
        var log = new List<string>();
        var railway = Build(b => b
            .Effects(eff => eff
                .Do(p => log.Add("A"))
                .Do(p => log.Add("B")))
            .Do(p => Either<Err, Pay>.FromRight(p with { Note = "main" })));

        var result = await railway.Execute(new Req(1));
        result.IsRight.Should().BeTrue();
        log.Should().Equal("A", "B");
    }

    [Fact]
    public async Task Effects_CanReturnError_StopsPipeline()
    {
        var railway = Build(b => b
            .Effects(eff => eff
                .Do(async (p, ct) =>
                {
                    await Task.Yield();
                    return Either<Err, Unit>.FromLeft(new Err("EFF_ERR"));
                })));

        var result = await railway.Execute(new Req(1));
        result.IsLeft.Should().BeTrue();
        result.Left.Code.Should().Be("EFF_ERR");
    }

    [Fact]
    public async Task TryEffects_ExceptionSwallowed_PipelineContinues()
    {
        var railway = Build(b => b
            .TryEffects(eff => eff
                .Do(async (p, ct) =>
                {
                    await Task.Yield();
                    throw new InvalidOperationException("boom");
                }))
            .Do(p => Either<Err, Pay>.FromRight(p with { Note = "after" })));

        var result = await railway.Execute(new Req(1));
        result.IsRight.Should().BeTrue();
        result.Right.Note.Should().Be("after");
    }
}
