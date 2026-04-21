using System;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Xunit;
using Zooper.Fox;

namespace Zooper.Bee.Tests;

public class TapTests
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
    public async Task Tap_Sync_ObservesPayload_PassesThrough()
    {
        string? observed = null;
        var railway = Build(b => b
            .Do(p => Either<Err, Pay>.FromRight(p with { Note = "set" }))
            .Tap(p => { observed = p.Note; }));

        var result = await railway.Execute(new Req(1));
        result.IsRight.Should().BeTrue();
        observed.Should().Be("set");
        result.Right.Note.Should().Be("set");
    }

    [Fact]
    public async Task Tap_Async_NoReturn_PassesThrough()
    {
        string? observed = null;
        var railway = Build(b => b
            .Do(p => Either<Err, Pay>.FromRight(p with { Note = "async" }))
            .Tap(async (p, ct) =>
            {
                await Task.Yield();
                observed = p.Note;
            }));

        var result = await railway.Execute(new Req(1));
        result.IsRight.Should().BeTrue();
        observed.Should().Be("async");
    }

    [Fact]
    public async Task Tap_Either_CanReturnError()
    {
        var railway = Build(b => b
            .Tap(async (p, ct) =>
            {
                await Task.Yield();
                return Either<Err, Unit>.FromLeft(new Err("TAP_ERR"));
            }));

        var result = await railway.Execute(new Req(1));
        result.IsLeft.Should().BeTrue();
        result.Left.Code.Should().Be("TAP_ERR");
    }

    [Fact]
    public async Task Tap_SkipsOnLeft()
    {
        bool tapCalled = false;
        var railway = Build(b => b
            .Do(_ => Either<Err, Pay>.FromLeft(new Err("E1")))
            .Tap(p => { tapCalled = true; }));

        await railway.Execute(new Req(1));
        tapCalled.Should().BeFalse();
    }

    [Fact]
    public async Task TryTap_Sync_ExceptionSwallowed()
    {
        var railway = Build(b => b
            .TryTap(p => throw new InvalidOperationException("boom"))
            .Do(p => Either<Err, Pay>.FromRight(p with { Note = "after" })));

        var result = await railway.Execute(new Req(1));
        result.IsRight.Should().BeTrue();
        result.Right.Note.Should().Be("after");
    }

    [Fact]
    public async Task TryTap_Async_ExceptionSwallowed()
    {
        var railway = Build(b => b
            .TryTap(async (p, ct) =>
            {
                await Task.Yield();
                throw new InvalidOperationException("async boom");
            })
            .Do(p => Either<Err, Pay>.FromRight(p with { Note = "after" })));

        var result = await railway.Execute(new Req(1));
        result.IsRight.Should().BeTrue();
    }
}
