using System;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Xunit;
using Zooper.Fox;

namespace Zooper.Bee.Tests;

public class GuardConditionTests
{
    private record Req(string Category, int Value);
    private record Pay(string Category, int Value, string? Result = null);
    private record Succ(string Result);
    private record Err(string Code);

    private static Railway<Req, Succ, Err> Build(
        Action<RailwayGuardBuilder<Req, Pay, Succ, Err>> guards)
        => Railway.Create<Req, Pay, Succ, Err>(
            r => new Pay(r.Category, r.Value),
            p => new Succ(p.Result ?? ""),
            guards,
            steps => steps.Do(p => Either<Err, Pay>.FromRight(p with { Result = "ok" })));

    [Fact]
    public async Task When_ConditionTrue_GroupGuardFails_ShortCircuits()
    {
        var railway = Build(g => g
            .When(
                r => r.Category == "Premium",
                grp => grp.Guard(_ => Either<Err, Unit>.FromLeft(new Err("GUARD")))));

        var result = await railway.Execute(new Req("Premium", 1));
        result.IsLeft.Should().BeTrue();
        result.Left.Code.Should().Be("GUARD");
    }

    [Fact]
    public async Task When_ConditionTrue_GroupGuardPasses_RailContinues()
    {
        var railway = Build(g => g
            .When(
                r => r.Category == "Premium",
                grp => grp.Guard(_ => Either<Err, Unit>.FromRight(Unit.Value))));

        var result = await railway.Execute(new Req("Premium", 1));
        result.IsRight.Should().BeTrue();
        result.Right.Result.Should().Be("ok");
    }

    [Fact]
    public async Task When_ConditionFalse_GroupGuardsSkipped_RailContinues()
    {
        var guardRan = false;
        var railway = Build(g => g
            .When(
                r => r.Category == "Premium",
                grp => grp.Guard(_ =>
                {
                    guardRan = true;
                    return Either<Err, Unit>.FromLeft(new Err("GUARD"));
                })));

        var result = await railway.Execute(new Req("Standard", 1));
        result.IsRight.Should().BeTrue();
        guardRan.Should().BeFalse();
    }

    [Fact]
    public async Task When_AsyncCondition_True_GroupGuardRuns()
    {
        var railway = Build(g => g
            .When(
                async (r, _) => { await Task.Yield(); return r.Value > 10; },
                grp => grp.Guard(_ => Either<Err, Unit>.FromLeft(new Err("GUARD")))));

        var blocked = await railway.Execute(new Req("x", 100));
        blocked.IsLeft.Should().BeTrue();
        blocked.Left.Code.Should().Be("GUARD");
    }

    [Fact]
    public async Task When_AsyncCondition_False_GroupGuardsSkipped()
    {
        var railway = Build(g => g
            .When(
                async (r, _) => { await Task.Yield(); return r.Value > 10; },
                grp => grp.Guard(_ => Either<Err, Unit>.FromLeft(new Err("GUARD")))));

        var passed = await railway.Execute(new Req("x", 1));
        passed.IsRight.Should().BeTrue();
    }

    [Fact]
    public async Task When_Nested_OuterFalse_SkipsInnerAndDoesNotEvaluateInnerCondition()
    {
        var innerConditionEvaluated = false;
        var innerGuardRan = false;

        var railway = Build(g => g
            .When(
                r => r.Category == "Premium",
                outer => outer.When(
                    r => { innerConditionEvaluated = true; return r.Value > 0; },
                    inner => inner.Guard(_ =>
                    {
                        innerGuardRan = true;
                        return Either<Err, Unit>.FromLeft(new Err("INNER"));
                    }))));

        var result = await railway.Execute(new Req("Standard", 100));
        result.IsRight.Should().BeTrue();
        innerConditionEvaluated.Should().BeFalse();
        innerGuardRan.Should().BeFalse();
    }

    [Fact]
    public async Task When_Nested_BothTrue_RunsNestedGuard()
    {
        var railway = Build(g => g
            .When(
                r => r.Category == "Premium",
                outer => outer.When(
                    r => r.Value > 0,
                    inner => inner.Guard(_ => Either<Err, Unit>.FromLeft(new Err("INNER"))))));

        var result = await railway.Execute(new Req("Premium", 100));
        result.IsLeft.Should().BeTrue();
        result.Left.Code.Should().Be("INNER");
    }

    [Fact]
    public async Task UnconditionalGuard_OutsideWhen_AlwaysRuns()
    {
        var railway = Build(g => g
            .Guard(_ => Either<Err, Unit>.FromLeft(new Err("ALWAYS")))
            .When(
                r => r.Category == "Premium",
                grp => grp.Guard(_ => Either<Err, Unit>.FromRight(Unit.Value))));

        // Category is Standard so the When group is skipped, but the plain guard still runs.
        var result = await railway.Execute(new Req("Standard", 1));
        result.IsLeft.Should().BeTrue();
        result.Left.Code.Should().Be("ALWAYS");
    }
}
