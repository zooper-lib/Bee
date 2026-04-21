using System;
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
}
