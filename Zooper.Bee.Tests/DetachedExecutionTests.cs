using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Xunit;
using Zooper.Fox;

namespace Zooper.Bee.Tests;

public class DetachedExecutionTests
{
	#region Test Models
	// Request model
	private record NotificationRequest(string UserId, string Message, bool IsUrgent);

	// Main payload model
	private record NotificationPayload(
		string UserId,
		string Message,
		bool IsUrgent,
		bool IsProcessed = false,
		string Status = "Pending");

	// Success result model
	private record NotificationResult(string UserId, string Status);

	// Error model
	private record NotificationError(string Code, string Message);
	#endregion

	[Fact]
	public async Task Detached_ExecutesInBackground_DoesNotAffectMainWorkflow()
	{
		// Arrange
		var backgroundTaskCompleted = new TaskCompletionSource<bool>();
		var syncObj = new object();
		var backgroundTaskRan = false;

		var workflow = new WorkflowBuilder<NotificationRequest, NotificationPayload, NotificationResult, NotificationError>(
			request => new NotificationPayload(request.UserId, request.Message, request.IsUrgent),
			payload => new NotificationResult(payload.UserId, payload.Status)
		)
		.Do(payload =>
		{
			// Main workflow processing
			return Either<NotificationError, NotificationPayload>.FromRight(
				payload with
				{
					IsProcessed = true,
					Status = "Processed"
				});
		})
		// Detached execution that won't affect the main workflow result
		.Detach(
			detached => detached
				.Do(payload =>
				{
					try
					{
						// This task runs in the background
						lock (syncObj)
						{
							backgroundTaskRan = true;
						}

						// Simulate some work
						Thread.Sleep(100);

						// In a real application, this might send an email or log to a database
						Console.WriteLine($"Background notification sent to: {payload.UserId}");

						// This Status change should NOT affect the main workflow result
						backgroundTaskCompleted.SetResult(true);
						return Either<NotificationError, NotificationPayload>.FromRight(
							payload with { Status = "Background task executed" });
					}
					catch (Exception ex)
					{
						backgroundTaskCompleted.SetException(ex);
						throw;
					}
				})
		)
		.Finally(payload =>
		{
			// Add a small delay to allow the background task to start
			Thread.Sleep(200);
			return Either<NotificationError, NotificationPayload>.FromRight(payload);
		})
		.Build();

		var request = new NotificationRequest("user-123", "Test message", false);

		// Act
		var result = await workflow.Execute(request);

		// Wait for the background task to complete or timeout after 2 seconds
		var timeoutTask = Task.Delay(2000);
		var completedTask = await Task.WhenAny(backgroundTaskCompleted.Task, timeoutTask);
		var timedOut = completedTask == timeoutTask;

		// Assert
		result.IsRight.Should().BeTrue();
		result.Right.Status.Should().Be("Processed"); // Should have the status from the main workflow

		// Verify the background task ran
		lock (syncObj)
		{
			backgroundTaskRan.Should().BeTrue();
		}

		timedOut.Should().BeFalse("Background task timed out");
	}

	[Fact]
	public async Task Detached_WithCondition_OnlyExecutesWhenConditionIsTrue()
	{
		// Arrange
		var urgentTaskCompleted = new TaskCompletionSource<bool>();
		var regularTaskCompleted = new TaskCompletionSource<bool>();
		var syncObj = new object();
		var urgentTaskRan = false;
		var regularTaskRan = false;

		var workflow = new WorkflowBuilder<NotificationRequest, NotificationPayload, NotificationResult, NotificationError>(
			request => new NotificationPayload(request.UserId, request.Message, request.IsUrgent),
			payload => new NotificationResult(payload.UserId, payload.Status)
		)
		.Do(payload =>
		{
			// Main workflow processing
			return Either<NotificationError, NotificationPayload>.FromRight(
				payload with
				{
					IsProcessed = true,
					Status = "Processed"
				});
		})
		// Conditional detached execution for urgent notifications
		.Detach(
			// Only execute for urgent notifications
			payload => payload.IsUrgent,
			detached => detached
				.Do(payload =>
				{
					try
					{
						lock (syncObj)
						{
							urgentTaskRan = true;
						}

						// Simulate some work
						Thread.Sleep(100);

						Console.WriteLine($"URGENT notification sent to: {payload.UserId}");

						urgentTaskCompleted.SetResult(true);
						return Either<NotificationError, NotificationPayload>.FromRight(payload);
					}
					catch (Exception ex)
					{
						urgentTaskCompleted.SetException(ex);
						throw;
					}
				})
		)
		// Unconditional detached execution for all notifications
		.Detach(
			detached => detached
				.Do(payload =>
				{
					try
					{
						lock (syncObj)
						{
							regularTaskRan = true;
						}

						// Simulate some work
						Thread.Sleep(100);

						Console.WriteLine($"Regular notification processing for: {payload.UserId}");

						regularTaskCompleted.SetResult(true);
						return Either<NotificationError, NotificationPayload>.FromRight(payload);
					}
					catch (Exception ex)
					{
						regularTaskCompleted.SetException(ex);
						throw;
					}
				})
		)
		.Finally(payload =>
		{
			// Add a small delay to allow the background task to start
			Thread.Sleep(200);
			return Either<NotificationError, NotificationPayload>.FromRight(payload);
		})
		.Build();

		// Act & Assert for urgent request
		var urgentRequest = new NotificationRequest("user-urgent", "Urgent message", true);
		var urgentResult = await workflow.Execute(urgentRequest);

		// Wait for the background tasks to complete or timeout
		var timeoutTask = Task.Delay(2000);
		await Task.WhenAny(
			Task.WhenAll(urgentTaskCompleted.Task, regularTaskCompleted.Task),
			timeoutTask);

		urgentResult.IsRight.Should().BeTrue();
		urgentResult.Right.Status.Should().Be("Processed");

		lock (syncObj)
		{
			urgentTaskRan.Should().BeTrue(); // Urgent task should run for urgent requests
			regularTaskRan.Should().BeTrue(); // Regular task should run for all requests
		}

		// Reset for next test
		urgentTaskCompleted = new TaskCompletionSource<bool>();
		regularTaskCompleted = new TaskCompletionSource<bool>();
		lock (syncObj)
		{
			urgentTaskRan = false;
			regularTaskRan = false;
		}

		// Act & Assert for regular request
		var regularRequest = new NotificationRequest("user-regular", "Regular message", false);
		var regularResult = await workflow.Execute(regularRequest);

		// Wait for the background tasks to complete or timeout
		timeoutTask = Task.Delay(2000);
		await Task.WhenAny(regularTaskCompleted.Task, timeoutTask);

		regularResult.IsRight.Should().BeTrue();
		regularResult.Right.Status.Should().Be("Processed");

		lock (syncObj)
		{
			urgentTaskRan.Should().BeFalse(); // Urgent task should NOT run for regular requests
			regularTaskRan.Should().BeTrue();  // Regular task should run for all requests
		}
	}

	[Fact]
	public async Task Detached_WithMultipleActivities_ExecutesAllInOrder()
	{
		// Arrange
		var detachedTasksCompleted = new TaskCompletionSource<bool>();
		var syncObj = new object();
		var executionOrder = new List<string>();

		var workflow = new WorkflowBuilder<NotificationRequest, NotificationPayload, NotificationResult, NotificationError>(
			request => new NotificationPayload(request.UserId, request.Message, request.IsUrgent),
			payload => new NotificationResult(payload.UserId, payload.Status)
		)
		.Do(payload =>
		{
			// Main workflow processing
			lock (syncObj)
			{
				executionOrder.Add("Main");
			}

			return Either<NotificationError, NotificationPayload>.FromRight(
				payload with { IsProcessed = true, Status = "Processed" });
		})
		// Detached execution with multiple activities
		.Detach(
			detached => detached
				.Do(payload =>
				{
					try
					{
						// First detached activity
						lock (syncObj)
						{
							executionOrder.Add("Detached1");
						}

						// Simulate some work
						Thread.Sleep(50);

						return Either<NotificationError, NotificationPayload>.FromRight(payload);
					}
					catch (Exception)
					{
						detachedTasksCompleted.SetException(new Exception("Failed in Detached1"));
						throw;
					}
				})
				.Do(payload =>
				{
					try
					{
						// Second detached activity - should run after the first one
						lock (syncObj)
						{
							executionOrder.Add("Detached2");
						}

						// Simulate some work
						Thread.Sleep(50);

						return Either<NotificationError, NotificationPayload>.FromRight(payload);
					}
					catch (Exception)
					{
						detachedTasksCompleted.SetException(new Exception("Failed in Detached2"));
						throw;
					}
				})
				.Do(payload =>
				{
					try
					{
						// Third detached activity - should run after the second one
						lock (syncObj)
						{
							executionOrder.Add("Detached3");
						}

						// Notify that all detached tasks completed
						detachedTasksCompleted.SetResult(true);

						return Either<NotificationError, NotificationPayload>.FromRight(payload);
					}
					catch (Exception ex)
					{
						detachedTasksCompleted.SetException(ex);
						throw;
					}
				})
		)
		.Finally(payload =>
		{
			// Add a small delay to allow the background task to start
			Thread.Sleep(200);
			return Either<NotificationError, NotificationPayload>.FromRight(payload);
		})
		.Build();

		var request = new NotificationRequest("user-123", "Test message", false);

		// Act
		var result = await workflow.Execute(request);

		// Wait for the detached tasks to complete or timeout after 2 seconds
		var timeoutTask = Task.Delay(2000);
		var completedTask = await Task.WhenAny(detachedTasksCompleted.Task, timeoutTask);
		var timedOut = completedTask == timeoutTask;

		// Assert
		result.IsRight.Should().BeTrue();
		timedOut.Should().BeFalse("Detached tasks timed out");

		// Lock to access shared state
		List<string> capturedOrder;
		lock (syncObj)
		{
			capturedOrder = new List<string>(executionOrder);
		}

		// Check that the main activity executed first
		capturedOrder[0].Should().Be("Main");

		// Check that the detached activities executed in order relative to each other
		// We need to find the indices of each detached activity
		int detached1Index = capturedOrder.IndexOf("Detached1");
		int detached2Index = capturedOrder.IndexOf("Detached2");
		int detached3Index = capturedOrder.IndexOf("Detached3");

		// All detached activities should be found
		detached1Index.Should().BeGreaterThan(0, "Detached1 should have executed");
		detached2Index.Should().BeGreaterThan(0, "Detached2 should have executed");
		detached3Index.Should().BeGreaterThan(0, "Detached3 should have executed");

		// Check the order
		detached1Index.Should().BeLessThan(detached2Index, "Detached1 should execute before Detached2");
		detached2Index.Should().BeLessThan(detached3Index, "Detached2 should execute before Detached3");
	}
}