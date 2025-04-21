using System;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Xunit;
using Zooper.Fox;

namespace Zooper.Bee.Tests;

public class WorkflowTests
{
	#region Test Models
	// Request model
	private record TestRequest(string Name, int Value);

	// Payload model
	private record TestPayload(
		string Name,
		int Value,
		bool IsValidated = false,
		bool IsProcessed = false,
		string? Result = null);

	// Success result model
	private record TestSuccess(string Result);

	// Error model
	private record TestError(string Code, string Message);
	#endregion

	[Fact]
	public async Task Execute_ValidRequest_ReturnsSuccessResult()
	{
		// Arrange
		var workflow = new WorkflowBuilder<TestRequest, TestPayload, TestSuccess, TestError>(
			// Create the payload from the request
			request => new TestPayload(request.Name, request.Value),

			// Create the success result from the payload
			payload => new TestSuccess(payload.Result ?? "Default")
		)
		.Do(payload =>
		{
			// Validate the payload
			var validated = payload with { IsValidated = true };
			return Either<TestError, TestPayload>.FromRight(validated);
		})
		.Do(payload =>
		{
			// Process the payload
			var processed = payload with
			{
				IsProcessed = true,
				Result = $"Processed: {payload.Name}-{payload.Value}"
			};
			return Either<TestError, TestPayload>.FromRight(processed);
		})
		.Build();

		var request = new TestRequest("Test", 42);

		// Act
		var result = await workflow.Execute(request);

		// Assert
		result.IsRight.Should().BeTrue();
		result.Right.Result.Should().Be("Processed: Test-42");
	}

	[Fact]
	public async Task Execute_WithValidation_RejectsInvalidRequest()
	{
		// Arrange
		var workflow = new WorkflowBuilder<TestRequest, TestPayload, TestSuccess, TestError>(
			request => new TestPayload(request.Name, request.Value),
			payload => new TestSuccess(payload.Result ?? "Default")
		)
		.Validate(request =>
		{
			if (request.Value <= 0)
			{
				return Option<TestError>.Some(new TestError("INVALID_VALUE", "Value must be positive"));
			}
			return Option<TestError>.None();
		})
		.Do(payload => Either<TestError, TestPayload>.FromRight(payload with { IsProcessed = true }))
		.Build();

		var invalidRequest = new TestRequest("Test", -5);

		// Act
		var result = await workflow.Execute(invalidRequest);

		// Assert
		result.IsLeft.Should().BeTrue();
		result.Left.Code.Should().Be("INVALID_VALUE");
		result.Left.Message.Should().Be("Value must be positive");
	}

	[Fact]
	public async Task Execute_WithConditionalActivity_OnlyExecutesWhenConditionMet()
	{
		// Arrange
		var workflow = new WorkflowBuilder<TestRequest, TestPayload, TestSuccess, TestError>(
			request => new TestPayload(request.Name, request.Value),
			payload => new TestSuccess(payload.Result ?? "Default")
		)
		.Do(payload => Either<TestError, TestPayload>.FromRight(
			payload with { IsValidated = true }))
		.DoIf(
			// Condition: Value is greater than 50
			payload => payload.Value > 50,
			// Activity to execute when condition is true
			payload => Either<TestError, TestPayload>.FromRight(
				payload with { Result = "High Value Processing" })
		)
		.DoIf(
			// Condition: Value is less than or equal to 50
			payload => payload.Value <= 50,
			// Activity to execute when condition is true
			payload => Either<TestError, TestPayload>.FromRight(
				payload with { Result = "Standard Processing" })
		)
		.Build();

		var lowValueRequest = new TestRequest("Test", 42);
		var highValueRequest = new TestRequest("Test", 100);

		// Act
		var lowValueResult = await workflow.Execute(lowValueRequest);
		var highValueResult = await workflow.Execute(highValueRequest);

		// Assert
		lowValueResult.Right.Result.Should().Be("Standard Processing");
		highValueResult.Right.Result.Should().Be("High Value Processing");
	}

	[Fact]
	public async Task Execute_WithErrorInActivity_ReturnsError()
	{
		// Arrange
		var workflow = new WorkflowBuilder<TestRequest, TestPayload, TestSuccess, TestError>(
			request => new TestPayload(request.Name, request.Value),
			payload => new TestSuccess(payload.Result ?? "Default")
		)
		.Do(payload =>
		{
			if (payload.Value == 0)
			{
				return Either<TestError, TestPayload>.FromLeft(
					new TestError("ZERO_VALUE", "Value cannot be zero"));
			}
			return Either<TestError, TestPayload>.FromRight(
				payload with { IsValidated = true });
		})
		.Do(payload => Either<TestError, TestPayload>.FromRight(
			payload with { IsProcessed = true }))
		.Build();

		var zeroValueRequest = new TestRequest("Test", 0);

		// Act
		var result = await workflow.Execute(zeroValueRequest);

		// Assert
		result.IsLeft.Should().BeTrue();
		result.Left.Code.Should().Be("ZERO_VALUE");
	}

	[Fact]
	public async Task Execute_WithFinallyActivities_ExecutesThemEvenOnError()
	{
		// Arrange
		bool finallyExecuted = false;

		var workflow = new WorkflowBuilder<TestRequest, TestPayload, TestSuccess, TestError>(
			request => new TestPayload(request.Name, request.Value),
			payload => new TestSuccess(payload.Result ?? "Default")
		)
		.Do(payload =>
		{
			if (payload.Value < 0)
			{
				return Either<TestError, TestPayload>.FromLeft(
					new TestError("NEGATIVE_VALUE", "Value cannot be negative"));
			}
			return Either<TestError, TestPayload>.FromRight(
				payload with { IsValidated = true });
		})
		.Finally(payload =>
		{
			finallyExecuted = true;
			return Either<TestError, TestPayload>.FromRight(payload);
		})
		.Build();

		var invalidRequest = new TestRequest("Test", -10);

		// Act
		var result = await workflow.Execute(invalidRequest);

		// Assert
		result.IsLeft.Should().BeTrue();
		finallyExecuted.Should().BeTrue();
	}
}