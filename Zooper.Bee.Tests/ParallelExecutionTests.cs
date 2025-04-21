using System;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Xunit;
using Zooper.Fox;

namespace Zooper.Bee.Tests;

public class ParallelExecutionTests
{
	#region Test Models
	// Request model
	private record TestRequest(string Id, int[] Values);

	// Main payload model
	private record TestPayload(
		string Id,
		int[] Values,
		int Sum = 0,
		int Product = 0,
		bool IsProcessed = false);

	// Success result model
	private record TestSuccess(string Id, int Sum, int Product, bool IsProcessed);

	// Error model
	private record TestError(string Code, string Message);
	#endregion

	[Fact]
	public async Task Parallel_ExecutesGroupsInParallel_CombinesResults()
	{
		// Arrange
		var workflow = new WorkflowBuilder<TestRequest, TestPayload, TestSuccess, TestError>(
			request => new TestPayload(request.Id, request.Values),
			payload => new TestSuccess(payload.Id, payload.Sum, payload.Product, payload.IsProcessed)
		)
		.Do(payload => Either<TestError, TestPayload>.FromRight(
			payload with { IsProcessed = true }))
		.Parallel(
			parallel => parallel
				// First parallel group - calculate sum
				.Group(
					group => group
						.Do(payload =>
						{
							int sum = 0;
							foreach (var value in payload.Values)
							{
								sum += value;
							}
							return Either<TestError, TestPayload>.FromRight(
								payload with { Sum = sum });
						})
				)
				// Second parallel group - calculate product
				.Group(
					group => group
						.Do(payload =>
						{
							int product = 1;
							foreach (var value in payload.Values)
							{
								product *= value;
							}
							return Either<TestError, TestPayload>.FromRight(
								payload with { Product = product });
						})
				)
		)
		.Build();

		var request = new TestRequest("test-123", new[] { 2, 3, 5 });

		// Act
		var result = await workflow.Execute(request);

		// Assert
		result.IsRight.Should().BeTrue();
		result.Right.Sum.Should().Be(10); // 2 + 3 + 5
		result.Right.Product.Should().Be(30); // 2 * 3 * 5
		result.Right.IsProcessed.Should().BeTrue();
	}

	[Fact]
	public async Task Parallel_WithConditionalGroups_OnlyExecutesMatchingGroups()
	{
		// Arrange
		var workflow = new WorkflowBuilder<TestRequest, TestPayload, TestSuccess, TestError>(
			request => new TestPayload(request.Id, request.Values),
			payload => new TestSuccess(payload.Id, payload.Sum, payload.Product, payload.IsProcessed)
		)
		.Parallel(
			parallel => parallel
				// Group that only runs when the ID starts with "sum-"
				.Group(
					// Condition
					payload => payload.Id.StartsWith("sum-"),
					group => group
						.Do(payload =>
						{
							int sum = 0;
							foreach (var value in payload.Values)
							{
								sum += value;
							}
							return Either<TestError, TestPayload>.FromRight(
								payload with { Sum = sum });
						})
				)
				// Group that only runs when the ID starts with "product-"
				.Group(
					// Condition
					payload => payload.Id.StartsWith("product-"),
					group => group
						.Do(payload =>
						{
							int product = 1;
							foreach (var value in payload.Values)
							{
								product *= value;
							}
							return Either<TestError, TestPayload>.FromRight(
								payload with { Product = product });
						})
				)
				// Group that always runs
				.Group(
					group => group
						.Do(payload => Either<TestError, TestPayload>.FromRight(
							payload with { IsProcessed = true }))
				)
		)
		.Build();

		var sumRequest = new TestRequest("sum-123", new[] { 2, 3, 5 });
		var productRequest = new TestRequest("product-456", new[] { 2, 3, 5 });
		var otherRequest = new TestRequest("other-789", new[] { 2, 3, 5 });

		// Act
		var sumResult = await workflow.Execute(sumRequest);
		var productResult = await workflow.Execute(productRequest);
		var otherResult = await workflow.Execute(otherRequest);

		// Assert
		sumResult.IsRight.Should().BeTrue();
		sumResult.Right.Sum.Should().Be(10); // 2 + 3 + 5
		sumResult.Right.Product.Should().Be(0); // Not calculated
		sumResult.Right.IsProcessed.Should().BeTrue();

		productResult.IsRight.Should().BeTrue();
		productResult.Right.Sum.Should().Be(0); // Not calculated
		productResult.Right.Product.Should().Be(30); // 2 * 3 * 5
		productResult.Right.IsProcessed.Should().BeTrue();

		otherResult.IsRight.Should().BeTrue();
		otherResult.Right.Sum.Should().Be(0); // Not calculated
		otherResult.Right.Product.Should().Be(0); // Not calculated
		otherResult.Right.IsProcessed.Should().BeTrue(); // Only the unconditional group ran
	}

	[Fact]
	public async Task Parallel_ErrorInOneGroup_StopsExecutionAndReturnsError()
	{
		// Arrange
		var workflow = new WorkflowBuilder<TestRequest, TestPayload, TestSuccess, TestError>(
			request => new TestPayload(request.Id, request.Values),
			payload => new TestSuccess(payload.Id, payload.Sum, payload.Product, payload.IsProcessed)
		)
		.Parallel(
			parallel => parallel
				// First group - calculate sum (will succeed)
				.Group(
					group => group
						.Do(payload =>
						{
							int sum = 0;
							foreach (var value in payload.Values)
							{
								sum += value;
							}
							return Either<TestError, TestPayload>.FromRight(
								payload with { Sum = sum });
						})
				)
				// Second group - will fail if values contain zero
				.Group(
					group => group
						.Do(payload =>
						{
							foreach (var value in payload.Values)
							{
								if (value == 0)
								{
									return Either<TestError, TestPayload>.FromLeft(
										new TestError("ZERO_VALUE", "Cannot process values containing zero"));
								}
							}

							int product = 1;
							foreach (var value in payload.Values)
							{
								product *= value;
							}
							return Either<TestError, TestPayload>.FromRight(
								payload with { Product = product });
						})
				)
		)
		.Build();

		var validRequest = new TestRequest("valid", new[] { 2, 3, 5 });
		var invalidRequest = new TestRequest("invalid", new[] { 2, 0, 5 });

		// Act
		var validResult = await workflow.Execute(validRequest);
		var invalidResult = await workflow.Execute(invalidRequest);

		// Assert
		validResult.IsRight.Should().BeTrue();
		validResult.Right.Sum.Should().Be(10);
		validResult.Right.Product.Should().Be(30);

		invalidResult.IsLeft.Should().BeTrue();
		invalidResult.Left.Code.Should().Be("ZERO_VALUE");
		invalidResult.Left.Message.Should().Be("Cannot process values containing zero");
	}

	[Fact]
	public async Task ParallelDetached_DetachedGroupsDoNotAffectResult()
	{
		// Arrange
		var backgroundTaskCompleted = new TaskCompletionSource<bool>();
		var syncObj = new object();
		var backgroundTaskRan = false;

		var workflow = new WorkflowBuilder<TestRequest, TestPayload, TestSuccess, TestError>(
			request => new TestPayload(request.Id, request.Values),
			payload => new TestSuccess(payload.Id, payload.Sum, payload.Product, payload.IsProcessed)
		)
		.Do(payload =>
		{
			int sum = 0;
			foreach (var value in payload.Values)
			{
				sum += value;
			}
			return Either<TestError, TestPayload>.FromRight(
				payload with { Sum = sum, IsProcessed = true });
		})
		.ParallelDetached(
			parallelDetached => parallelDetached
				.Detached(
					detachedGroup => detachedGroup
						.Do(payload =>
						{
							try
							{
								// This is a detached task, its changes should not affect the main workflow
								lock (syncObj)
								{
									backgroundTaskRan = true;
								}

								// Simulate some work
								Thread.Sleep(100);

								// This modification to Product should NOT be reflected in the final result
								int product = 1;
								foreach (var value in payload.Values)
								{
									product *= value;
								}

								backgroundTaskCompleted.SetResult(true);
								return Either<TestError, TestPayload>.FromRight(
									payload with { Product = product });
							}
							catch (Exception ex)
							{
								backgroundTaskCompleted.SetException(ex);
								throw;
							}
						})
				)
		)
		.Finally(payload =>
		{
			// Add a small delay to allow the background task to start
			Thread.Sleep(200);
			return Either<TestError, TestPayload>.FromRight(payload);
		})
		.Build();

		var request = new TestRequest("test-123", new[] { 2, 3, 5 });

		// Act
		var result = await workflow.Execute(request);

		// Wait for the background task to complete or timeout after 2 seconds
		var timeoutTask = Task.Delay(2000);
		var completedTask = await Task.WhenAny(backgroundTaskCompleted.Task, timeoutTask);
		var timedOut = completedTask == timeoutTask;

		// Assert
		result.IsRight.Should().BeTrue();
		result.Right.Sum.Should().Be(10); // 2 + 3 + 5
		result.Right.Product.Should().Be(0); // Should NOT be updated by detached group
		result.Right.IsProcessed.Should().BeTrue();

		// Verify that the background task did run (or was at least started)
		lock (syncObj)
		{
			backgroundTaskRan.Should().BeTrue();
		}

		timedOut.Should().BeFalse("Background task timed out");
	}

	[Fact]
	public async Task ParallelDetached_ErrorInDetachedGroup_DoesNotAffectMainWorkflow()
	{
		// Arrange
		var backgroundTaskCompleted = new TaskCompletionSource<bool>();
		var syncObj = new object();
		var backgroundTaskRan = false;

		var workflow = new WorkflowBuilder<TestRequest, TestPayload, TestSuccess, TestError>(
			request => new TestPayload(request.Id, request.Values),
			payload => new TestSuccess(payload.Id, payload.Sum, payload.Product, payload.IsProcessed)
		)
		.Do(payload =>
		{
			int sum = 0;
			foreach (var value in payload.Values)
			{
				sum += value;
			}
			return Either<TestError, TestPayload>.FromRight(
				payload with { Sum = sum, IsProcessed = true });
		})
		.ParallelDetached(
			parallelDetached => parallelDetached
				.Detached(
					detachedGroup => detachedGroup
						.Do(payload =>
						{
							try
							{
								lock (syncObj)
								{
									backgroundTaskRan = true;
								}

								// Simulate some work
								Thread.Sleep(100);

								// This error should NOT affect the main workflow
								backgroundTaskCompleted.SetResult(true);
								return Either<TestError, TestPayload>.FromLeft(
									new TestError("BACKGROUND_ERROR", "This error occurs in background"));
							}
							catch (Exception ex)
							{
								backgroundTaskCompleted.SetException(ex);
								throw;
							}
						})
				)
		)
		.Finally(payload =>
		{
			// Add a small delay to allow the background task to start
			Thread.Sleep(200);
			return Either<TestError, TestPayload>.FromRight(payload);
		})
		.Build();

		var request = new TestRequest("test-123", new[] { 2, 3, 5 });

		// Act
		var result = await workflow.Execute(request);

		// Wait for the background task to complete or timeout after 2 seconds
		var timeoutTask = Task.Delay(2000);
		var completedTask = await Task.WhenAny(backgroundTaskCompleted.Task, timeoutTask);
		var timedOut = completedTask == timeoutTask;

		// Assert
		result.IsRight.Should().BeTrue(); // Main workflow should succeed
		result.Right.Sum.Should().Be(10);
		result.Right.IsProcessed.Should().BeTrue();

		// Verify that the background task did run
		lock (syncObj)
		{
			backgroundTaskRan.Should().BeTrue();
		}

		timedOut.Should().BeFalse("Background task timed out");
	}
}