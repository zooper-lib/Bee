using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Xunit;
using Zooper.Fox;

namespace Zooper.Bee.Tests;

public class RailwayTests
{
	private record Req(string Name, int Value);
	private record Pay(string Name, int Value, string? Result = null);
	private record Succ(string Result);
	private record Err(string Code);

	private static Railway<Req, Succ, Err> Build(
		Action<RailwayStepsBuilder<Req, Pay, Succ, Err>> configure)
		=> Railway.Create<Req, Pay, Succ, Err>(
			r => new Pay(r.Name, r.Value),
			p => new Succ(p.Result ?? ""),
			configure);

	[Fact]
	public async Task Do_Sync_TransformsPayload()
	{
		var railway = Build(b => b
			.Do(p => Either<Err, Pay>.FromRight(p with { Result = "ok" })));
		var result = await railway.Execute(new Req("x", 1));
		result.IsRight.Should().BeTrue();
		result.Right.Result.Should().Be("ok");
	}

	[Fact]
	public async Task Do_ReturnsLeft_SubsequentDoSkipped()
	{
		bool secondCalled = false;
		var railway = Build(b => b
			.Do(_ => Either<Err, Pay>.FromLeft(new Err("E1")))
			.Do(p => { secondCalled = true; return Either<Err, Pay>.FromRight(p); }));
		var result = await railway.Execute(new Req("x", 1));
		result.IsLeft.Should().BeTrue();
		secondCalled.Should().BeFalse();
	}

	[Fact]
	public async Task Ensure_FailsWhenConditionFalse()
	{
		var railway = Build(b => b
			.Ensure(p => p.Value > 0, p => new Err("NEG")));
		var result = await railway.Execute(new Req("x", -1));
		result.IsLeft.Should().BeTrue();
		result.Left.Code.Should().Be("NEG");
	}

	[Fact]
	public async Task Guard_BlocksExecutionBeforeSteps()
	{
		var railway = Railway.Create<Req, Pay, Succ, Err>(
			r => new Pay(r.Name, r.Value),
			p => new Succ(p.Result ?? ""),
			guards => guards.Guard(r => r.Value > 0
				? Either<Err, Unit>.FromRight(Unit.Value)
				: Either<Err, Unit>.FromLeft(new Err("GUARD"))),
			steps => steps.Do(p => Either<Err, Pay>.FromRight(p with { Result = "ok" })));
		var badResult = await railway.Execute(new Req("x", -1));
		badResult.IsLeft.Should().BeTrue();
		badResult.Left.Code.Should().Be("GUARD");
	}

	[Fact]
	public async Task Phases_RunValidationThenGuardThenSteps()
	{
		var log = new List<string>();
		var railway = Railway.Create<Req, Pay, Succ, Err>(
			r => new Pay(r.Name, r.Value),
			p => new Succ(p.Result ?? ""),
			validations => validations.Validate(_ => { log.Add("validate"); return Option<Err>.None(); }),
			guards => guards.Guard(_ => { log.Add("guard"); return Either<Err, Unit>.FromRight(Unit.Value); }),
			steps => steps.Do(p => { log.Add("step"); return Either<Err, Pay>.FromRight(p with { Result = "ok" }); }));

		var result = await railway.Execute(new Req("x", 1));

		result.IsRight.Should().BeTrue();
		log.Should().Equal("validate", "guard", "step");
	}

	[Fact]
	public async Task Phases_OrderIndependentOfDelegateArguments()
	{
		// Guard configured before validation in the argument list; validation must still run first.
		var log = new List<string>();
		var railway = Railway.Create<Req, Pay, Succ, Err>(
			r => new Pay(r.Name, r.Value),
			p => new Succ(p.Result ?? ""),
			validations => validations.Validate(_ => { log.Add("validate"); return Option<Err>.None(); }),
			guards => guards.Guard(_ => { log.Add("guard"); return Either<Err, Unit>.FromRight(Unit.Value); }),
			steps => steps.Do(p => Either<Err, Pay>.FromRight(p)));

		await railway.Execute(new Req("x", 1));

		log.Should().Equal("validate", "guard");
	}

	[Fact]
	public async Task Validation_Failure_ShortCircuitsBeforeGuardAndSteps()
	{
		var guardRan = false;
		var stepRan = false;
		var railway = Railway.Create<Req, Pay, Succ, Err>(
			r => new Pay(r.Name, r.Value),
			p => new Succ(p.Result ?? ""),
			validations => validations.Validate(_ => Option<Err>.Some(new Err("VAL"))),
			guards => guards.Guard(_ => { guardRan = true; return Either<Err, Unit>.FromRight(Unit.Value); }),
			steps => steps.Do(p => { stepRan = true; return Either<Err, Pay>.FromRight(p); }));

		var result = await railway.Execute(new Req("x", 1));

		result.IsLeft.Should().BeTrue();
		result.Left.Code.Should().Be("VAL");
		guardRan.Should().BeFalse();
		stepRan.Should().BeFalse();
	}

	[Fact]
	public async Task Guard_Failure_ShortCircuitsBeforeStepsAfterValidationsPass()
	{
		var stepRan = false;
		var railway = Railway.Create<Req, Pay, Succ, Err>(
			r => new Pay(r.Name, r.Value),
			p => new Succ(p.Result ?? ""),
			validations => validations.Validate(_ => Option<Err>.None()),
			guards => guards.Guard(_ => Either<Err, Unit>.FromLeft(new Err("GUARD"))),
			steps => steps.Do(p => { stepRan = true; return Either<Err, Pay>.FromRight(p); }));

		var result = await railway.Execute(new Req("x", 1));

		result.IsLeft.Should().BeTrue();
		result.Left.Code.Should().Be("GUARD");
		stepRan.Should().BeFalse();
	}

	[Fact]
	public async Task Validations_FirstRegisteredFailureWins()
	{
		var railway = Railway.Create<Req, Pay, Succ, Err>(
			r => new Pay(r.Name, r.Value),
			p => new Succ(p.Result ?? ""),
			validations => validations
				.Validate(_ => Option<Err>.Some(new Err("V1")))
				.Validate(_ => Option<Err>.Some(new Err("V2"))),
			null,
			steps => steps.Do(p => Either<Err, Pay>.FromRight(p)));

		var result = await railway.Execute(new Req("x", 1));

		result.Left.Code.Should().Be("V1");
	}

	[Fact]
	public async Task Guards_FirstRegisteredFailureWins()
	{
		var railway = Railway.Create<Req, Pay, Succ, Err>(
			r => new Pay(r.Name, r.Value),
			p => new Succ(p.Result ?? ""),
			guards => guards
				.Guard(_ => Either<Err, Unit>.FromLeft(new Err("G1")))
				.Guard(_ => Either<Err, Unit>.FromLeft(new Err("G2"))),
			steps => steps.Do(p => Either<Err, Pay>.FromRight(p)));

		var result = await railway.Execute(new Req("x", 1));

		result.Left.Code.Should().Be("G1");
	}

	[Fact]
	public async Task NoValidationsOrGuards_RunsStepsDirectly()
	{
		var railway = Railway.Create<Req, Pay, Succ, Err>(
			r => new Pay(r.Name, r.Value),
			p => new Succ(p.Result ?? ""),
			steps => steps.Do(p => Either<Err, Pay>.FromRight(p with { Result = "ok" })));

		var result = await railway.Execute(new Req("x", 1));

		result.IsRight.Should().BeTrue();
		result.Right.Result.Should().Be("ok");
	}
}
