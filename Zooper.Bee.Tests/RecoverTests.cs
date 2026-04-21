using System;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Xunit;
using Zooper.Fox;

namespace Zooper.Bee.Tests;

public class RecoverTests
{
    private record Req(int Value);
    private record Pay(int Value, string? Note = null);
    private record Succ(string? Note);
    private record Err(string Code);
    private record SpecificErr(string Code, string Detail) : Err(Code);

    private static Railway<Req, Succ, Err> Build(
        Action<RailwayStepsBuilder<Req, Pay, Succ, Err>> configure)
        => Railway.Create<Req, Pay, Succ, Err>(
            r => new Pay(r.Value),
            p => new Succ(p.Note),
            configure);

    [Fact]
    public async Task Recover_HandlesMatchingErrorType_Sync()
    {
        var railway = Build(b => b
            .Do(_ => Either<Err, Pay>.FromLeft(new SpecificErr("SE", "detail")))
            .Recover<SpecificErr>((err, last) => last with { Note = $"recovered:{err.Detail}" }));

        var result = await railway.Execute(new Req(1));
        result.IsRight.Should().BeTrue();
        result.Right.Note.Should().Be("recovered:detail");
    }

    [Fact]
    public async Task Recover_HandlesMatchingErrorType_Async()
    {
        var railway = Build(b => b
            .Do(_ => Either<Err, Pay>.FromLeft(new SpecificErr("SE", "detail")))
            .Recover<SpecificErr>(async (err, last, ct) =>
            {
                await Task.Yield();
                return last with { Note = $"async-recovered:{err.Detail}" };
            }));

        var result = await railway.Execute(new Req(1));
        result.IsRight.Should().BeTrue();
        result.Right.Note.Should().Be("async-recovered:detail");
    }

    [Fact]
    public async Task Recover_DoesNotHandleNonMatchingErrorType()
    {
        var railway = Build(b => b
            .Do(_ => Either<Err, Pay>.FromLeft(new Err("PLAIN")))
            .Recover<SpecificErr>((err, last) => last with { Note = "recovered" }));

        var result = await railway.Execute(new Req(1));
        result.IsLeft.Should().BeTrue();
        result.Left.Code.Should().Be("PLAIN");
    }

    [Fact]
    public async Task Recover_UsesPreFailurePayload()
    {
        var railway = Build(b => b
            .Do(p => Either<Err, Pay>.FromRight(p with { Note = "before-error" }))
            .Do(_ => Either<Err, Pay>.FromLeft(new Err("E1")))
            .Recover<Err>((err, last) => last with { Note = $"recovered-from:{last.Note}" }));

        var result = await railway.Execute(new Req(1));
        result.IsRight.Should().BeTrue();
        result.Right.Note.Should().Be("recovered-from:before-error");
    }

    [Fact]
    public async Task Recover_OnRightState_Passthrough()
    {
        var railway = Build(b => b
            .Do(p => Either<Err, Pay>.FromRight(p with { Note = "ok" }))
            .Recover<Err>((err, last) => last with { Note = "should-not-be-called" }));

        var result = await railway.Execute(new Req(1));
        result.Right.Note.Should().Be("ok");
    }
}
