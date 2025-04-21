using Zooper.Fox;

namespace Zooper.Bee.Example;

public class ContextLocalPayloadExample
{
	// Request models
	public record OrderRequest(
		int OrderId,
		string CustomerName,
		decimal OrderAmount,
		bool NeedsShipping);

	// Success model
	public record OrderConfirmation(
		int OrderId,
		string CustomerName,
		decimal TotalAmount,
		string? ShippingTrackingNumber);

	// Error model
	public record OrderError(string Code, string Message);

	// Main workflow payload model
	public record OrderPayload(
		int OrderId,
		string CustomerName,
		decimal OrderAmount,
		bool NeedsShipping,
		decimal TotalAmount = 0,
		string? ShippingTrackingNumber = null);

	// Local payload for shipping context
	public record ShippingPayload(
		string CustomerAddress,
		decimal ShippingCost,
		decimal PackagingCost,
		decimal InsuranceCost,
		string? TrackingNumber = null);

	public static async Task RunExample()
	{
		Console.WriteLine("\n=== Workflow With Context Local Payload Example ===\n");

		// Create sample requests
		var standardOrder = new OrderRequest(2001, "Alice Johnson", 75.00m, false);
		var shippingOrder = new OrderRequest(2002, "Bob Smith", 120.00m, true);

		// Build the order processing workflow
		var workflow = CreateOrderWorkflow();

		// Process the standard order (no shipping)
		Console.WriteLine("Processing standard order (no shipping):");
		await ProcessOrder(workflow, standardOrder);

		Console.WriteLine();

		// Process the order with shipping
		Console.WriteLine("Processing order with shipping:");
		await ProcessOrder(workflow, shippingOrder);
	}

	private static async Task ProcessOrder(
		Workflow<OrderRequest, OrderConfirmation, OrderError> workflow,
		OrderRequest request)
	{
		var result = await workflow.Execute(request);

		if (result.IsRight)
		{
			var confirmation = result.Right;
			Console.WriteLine($"Order {confirmation.OrderId} processed successfully");
			Console.WriteLine($"Customer: {confirmation.CustomerName}");
			Console.WriteLine($"Total Amount: ${confirmation.TotalAmount}");

			if (confirmation.ShippingTrackingNumber != null)
			{
				Console.WriteLine($"Shipping Tracking Number: {confirmation.ShippingTrackingNumber}");
			}
			else
			{
				Console.WriteLine("No shipping required");
			}
		}
		else
		{
			var error = result.Left;
			Console.WriteLine($"Order processing failed: [{error.Code}] {error.Message}");
		}
	}

	private static Workflow<OrderRequest, OrderConfirmation, OrderError> CreateOrderWorkflow()
	{
		return new WorkflowBuilder<OrderRequest, OrderPayload, OrderConfirmation, OrderError>(
			// Create initial payload from request
			request => new OrderPayload(
				request.OrderId,
				request.CustomerName,
				request.OrderAmount,
				request.NeedsShipping),

			// Create result from final payload
			payload => new OrderConfirmation(
				payload.OrderId,
				payload.CustomerName,
				payload.TotalAmount,
				payload.ShippingTrackingNumber)
		)
		// Validate order amount
		.Validate(request =>
		{
			if (request.OrderAmount <= 0)
			{
				return Option<OrderError>.Some(
					new OrderError("INVALID_AMOUNT", "Order amount must be greater than zero"));
			}

			return Option<OrderError>.None();
		})
		// Process the basic order details
		.Do(payload =>
		{
			Console.WriteLine($"Processing order {payload.OrderId} for {payload.CustomerName}...");

			// Set the initial total amount to the order amount
			return Either<OrderError, OrderPayload>.FromRight(
				payload with { TotalAmount = payload.OrderAmount });
		})
		// Use specialized context for shipping-specific processing
		.WithContext(
			// Only enter this context if shipping is needed
			payload => payload.NeedsShipping,

			// Create the local shipping payload
			payload => new ShippingPayload(
				CustomerAddress: "123 Example St, City, Country", // In real world, this would come from a database
				ShippingCost: 12.50m,
				PackagingCost: 2.75m,
				InsuranceCost: 5.00m),

			// Configure the context with shipping-specific activities
			context => context
				// First shipping activity - calculate shipping costs
				.Do((mainPayload, shippingPayload) =>
				{
					Console.WriteLine("Calculating shipping costs...");

					// Calculate the total shipping cost
					decimal totalShippingCost =
						shippingPayload.ShippingCost +
						shippingPayload.PackagingCost +
						shippingPayload.InsuranceCost;

					// Update both payloads
					var updatedMainPayload = mainPayload with
					{
						TotalAmount = mainPayload.OrderAmount + totalShippingCost
					};

					return Either<OrderError, (OrderPayload, ShippingPayload)>.FromRight(
						(updatedMainPayload, shippingPayload));
				})
				// Second shipping activity - generate tracking number
				.Do((mainPayload, shippingPayload) =>
				{
					Console.WriteLine("Generating shipping tracking number...");

					// Generate a fake tracking number
					string trackingNumber = $"TRACK-{Guid.NewGuid().ToString()[..8]}";

					// Update both payloads with the tracking number
					var updatedShippingPayload = shippingPayload with { TrackingNumber = trackingNumber };
					var updatedMainPayload = mainPayload with { ShippingTrackingNumber = trackingNumber };

					return Either<OrderError, (OrderPayload, ShippingPayload)>.FromRight(
						(updatedMainPayload, updatedShippingPayload));
				})
		)
		// Finalize the order
		.Do(payload =>
		{
			Console.WriteLine($"Finalizing order {payload.OrderId}...");

			// In a real system, we would persist the order to a database here

			return Either<OrderError, OrderPayload>.FromRight(payload);
		})
		.Build();
	}
}