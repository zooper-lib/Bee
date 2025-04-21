using Zooper.Fox;

namespace Zooper.Bee.Example;

public class ParameterlessWorkflowExample
{
	// Success model
	public record ProcessingResult(DateTime ProcessedAt, string Status);

	// Error model
	public record ProcessingError(string Code, string Message);

	// Payload model
	public record ProcessingPayload(
		DateTime StartedAt,
		bool IsCompleted = false,
		string Status = "Pending");

	public static async Task RunExample()
	{
		Console.WriteLine("\n=== Parameterless Workflow Example ===\n");

		Console.WriteLine("Example 1: Using WorkflowBuilderFactory.Create");
		await RunExampleWithFactory();

		Console.WriteLine("\nExample 2: Using Unit type directly");
		await RunExampleWithUnit();

		Console.WriteLine("\nExample 3: Using extension method for execution");
		await RunExampleWithExtension();
	}

	private static async Task RunExampleWithFactory()
	{
		// Create a workflow that doesn't need input parameters
		var workflow = WorkflowBuilderFactory.CreateWorkflow<ProcessingPayload, ProcessingResult, ProcessingError>(
			// Initial payload factory - no parameters needed
			() => new ProcessingPayload(StartedAt: DateTime.UtcNow),

			// Result selector - convert final payload to success result
			payload => new ProcessingResult(DateTime.UtcNow, payload.Status),

			// Configure the workflow
			builder => builder
				.Do(payload =>
				{
					Console.WriteLine("Processing step 1...");
					return Either<ProcessingError, ProcessingPayload>.FromRight(
						payload with { Status = "Step 1 completed" });
				})
				.Do(payload =>
				{
					Console.WriteLine("Processing step 2...");
					return Either<ProcessingError, ProcessingPayload>.FromRight(
						payload with { Status = "Step 2 completed", IsCompleted = true });
				})
		);

		// Execute without parameters
		var result = await workflow.Execute();

		if (result.IsRight)
		{
			Console.WriteLine($"Workflow completed successfully: {result.Right.Status}");
			Console.WriteLine($"Processed at: {result.Right.ProcessedAt}");
		}
		else
		{
			Console.WriteLine($"Workflow failed: [{result.Left.Code}] {result.Left.Message}");
		}
	}

	private static async Task RunExampleWithUnit()
	{
		// Create a workflow with Unit type as request
		var workflow = new WorkflowBuilder<Unit, ProcessingPayload, ProcessingResult, ProcessingError>(
			// Use Unit parameter (ignored)
			_ => new ProcessingPayload(StartedAt: DateTime.UtcNow),

			// Result selector
			payload => new ProcessingResult(DateTime.UtcNow, payload.Status)
		)
		.Do(payload =>
		{
			Console.WriteLine("Executing task A...");
			return Either<ProcessingError, ProcessingPayload>.FromRight(
				payload with { Status = "Task A completed" });
		})
		.Do(payload =>
		{
			Console.WriteLine("Executing task B...");
			return Either<ProcessingError, ProcessingPayload>.FromRight(
				payload with { Status = "Task B completed", IsCompleted = true });
		})
		.Build();

		// Execute with Unit.Value
		var result = await workflow.Execute(Unit.Value);

		if (result.IsRight)
		{
			Console.WriteLine($"Workflow completed successfully: {result.Right.Status}");
			Console.WriteLine($"Processed at: {result.Right.ProcessedAt}");
		}
		else
		{
			Console.WriteLine($"Workflow failed: [{result.Left.Code}] {result.Left.Message}");
		}
	}

	private static async Task RunExampleWithExtension()
	{
		// Create a workflow with Unit type as request
		var workflow = new WorkflowBuilder<Unit, ProcessingPayload, ProcessingResult, ProcessingError>(
			// Use Unit parameter (ignored)
			_ => new ProcessingPayload(StartedAt: DateTime.UtcNow),

			// Result selector
			payload => new ProcessingResult(DateTime.UtcNow, payload.Status)
		)
		.Do(payload =>
		{
			Console.WriteLine("Running process X...");
			return Either<ProcessingError, ProcessingPayload>.FromRight(
				payload with { Status = "Process X completed" });
		})
		.Do(payload =>
		{
			Console.WriteLine("Running process Y...");
			return Either<ProcessingError, ProcessingPayload>.FromRight(
				payload with { Status = "Process Y completed", IsCompleted = true });
		})
		.Build();

		// Execute using the extension method (no parameters)
		var result = await workflow.Execute();

		if (result.IsRight)
		{
			Console.WriteLine($"Workflow completed successfully: {result.Right.Status}");
			Console.WriteLine($"Processed at: {result.Right.ProcessedAt}");
		}
		else
		{
			Console.WriteLine($"Workflow failed: [{result.Left.Code}] {result.Left.Message}");
		}
	}
}