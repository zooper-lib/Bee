using System;
using System.Collections.Generic;
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

    [Fact]
    public async Task ParameterlessRailway_RunsValidationThenGuardThenSteps()
    {
        var log = new List<string>();
        var railway = Railway.Create<Pay, Succ, Err>(
            () => new Pay(),
            p => new Succ(p.Status),
            validations => validations.Validate(_ => { log.Add("validate"); return Option<Err>.None(); }),
            guards => guards.Guard(_ => { log.Add("guard"); return Either<Err, Unit>.FromRight(Unit.Value); }),
            steps => steps.Do(p => { log.Add("step"); return Either<Err, Pay>.FromRight(p with { Status = "Done" }); }));

        var result = await railway.Execute();

        result.IsRight.Should().BeTrue();
        result.Right.Status.Should().Be("Done");
        log.Should().Equal("validate", "guard", "step");
    }

    [Fact]
    public async Task ParameterlessRailway_ValidationFailure_ShortCircuits()
    {
        var railway = Railway.Create<Pay, Succ, Err>(
            () => new Pay(),
            p => new Succ(p.Status),
            validations => validations.Validate(_ => Option<Err>.Some(new Err("VAL"))),
            guards => guards.Guard(_ => Either<Err, Unit>.FromRight(Unit.Value)),
            steps => steps.Do(p => Either<Err, Pay>.FromRight(p)));

        var result = await railway.Execute();

        result.IsLeft.Should().BeTrue();
        result.Left.Code.Should().Be("VAL");
    }
}
