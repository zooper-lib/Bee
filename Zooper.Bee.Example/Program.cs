using Zooper.Fox;

namespace Zooper.Bee.Example;

public class Program
{
	// Simple request model
	public record OrderRequest(int OrderId, string CustomerName, decimal OrderAmount);

	// Success result model
	public record OrderConfirmation(string ConfirmationNumber, DateTime ProcessedDate);

	// Error model
	public record OrderError(string ErrorCode, string Message);

	// Payload model to carry data through the workflow
	public record OrderProcessingPayload(
		int OrderId,
		string CustomerName,
		decimal OrderAmount,
		bool IsValidated = false,
		bool IsPaymentProcessed = false,
		string? ConfirmationNumber = null);

	public static async Task Main()
	{
		Console.WriteLine("=== Zooper.Bee Workflow Example ===\n");

		// Create a valid order request
		var validOrder = new OrderRequest(1001, "John Doe", 99.99m);

		// Create an invalid order request (negative amount)
		var invalidOrder = new OrderRequest(1002, "Jane Smith", -50.00m);

		// Process both orders
		await ProcessOrder(validOrder);
		Console.WriteLine();
		await ProcessOrder(invalidOrder);

		// Run the branching example
		await BranchingExample.RunExample();

		// Run the branch with local payload example
		await ContextLocalPayloadExample.RunExample();

		// Run the parallel execution example
		await ParallelExecutionExample.RunExample();

		// Run the parameterless workflow example
		await ParameterlessWorkflowExample.RunExample();
	}

	private static async Task ProcessOrder(OrderRequest request)
	{
		Console.WriteLine($"Processing order {request.OrderId} for {request.CustomerName}...");

		// Create the workflow
		var workflow = CreateOrderWorkflow();

		// Execute the workflow with the request
		var result = await workflow.Execute(request);

		// Handle the result
		if (result.IsRight)
		{
			var confirmation = result.Right;
			Console.WriteLine($"Order processed successfully!");
			Console.WriteLine($"Confirmation: {confirmation.ConfirmationNumber}");
			Console.WriteLine($"Processed on: {confirmation.ProcessedDate}");
		}
		else
		{
			var error = result.Left;
			Console.WriteLine($"Order processing failed: [{error.ErrorCode}] {error.Message}");
		}
	}

	private static Workflow<OrderRequest, OrderConfirmation, OrderError> CreateOrderWorkflow()
	{
		return new WorkflowBuilder<OrderRequest, OrderProcessingPayload, OrderConfirmation, OrderError>(
			// Context factory: Create initial payload from request
			request => new OrderProcessingPayload(
				request.OrderId,
				request.CustomerName,
				request.OrderAmount),

			// Result selector: Create success result from payload
			payload => new OrderConfirmation(
				payload.ConfirmationNumber ?? throw new InvalidOperationException("Missing confirmation number"),
				DateTime.Now)
		)
		// Validate order amount
		.Validate(request =>
		{
			if (request.OrderAmount <= 0)
			{
				return Option<OrderError>.Some(new OrderError("INVALID_AMOUNT", "Order amount must be greater than zero"));
			}

			return Option<OrderError>.None();
		})
		// Validate the order
		.Do(payload =>
		{
			Console.WriteLine($"Validating order {payload.OrderId}...");

			// Simulate validation logic
			return Either<OrderError, OrderProcessingPayload>.FromRight(
				payload with { IsValidated = true });
		})
		// Process payment
		.Do(payload =>
		{
			Console.WriteLine($"Processing payment of ${payload.OrderAmount}...");

			// Simulate payment processing
			return Either<OrderError, OrderProcessingPayload>.FromRight(
				payload with { IsPaymentProcessed = true });
		})
		// Generate confirmation number
		.Do(payload =>
		{
			Console.WriteLine("Generating confirmation number...");

			// Simulate confirmation number generation
			var confirmationNumber = $"ORD-{payload.OrderId}-{DateTime.Now:yyyyMMddHHmmss}";

			return Either<OrderError, OrderProcessingPayload>.FromRight(
				payload with { ConfirmationNumber = confirmationNumber });
		})
		// Conditionally send notification for large orders
		.DoIf(
			// Condition: Order amount is over $50
			payload => payload.OrderAmount > 50,
			// Activity: Notify about large order
			payload =>
			{
				Console.WriteLine("Large order detected! Sending priority notification...");
				return Either<OrderError, OrderProcessingPayload>.FromRight(payload);
			})
		// Finally, log the order processing result
		.Finally(payload =>
		{
			Console.WriteLine($"Logging order {payload.OrderId} processing completion...");
			return Either<OrderError, OrderProcessingPayload>.FromRight(payload);
		})
		.Build();
	}
}