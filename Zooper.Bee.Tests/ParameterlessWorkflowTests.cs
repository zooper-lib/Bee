using System;
using System.Threading.Tasks;
using FluentAssertions;
using Xunit;
using Zooper.Fox;
using Zooper.Bee.Extensions;

namespace Zooper.Bee.Tests;

public class ParameterlessWorkflowTests
{
	#region Test Models

	// Payload model for tests
	private record TestPayload(DateTime StartTime, string Status = "Waiting");

	// Success result model
	private record TestSuccess(string Status, bool IsComplete);

	// Error model
	private record TestError(string Code, string Message);

	#endregion

	[Fact]
	public async Task ParameterlessWorkflow_UsingUnitType_CanBeExecuted()
	{
		// Arrange
		var workflow = new WorkflowBuilder<Unit, TestPayload, TestSuccess, TestError>(
				// Convert Unit to initial payload
				_ => new TestPayload(DateTime.UtcNow),

				// Convert final payload to success result
				payload => new TestSuccess(payload.Status, true)
			)
			.Do(payload => Either<TestError, TestPayload>.FromRight(
					payload with
					{
						Status = "Processing"
					}
				)
			)
			.Do(payload => Either<TestError, TestPayload>.FromRight(
					payload with
					{
						Status = "Completed"
					}
				)
			)
			.Build();

		// Act
		var result = await workflow.Execute(Unit.Value);

		// Assert
		result.IsRight.Should().BeTrue();
		result.Right.Status.Should().Be("Completed");
		result.Right.IsComplete.Should().BeTrue();
	}

	[Fact]
	public async Task ParameterlessWorkflow_UsingFactory_CanBeExecuted()
	{
		// Arrange
		var workflow = WorkflowBuilderFactory.CreateWorkflow<TestPayload, TestSuccess, TestError>(
			// Initial payload factory
			() => new TestPayload(DateTime.UtcNow),

			// Result selector
			payload => new TestSuccess(payload.Status, true),

			// Configure the workflow
			builder => builder
				.Do(payload => Either<TestError, TestPayload>.FromRight(
						payload with
						{
							Status = "Processing"
						}
					)
				)
				.Do(payload => Either<TestError, TestPayload>.FromRight(
						payload with
						{
							Status = "Completed"
						}
					)
				)
		);

		// Act
		var result = await workflow.Execute();

		// Assert
		result.IsRight.Should().BeTrue();
		result.Right.Status.Should().Be("Completed");
		result.Right.IsComplete.Should().BeTrue();
	}

	[Fact]
	public async Task ParameterlessWorkflow_UsingExtensionMethod_CanBeExecuted()
	{
		// Arrange
		var workflow = new WorkflowBuilder<Unit, TestPayload, TestSuccess, TestError>(
				_ => new TestPayload(DateTime.UtcNow),
				payload => new TestSuccess(payload.Status, true)
			)
			.Do(payload => Either<TestError, TestPayload>.FromRight(
					payload with
					{
						Status = "Processing"
					}
				)
			)
			.Do(payload => Either<TestError, TestPayload>.FromRight(
					payload with
					{
						Status = "Completed"
					}
				)
			)
			.Build();

		// Act - using extension method (no parameters)
		var result = await workflow.Execute();

		// Assert
		result.IsRight.Should().BeTrue();
		result.Right.Status.Should().Be("Completed");
		result.Right.IsComplete.Should().BeTrue();
	}

	[Fact]
	public async Task ParameterlessWorkflow_WithError_ReturnsError()
	{
		// Arrange
		var workflow = WorkflowBuilderFactory.Create<TestPayload, TestSuccess, TestError>(
				() => new TestPayload(DateTime.UtcNow),
				payload => new TestSuccess(payload.Status, true)
			)
			.Do(payload => Either<TestError, TestPayload>.FromRight(
					payload with
					{
						Status = "Processing"
					}
				)
			)
			.Do(payload =>
				{
					// Simulate an error in the workflow
					return Either<TestError, TestPayload>.FromLeft(
						new TestError("PROCESSING_FAILED", "Failed to complete processing")
					);
				}
			)
			.Build();

		// Act
		var result = await workflow.Execute();

		// Assert
		result.IsLeft.Should().BeTrue();
		result.Left.Code.Should().Be("PROCESSING_FAILED");
		result.Left.Message.Should().Be("Failed to complete processing");
	}
}