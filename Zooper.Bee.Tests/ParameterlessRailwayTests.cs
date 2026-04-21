using System;
using System.Threading.Tasks;
using FluentAssertions;
using Xunit;
using Zooper.Bee.Extensions;
using Zooper.Fox;

namespace Zooper.Bee.Tests;

public class ParameterlessRailwayTests
{
    private record Pay(string Status = "Waiting");
    private record Succ(string Status);
    private record Err(string Code);

    [Fact]
    public async Task ParameterlessRailway_Execute_Success()
    {
        var railway = Railway.Create<Pay, Succ, Err>(
            () => new Pay(),
            p => new Succ(p.Status),
            b => b
                .Do(p => Either<Err, Pay>.FromRight(p with { Status = "Done" })));

        var result = await railway.Execute();
        result.IsRight.Should().BeTrue();
        result.Right.Status.Should().Be("Done");
    }

    [Fact]
    public async Task ParameterlessRailway_Execute_Error()
    {
        var railway = Railway.Create<Pay, Succ, Err>(
            () => new Pay(),
            p => new Succ(p.Status),
            b => b
                .Do(_ => Either<Err, Pay>.FromLeft(new Err("FAIL"))));

        var result = await railway.Execute();
        result.IsLeft.Should().BeTrue();
        result.Left.Code.Should().Be("FAIL");
    }
}
