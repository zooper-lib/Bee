using System;
using System.Threading.Tasks;
using FluentAssertions;
using Xunit;
using Zooper.Fox;

namespace Zooper.Bee.Tests;

public class BranchTests
{
    private record Req(string Category, int Value);
    private record Pay(string Category, int Value, string? Result = null);
    private record Succ(string? Result);
    private record Err(string Code);

    private static Railway<Req, Succ, Err> Build(
        Action<RailwayStepsBuilder<Req, Pay, Succ, Err>> configure)
        => Railway.Create<Req, Pay, Succ, Err>(
            r => new Pay(r.Category, r.Value),
            p => new Succ(p.Result),
            configure);

    [Fact]
    public async Task Branch_ExecutesBodyWhenConditionTrue()
    {
        var railway = Build(b => b
            .Branch(
                p => p.Category == "Premium",
                br => br.Do(p => Either<Err, Pay>.FromRight(p with { Result = "Premium" }))));

        var result = await railway.Execute(new Req("Premium", 100));
        result.Right.Result.Should().Be("Premium");
    }

    [Fact]
    public async Task Branch_SkipsBodyWhenConditionFalse()
    {
        var railway = Build(b => b
            .Do(p => Either<Err, Pay>.FromRight(p with { Result = "initial" }))
            .Branch(
                p => p.Category == "Premium",
                br => br.Do(p => Either<Err, Pay>.FromRight(p with { Result = "Premium" }))));

        var result = await railway.Execute(new Req("Standard", 100));
        result.Right.Result.Should().Be("initial");
    }

    [Fact]
    public async Task Branch_SkipsOnLeft()
    {
        bool branchCalled = false;
        var railway = Build(b => b
            .Do(_ => Either<Err, Pay>.FromLeft(new Err("E1")))
            .Branch(
                p => { branchCalled = true; return true; },
                br => br.Do(p => Either<Err, Pay>.FromRight(p))));

        var result = await railway.Execute(new Req("x", 1));
        result.IsLeft.Should().BeTrue();
        branchCalled.Should().BeFalse();
    }

    [Fact]
    public async Task Branch_PropagatesErrorFromBranchBody()
    {
        var railway = Build(b => b
            .Branch(
                p => true,
                br => br.Do(_ => Either<Err, Pay>.FromLeft(new Err("BRANCH_ERR")))));

        var result = await railway.Execute(new Req("x", 1));
        result.IsLeft.Should().BeTrue();
        result.Left.Code.Should().Be("BRANCH_ERR");
    }

    [Fact]
    public async Task Branch_LocalRecoverHandlesError()
    {
        var railway = Build(b => b
            .Branch(
                p => true,
                br => br
                    .Do(_ => Either<Err, Pay>.FromLeft(new Err("INNER")))
                    .Recover<Err>((err, last) => last with { Result = "recovered" })));

        var result = await railway.Execute(new Req("x", 1));
        result.IsRight.Should().BeTrue();
        result.Right.Result.Should().Be("recovered");
    }

    [Fact]
    public async Task MultipleBranches_ExecuteInOrder()
    {
        var railway = Build(b => b
            .Do(p => Either<Err, Pay>.FromRight(p with { Result = "start" }))
            .Branch(
                p => p.Category == "A",
                br => br.Do(p => Either<Err, Pay>.FromRight(p with { Result = p.Result + "+A" })))
            .Branch(
                p => p.Value > 50,
                br => br.Do(p => Either<Err, Pay>.FromRight(p with { Result = p.Result + "+High" }))));

        var result = await railway.Execute(new Req("A", 100));
        result.Right.Result.Should().Be("start+A+High");
    }
}
