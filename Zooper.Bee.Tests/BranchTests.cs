using System.Threading.Tasks;
using FluentAssertions;
using Xunit;
using Zooper.Fox;

namespace Zooper.Bee.Tests;

public class BranchTests
{
	#region Test Models
	// Request model
	private record TestRequest(string Name, int Value, string Category);

	// Payload model
	private record TestPayload(
		string Name,
		int Value,
		string Category,
		bool IsStandardProcessed = false,
		bool IsPremiumProcessed = false,
		string? ProcessingResult = null);

	// Success result model
	private record TestSuccess(string Name, string ProcessingResult);

	// Error model
	private record TestError(string Code, string Message);
	#endregion

	[Fact]
	public async Task Branch_ExecutesWhenConditionIsTrue()
	{
		// Arrange
		var workflow = new WorkflowBuilder<TestRequest, TestPayload, TestSuccess, TestError>(
			request => new TestPayload(request.Name, request.Value, request.Category),
			payload => new TestSuccess(payload.Name, payload.ProcessingResult ?? "Not processed")
		)
		.Do(payload => Either<TestError, TestPayload>.FromRight(payload))
		.Group(
			// Condition: Category is Premium
			payload => payload.Category == "Premium",
			branch => branch
				.Do(payload =>
				{
					var processed = payload with
					{
						IsPremiumProcessed = true,
						ProcessingResult = "Premium Processing"
					};
					return Either<TestError, TestPayload>.FromRight(processed);
				})
		)
		.Group(
			// Condition: Category is Standard
			payload => payload.Category == "Standard",
			branch => branch
				.Do(payload =>
				{
					var processed = payload with
					{
						IsStandardProcessed = true,
						ProcessingResult = "Standard Processing"
					};
					return Either<TestError, TestPayload>.FromRight(processed);
				})
		)
		.Build();

		var premiumRequest = new TestRequest("Premium Test", 100, "Premium");
		var standardRequest = new TestRequest("Standard Test", 50, "Standard");

		// Act
		var premiumResult = await workflow.Execute(premiumRequest);
		var standardResult = await workflow.Execute(standardRequest);

		// Assert
		premiumResult.IsRight.Should().BeTrue();
		premiumResult.Right.ProcessingResult.Should().Be("Premium Processing");

		standardResult.IsRight.Should().BeTrue();
		standardResult.Right.ProcessingResult.Should().Be("Standard Processing");
	}

	[Fact]
	public async Task Branch_SkipsWhenConditionIsFalse()
	{
		// Arrange
		var workflow = new WorkflowBuilder<TestRequest, TestPayload, TestSuccess, TestError>(
			request => new TestPayload(request.Name, request.Value, request.Category),
			payload => new TestSuccess(payload.Name, payload.ProcessingResult ?? "Not processed")
		)
		.Do(payload => Either<TestError, TestPayload>.FromRight(
			payload with { ProcessingResult = "Initial Processing" }))
		.Group(
			// Condition: Category is Premium and Value is over 1000
			payload => payload.Category == "Premium" && payload.Value > 1000,
			branch => branch
				.Do(payload => Either<TestError, TestPayload>.FromRight(
					payload with
					{
						ProcessingResult = "VIP Processing"
					}))
		)
		.Build();

		var premiumRequest = new TestRequest("Premium Test", 500, "Premium"); // Doesn't meet Value > 1000 condition

		// Act
		var result = await workflow.Execute(premiumRequest);

		// Assert
		result.IsRight.Should().BeTrue();
		result.Right.ProcessingResult.Should().Be("Initial Processing");
	}

	[Fact]
	public async Task Branch_UnconditionalBranch_AlwaysExecutes()
	{
		// Arrange
		var workflow = new WorkflowBuilder<TestRequest, TestPayload, TestSuccess, TestError>(
			request => new TestPayload(request.Name, request.Value, request.Category),
			payload => new TestSuccess(payload.Name, payload.ProcessingResult ?? "Not processed")
		)
		.Group(
			branch => branch
				.Do(payload => Either<TestError, TestPayload>.FromRight(
					payload with { ProcessingResult = "Always Processed" }))
		)
		.Build();

		var anyRequest = new TestRequest("Test", 50, "Any");

		// Act
		var result = await workflow.Execute(anyRequest);

		// Assert
		result.IsRight.Should().BeTrue();
		result.Right.ProcessingResult.Should().Be("Always Processed");
	}

	[Fact]
	public async Task Branch_MultipleBranches_CorrectlyExecutes()
	{
		// Arrange
		var workflow = new WorkflowBuilder<TestRequest, TestPayload, TestSuccess, TestError>(
			request => new TestPayload(request.Name, request.Value, request.Category),
			payload => new TestSuccess(payload.Name, payload.ProcessingResult ?? "Not processed")
		)
		.Do(payload => Either<TestError, TestPayload>.FromRight(
			payload with { ProcessingResult = "Initial" }))
		.Group(
			// First branch - based on Category
			payload => payload.Category == "Premium",
			branch => branch
				.Do(payload => Either<TestError, TestPayload>.FromRight(
					payload with { ProcessingResult = payload.ProcessingResult + " + Premium" }))
		)
		.Group(
			// Second branch - based on Value
			payload => payload.Value > 75,
			branch => branch
				.Do(payload => Either<TestError, TestPayload>.FromRight(
					payload with { ProcessingResult = payload.ProcessingResult + " + High Value" }))
		)
		.Group(
			// Third branch - always executes
			branch => branch
				.Do(payload => Either<TestError, TestPayload>.FromRight(
					payload with { ProcessingResult = payload.ProcessingResult + " + Standard" }))
		)
		.Build();

		var premiumHighValueRequest = new TestRequest("Premium High Value", 100, "Premium");

		// Act
		var result = await workflow.Execute(premiumHighValueRequest);

		// Assert
		result.IsRight.Should().BeTrue();
		// All three branches should have executed in order
		result.Right.ProcessingResult.Should().Be("Initial + Premium + High Value + Standard");
	}

	[Fact]
	public async Task Branch_WithError_StopsExecutionAndReturnsError()
	{
		// Arrange
		var workflow = new WorkflowBuilder<TestRequest, TestPayload, TestSuccess, TestError>(
			request => new TestPayload(request.Name, request.Value, request.Category),
			payload => new TestSuccess(payload.Name, payload.ProcessingResult ?? "Not processed")
		)
		.Group(
			payload => payload.Category == "Premium",
			branch => branch
				.Do(payload =>
				{
					if (payload.Value <= 0)
					{
						return Either<TestError, TestPayload>.FromLeft(
							new TestError("INVALID_PREMIUM_VALUE", "Premium value must be positive"));
					}
					return Either<TestError, TestPayload>.FromRight(
						payload with { ProcessingResult = "Premium Processing" });
				})
		)
		.Group(
			branch => branch
				.Do(payload => Either<TestError, TestPayload>.FromRight(
					payload with { ProcessingResult = "Final Processing" }))
		)
		.Build();

		var invalidPremiumRequest = new TestRequest("Invalid Premium", 0, "Premium");

		// Act
		var result = await workflow.Execute(invalidPremiumRequest);

		// Assert
		result.IsLeft.Should().BeTrue();
		result.Left.Code.Should().Be("INVALID_PREMIUM_VALUE");
		// The second branch should not have executed
	}

	[Fact]
	public async Task Branch_WithMultipleActivities_ExecutesAllInOrder()
	{
		// Arrange
		var workflow = new WorkflowBuilder<TestRequest, TestPayload, TestSuccess, TestError>(
			request => new TestPayload(request.Name, request.Value, request.Category),
			payload => new TestSuccess(payload.Name, payload.ProcessingResult ?? "Not processed")
		)
		.Group(
			payload => true,
			branch => branch
				.Do(payload => Either<TestError, TestPayload>.FromRight(
					payload with { ProcessingResult = "Step 1" }))
				.Do(payload => Either<TestError, TestPayload>.FromRight(
					payload with { ProcessingResult = payload.ProcessingResult + " -> Step 2" }))
				.Do(payload => Either<TestError, TestPayload>.FromRight(
					payload with { ProcessingResult = payload.ProcessingResult + " -> Step 3" }))
		)
		.Build();

		var request = new TestRequest("Test", 50, "Standard");

		// Act
		var result = await workflow.Execute(request);

		// Assert
		result.IsRight.Should().BeTrue();
		result.Right.ProcessingResult.Should().Be("Step 1 -> Step 2 -> Step 3");
	}
}