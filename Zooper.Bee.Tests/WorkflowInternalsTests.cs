using System;
using System.Threading.Tasks;
using FluentAssertions;
using Xunit;
using Zooper.Fox;

namespace Zooper.Bee.Tests;

/// <summary>
/// Tests for the internal execution logic of workflows using end-to-end tests.
/// </summary>
public class WorkflowInternalsTests
{
	#region Test Models
	// Models for the tests
	private record TestRequest(string Name, int Value);
	private record TestPayload(string Name, int Value, string? Result = null);
	private record TestLocalPayload(string LocalData, int ProcessingValue = 0);
	private record TestSuccess(string Result);
	private record TestError(string Code, string Message);
	#endregion

	[Fact]
	public async Task DynamicBranchExecution_ConditionTrue_ExecutesActivities()
	{
		// Arrange
		var workflow = new WorkflowBuilder<TestRequest, TestPayload, TestSuccess, TestError>(
			// Create the payload from the request
			request => new TestPayload(request.Name, request.Value),

			// Create the success result from the payload
			payload => new TestSuccess(payload.Result ?? "No result")
		)
		.Do(payload => Either<TestError, TestPayload>.FromRight(
			payload with { Result = "Initial processing" }))
		.BranchWithLocalPayload(
			// Condition - always true
			payload => true,

			// Create local payload
			payload => new TestLocalPayload($"Local data for {payload.Name}"),

			// Branch configuration
			branch => branch
				.Do((mainPayload, localPayload) =>
				{
					var updatedMainPayload = mainPayload with
					{
						Result = $"Processed {mainPayload.Name} with {localPayload.LocalData}"
					};
					var updatedLocalPayload = localPayload with { ProcessingValue = 42 };

					return Either<TestError, (TestPayload, TestLocalPayload)>.FromRight(
						(updatedMainPayload, updatedLocalPayload));
				})
		)
		.Build();

		var request = new TestRequest("TestName", 123);

		// Act
		var result = await workflow.Execute(request);

		// Assert
		result.IsRight.Should().BeTrue();
		result.Right.Result.Should().Be("Processed TestName with Local data for TestName");
	}

	[Fact]
	public async Task DynamicBranchExecution_ConditionFalse_SkipsActivities()
	{
		// Arrange
		var workflow = new WorkflowBuilder<TestRequest, TestPayload, TestSuccess, TestError>(
			request => new TestPayload(request.Name, request.Value),
			payload => new TestSuccess(payload.Result ?? "No result")
		)
		.Do(payload => Either<TestError, TestPayload>.FromRight(
			payload with { Result = "Initial processing" }))
		.BranchWithLocalPayload(
			// Condition - always false
			payload => false,

			// Create local payload
			payload => new TestLocalPayload($"Local data for {payload.Name}"),

			// Branch configuration
			branch => branch
				.Do((mainPayload, localPayload) =>
				{
					var updatedMainPayload = mainPayload with
					{
						Result = "This should not be set"
					};

					return Either<TestError, (TestPayload, TestLocalPayload)>.FromRight(
						(updatedMainPayload, localPayload));
				})
		)
		.Build();

		var request = new TestRequest("TestName", 123);

		// Act
		var result = await workflow.Execute(request);

		// Assert
		result.IsRight.Should().BeTrue();
		result.Right.Result.Should().Be("Initial processing"); // Should remain unchanged
	}

	[Fact]
	public async Task DynamicBranchExecution_ActivityReturnsError_PropagatesError()
	{
		// Arrange
		var workflow = new WorkflowBuilder<TestRequest, TestPayload, TestSuccess, TestError>(
			request => new TestPayload(request.Name, request.Value),
			payload => new TestSuccess(payload.Result ?? "No result")
		)
		.BranchWithLocalPayload(
			// Condition - always true
			payload => true,

			// Create local payload
			payload => new TestLocalPayload($"Local data for {payload.Name}"),

			// Branch configuration
			branch => branch
				.Do((mainPayload, localPayload) =>
				{
					return Either<TestError, (TestPayload, TestLocalPayload)>.FromLeft(
						new TestError("TEST_ERROR", "This is a test error"));
				})
		)
		.Build();

		var request = new TestRequest("TestName", 123);

		// Act
		var result = await workflow.Execute(request);

		// Assert
		result.IsLeft.Should().BeTrue();
		result.Left.Code.Should().Be("TEST_ERROR");
		result.Left.Message.Should().Be("This is a test error");
	}

	[Fact]
	public async Task DynamicBranchExecution_MultipleActivities_ExecutesInOrder()
	{
		// Arrange
		var workflow = new WorkflowBuilder<TestRequest, TestPayload, TestSuccess, TestError>(
			request => new TestPayload(request.Name, request.Value),
			payload => new TestSuccess(payload.Result ?? "No result")
		)
		.BranchWithLocalPayload(
			// Condition - always true
			payload => true,

			// Create local payload
			payload => new TestLocalPayload("Initial local data", 0),

			// Branch configuration
			branch => branch
				// First activity
				.Do((mainPayload, localPayload) =>
				{
					var updatedMainPayload = mainPayload with { Value = mainPayload.Value + 1 };
					var updatedLocalPayload = localPayload with
					{
						LocalData = localPayload.LocalData + " -> Step 1",
						ProcessingValue = 10
					};

					return Either<TestError, (TestPayload, TestLocalPayload)>.FromRight(
						(updatedMainPayload, updatedLocalPayload));
				})
				// Second activity
				.Do((mainPayload, localPayload) =>
				{
					var updatedMainPayload = mainPayload with
					{
						Value = mainPayload.Value + localPayload.ProcessingValue,
						Result = $"Result: {localPayload.LocalData}"
					};

					var updatedLocalPayload = localPayload with
					{
						LocalData = localPayload.LocalData + " -> Step 2",
						ProcessingValue = 20
					};

					return Either<TestError, (TestPayload, TestLocalPayload)>.FromRight(
						(updatedMainPayload, updatedLocalPayload));
				})
		)
		.Build();

		var request = new TestRequest("TestName", 5);

		// Act
		var result = await workflow.Execute(request);

		// Assert
		result.IsRight.Should().BeTrue();
		result.Right.Result.Should().Be("Result: Initial local data -> Step 1");
	}

	[Fact]
	public async Task DynamicBranchExecution_MultipleBranches_ExecuteIndependently()
	{
		// Arrange
		var workflow = new WorkflowBuilder<TestRequest, TestPayload, TestSuccess, TestError>(
			request => new TestPayload(request.Name, request.Value),
			payload => new TestSuccess(payload.Result ?? "No result")
		)
		.Do(payload => Either<TestError, TestPayload>.FromRight(
			payload with { Result = "Start" }))
		// First branch with first local payload type
		.BranchWithLocalPayload(
			payload => true,
			payload => new TestLocalPayload("Branch 1 data"),
			branch => branch
				.Do((mainPayload, localPayload) =>
				{
					var updatedMainPayload = mainPayload with
					{
						Result = mainPayload.Result + " -> Branch 1"
					};

					return Either<TestError, (TestPayload, TestLocalPayload)>.FromRight(
						(updatedMainPayload, localPayload));
				})
		)
		// Second branch with the same local payload type
		.BranchWithLocalPayload(
			payload => payload.Value > 0,
			payload => new TestLocalPayload("Branch 2 data"),
			branch => branch
				.Do((mainPayload, localPayload) =>
				{
					var updatedMainPayload = mainPayload with
					{
						Result = mainPayload.Result + " -> Branch 2"
					};

					return Either<TestError, (TestPayload, TestLocalPayload)>.FromRight(
						(updatedMainPayload, localPayload));
				})
		)
		.Build();

		var request = new TestRequest("TestName", 5);

		// Act
		var result = await workflow.Execute(request);

		// Assert
		result.IsRight.Should().BeTrue();
		result.Right.Result.Should().Be("Start -> Branch 1 -> Branch 2");
	}
}