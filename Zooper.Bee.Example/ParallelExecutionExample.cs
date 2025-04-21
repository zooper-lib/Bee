using Zooper.Fox;

namespace Zooper.Bee.Example;

public class ParallelExecutionExample
{
	// Request model
	public record DataProcessingRequest(string DataId, string[] Segments, bool NotifyOnCompletion);

	// Success model
	public record DataProcessingResult(string DataId, int ProcessedSegments, DateTime CompletedAt);

	// Error model
	public record DataProcessingError(string Code, string Message);

	// Main payload model
	public record DataProcessingPayload(
		string DataId,
		string[] Segments,
		bool NotifyOnCompletion,
		int ProcessedSegments = 0,
		bool Validated = false,
		DateTime? CompletedAt = null);

	public static async Task RunExample()
	{
		Console.WriteLine("\n=== Parallel Execution Example ===\n");

		// Create a sample request
		var request = new DataProcessingRequest(
			"DATA-12345",
			new[] { "Segment1", "Segment2", "Segment3", "Segment4" },
			true
		);

		// Build the workflows
		var parallelWorkflow = CreateParallelWorkflow();
		var parallelDetachedWorkflow = CreateParallelDetachedWorkflow();

		// Process with parallel execution
		Console.WriteLine("Processing with parallel execution:");
		await ProcessData(parallelWorkflow, request);

		Console.WriteLine();

		// Process with parallel detached execution
		Console.WriteLine("Processing with parallel detached execution:");
		await ProcessData(parallelDetachedWorkflow, request);
	}

	private static async Task ProcessData(
		Workflow<DataProcessingRequest, DataProcessingResult, DataProcessingError> workflow,
		DataProcessingRequest request)
	{
		var result = await workflow.Execute(request);

		if (result.IsRight)
		{
			var success = result.Right;
			Console.WriteLine($"Data {success.DataId} processed successfully");
			Console.WriteLine($"Processed {success.ProcessedSegments} segments");
			Console.WriteLine($"Completed at: {success.CompletedAt}");
		}
		else
		{
			var error = result.Left;
			Console.WriteLine($"Data processing failed: [{error.Code}] {error.Message}");
		}
	}

	private static Workflow<DataProcessingRequest, DataProcessingResult, DataProcessingError> CreateParallelWorkflow()
	{
		return new WorkflowBuilder<DataProcessingRequest, DataProcessingPayload, DataProcessingResult, DataProcessingError>(
			// Create initial payload from request
			request => new DataProcessingPayload(
				request.DataId,
				request.Segments,
				request.NotifyOnCompletion),

			// Create result from final payload
			payload => new DataProcessingResult(
				payload.DataId,
				payload.ProcessedSegments,
				payload.CompletedAt ?? DateTime.UtcNow)
		)
		.Do(payload =>
		{
			Console.WriteLine($"Preparing to process data {payload.DataId}...");

			// Validate data
			return Either<DataProcessingError, DataProcessingPayload>.FromRight(
				payload with { Validated = true });
		})
		// Use parallel execution to process segments in parallel
		.Parallel(
			// Configure parallel execution groups
			parallel => parallel
				// First parallel group - process first half of segments
				.Group(
					// Create a group for the first half
					group => group
						.Do(payload =>
						{
							var halfwayPoint = payload.Segments.Length / 2;
							var firstHalf = payload.Segments[..halfwayPoint];

							Console.WriteLine($"Processing first half ({firstHalf.Length} segments) in parallel...");
							// Simulate processing time
							Task.Delay(500).GetAwaiter().GetResult();

							return Either<DataProcessingError, DataProcessingPayload>.FromRight(
								payload with { ProcessedSegments = payload.ProcessedSegments + firstHalf.Length });
						})
				)
				// Second parallel group - process second half of segments
				.Group(
					// Create a group for the second half
					group => group
						.Do(payload =>
						{
							var halfwayPoint = payload.Segments.Length / 2;
							var secondHalf = payload.Segments[halfwayPoint..];

							Console.WriteLine($"Processing second half ({secondHalf.Length} segments) in parallel...");
							// Simulate processing time
							Task.Delay(300).GetAwaiter().GetResult();

							return Either<DataProcessingError, DataProcessingPayload>.FromRight(
								payload with { ProcessedSegments = payload.ProcessedSegments + secondHalf.Length });
						})
				)
		)
		// Finalize the processing
		.Do(payload =>
		{
			Console.WriteLine($"Finalizing data processing for {payload.DataId}...");
			var completedAt = DateTime.UtcNow;

			return Either<DataProcessingError, DataProcessingPayload>.FromRight(
				payload with { CompletedAt = completedAt });
		})
		// Send notification if requested
		.DoIf(
			payload => payload.NotifyOnCompletion,
			payload =>
			{
				Console.WriteLine($"Sending completion notification for data {payload.DataId}...");
				return Either<DataProcessingError, DataProcessingPayload>.FromRight(payload);
			}
		)
		.Build();
	}

	private static Workflow<DataProcessingRequest, DataProcessingResult, DataProcessingError> CreateParallelDetachedWorkflow()
	{
		return new WorkflowBuilder<DataProcessingRequest, DataProcessingPayload, DataProcessingResult, DataProcessingError>(
			// Create initial payload from request
			request => new DataProcessingPayload(
				request.DataId,
				request.Segments,
				request.NotifyOnCompletion),

			// Create result from final payload
			payload => new DataProcessingResult(
				payload.DataId,
				payload.ProcessedSegments,
				payload.CompletedAt ?? DateTime.UtcNow)
		)
		.Do(payload =>
		{
			Console.WriteLine($"Preparing to process data {payload.DataId} with detached parallel execution...");

			// Since detached execution doesn't wait for results, we'll count all segments as processed
			// in the main workflow
			return Either<DataProcessingError, DataProcessingPayload>.FromRight(
				payload with
				{
					Validated = true,
					ProcessedSegments = payload.Segments.Length
				});
		})
		// Use parallel detached execution for background tasks
		.ParallelDetached(
			// Configure parallel detached execution groups
			parallelDetached => parallelDetached
				// First background task - log processing start
				.Detached(
					group => group
						.Do(payload =>
						{
							Console.WriteLine($"BACKGROUND: Logging processing start for {payload.DataId}...");
							// Simulate logging delay
							Task.Delay(200).GetAwaiter().GetResult();
							Console.WriteLine($"BACKGROUND: Logging completed for {payload.DataId}");

							return Either<DataProcessingError, DataProcessingPayload>.FromRight(payload);
						})
				)
				// Second background task - generate analytics
				.Detached(
					group => group
						.Do(payload =>
						{
							Console.WriteLine($"BACKGROUND: Generating analytics for {payload.DataId}...");
							// Simulate analytics generation
							Task.Delay(1000).GetAwaiter().GetResult();
							Console.WriteLine($"BACKGROUND: Analytics completed for {payload.DataId}");

							return Either<DataProcessingError, DataProcessingPayload>.FromRight(payload);
						})
				)
		)
		// Finalize the processing (this runs immediately, not waiting for detached tasks)
		.Do(payload =>
		{
			Console.WriteLine($"Main workflow: Finalizing data processing for {payload.DataId}...");
			var completedAt = DateTime.UtcNow;

			return Either<DataProcessingError, DataProcessingPayload>.FromRight(
				payload with { CompletedAt = completedAt });
		})
		// Wait briefly to allow background tasks to make progress before example ends
		.Finally(payload =>
		{
			// Just a small delay so we can see some background task output
			Task.Delay(500).GetAwaiter().GetResult();
			return Either<DataProcessingError, DataProcessingPayload>.FromRight(payload);
		})
		.Build();
	}
}