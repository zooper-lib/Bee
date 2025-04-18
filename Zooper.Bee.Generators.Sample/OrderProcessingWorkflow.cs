using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Zooper.Fox;

namespace Zooper.Bee.Generators.Sample;

/// <summary>
/// Demonstrates using the workflow with automatically generated property dependency tracking.
/// </summary>
public static class OrderProcessingWorkflow
{
	/// <summary>
	/// Processes an order request and returns either a result or an error.
	/// </summary>
	/// <param name="request">The order request to process</param>
	/// <param name="cancellationToken">Cancellation token</param>
	/// <returns>Either a success result or an error</returns>
	public static async Task<Either<OrderProcessingError, OrderProcessingResult>> ProcessAsync(
		OrderRequest request,
		CancellationToken cancellationToken = default)
	{
		// Create and configure the workflow
		var workflow = CreateOrderWorkflow();

		// Execute the workflow with the request
		return await workflow.Execute(request, cancellationToken);
	}

	/// <summary>
	/// Creates the order processing workflow with dependency-aware property handling.
	/// </summary>
	private static Workflow<OrderRequest, OrderProcessingResult, OrderProcessingError> CreateOrderWorkflow()
	{
		return new WorkflowBuilder<OrderRequest, OrderProcessingPayload, OrderProcessingResult, OrderProcessingError>(
			// Create initial payload from request
			request => new OrderProcessingPayload(request),
			// Create success result from final payload
			CreateOrderResult)

			// Validate the request
			.Validate(ValidateOrderRequest)

			// Calculate subtotal (no dependencies)
			.Do(p => p.Subtotal, CalculateSubtotal)

			// Determine shipping eligibility (no dependencies)
			.Do(p => p.IsFreeShippingEligible, DetermineShippingEligibility)

			// Apply discount (depends on subtotal)
			.DoIf(
				p => !string.IsNullOrEmpty(p.Request.DiscountCode),
				p => p.DiscountAmount,
				ApplyDiscountCode)

			// Calculate shipping cost (depends on eligibility)
			.Do(p => p.ShippingCost, CalculateShippingCost)

			// Calculate tax (depends on subtotal and shipping)
			.Do(p => p.TaxAmount, CalculateTaxAmount)

			// Calculate total (depends on all financial components)
			.Do(p => p.TotalAmount, CalculateTotalAmount)

			// Reserve inventory (no dependencies)
			.Do(p => p.IsInventoryReserved, ReserveInventory)

			// Process payment (depends on total)
			.Do(p => p.IsPaymentProcessed, ProcessPayment)

			// Mark as completed (depends on inventory and payment)
			.Do(p => p.IsCompleted, CompleteOrder)

			// Build the workflow
			.Build();
	}

	#region Result Creation

	private static OrderProcessingResult CreateOrderResult(OrderProcessingPayload payload)
	{
		return new OrderProcessingResult
		{
			OrderId = payload.OrderId,
			CustomerId = payload.Request.CustomerId,
			TotalAmount = payload.TotalAmount,
			OrderDate = payload.CreatedAt,
			EstimatedDeliveryDate = DateTime.UtcNow.AddDays(5),
			TrackingNumber = $"TRK-{payload.OrderId:N}".Substring(0, 15),
			IsCompleted = payload.IsCompleted
		};
	}

	#endregion

	#region Validation Methods

	private static Option<OrderProcessingError> ValidateOrderRequest(OrderRequest request)
	{
		// Validate customer ID
		if (string.IsNullOrEmpty(request.CustomerId))
		{
			return Option<OrderProcessingError>.Some(
				new OrderProcessingError("INVALID_CUSTOMER", "Customer ID is required"));
		}

		// Validate items
		if (request.Items.Count == 0)
		{
			return Option<OrderProcessingError>.Some(
				new OrderProcessingError("EMPTY_ORDER", "Order must contain at least one item"));
		}

		foreach (var item in request.Items)
		{
			if (string.IsNullOrEmpty(item.ProductId))
			{
				return Option<OrderProcessingError>.Some(
					new OrderProcessingError("INVALID_PRODUCT", "Product ID is required"));
			}

			if (item.Quantity <= 0)
			{
				return Option<OrderProcessingError>.Some(
					new OrderProcessingError("INVALID_QUANTITY", "Quantity must be greater than zero"));
			}

			if (item.UnitPrice < 0)
			{
				return Option<OrderProcessingError>.Some(
					new OrderProcessingError("INVALID_PRICE", "Price cannot be negative"));
			}
		}

		// Validate shipping address
		var address = request.ShippingAddress;
		if (string.IsNullOrEmpty(address.Street) ||
			string.IsNullOrEmpty(address.City) ||
			string.IsNullOrEmpty(address.State) ||
			string.IsNullOrEmpty(address.PostalCode) ||
			string.IsNullOrEmpty(address.Country))
		{
			return Option<OrderProcessingError>.Some(
				new OrderProcessingError("INVALID_ADDRESS", "Complete shipping address is required"));
		}

		return Option<OrderProcessingError>.None();
	}

	#endregion

	#region Activity Methods

	private static Either<OrderProcessingError, OrderProcessingPayload> CalculateSubtotal(OrderProcessingPayload payload)
	{
		decimal subtotal = payload.Request.Items.Sum(item => item.Quantity * item.UnitPrice);

		return Either<OrderProcessingError, OrderProcessingPayload>.FromRight(
			payload with { Subtotal = subtotal });
	}

	private static Either<OrderProcessingError, OrderProcessingPayload> DetermineShippingEligibility(
		OrderProcessingPayload payload)
	{
		// Customers are eligible for free shipping if order is over $100
		bool isEligible = payload.Request.Items.Sum(item => item.Quantity * item.UnitPrice) >= 100;

		// VIP customers always get free shipping
		if (payload.Request.CustomerId.StartsWith("VIP"))
		{
			isEligible = true;
		}

		return Either<OrderProcessingError, OrderProcessingPayload>.FromRight(
			payload with { IsFreeShippingEligible = isEligible });
	}

	private static Either<OrderProcessingError, OrderProcessingPayload> ApplyDiscountCode(
		OrderProcessingPayload payload)
	{
		// Extract and normalize discount code
		string code = payload.Request.DiscountCode?.ToUpperInvariant() ?? string.Empty;
		decimal discountAmount = 0;

		// Apply discount based on code
		switch (code)
		{
			case "SAVE10":
				discountAmount = payload.Subtotal * 0.1m;
				break;

			case "SAVE20":
				discountAmount = payload.Subtotal * 0.2m;
				break;

			case "WELCOME":
				discountAmount = 15; // Flat $15 off
				break;

			default:
				return Either<OrderProcessingError, OrderProcessingPayload>.FromLeft(
					new OrderProcessingError("INVALID_DISCOUNT", "Discount code is invalid or expired"));
		}

		return Either<OrderProcessingError, OrderProcessingPayload>.FromRight(
			payload with { DiscountAmount = discountAmount });
	}

	private static Either<OrderProcessingError, OrderProcessingPayload> CalculateShippingCost(
		OrderProcessingPayload payload)
	{
		decimal shippingCost = 0;

		// If eligible for free shipping, cost is $0
		if (payload.IsFreeShippingEligible)
		{
			shippingCost = 0;
		}
		// Otherwise, calculate based on country
		else
		{
			shippingCost = payload.Request.ShippingAddress.Country.ToUpperInvariant() == "USA"
				? 7.99m // Domestic shipping
				: 24.99m; // International shipping
		}

		return Either<OrderProcessingError, OrderProcessingPayload>.FromRight(
			payload with { ShippingCost = shippingCost });
	}

	private static Either<OrderProcessingError, OrderProcessingPayload> CalculateTaxAmount(
		OrderProcessingPayload payload)
	{
		// Calculate tax on subtotal and shipping
		decimal taxableAmount = payload.Subtotal + payload.ShippingCost;

		// Apply appropriate tax rate
		decimal taxRate = payload.Request.ShippingAddress.State.ToUpperInvariant() switch
		{
			"CA" => 0.0725m, // California
			"NY" => 0.0845m, // New York
			"TX" => 0.0625m, // Texas
			_ => 0.05m // Default rate
		};

		decimal taxAmount = taxableAmount * taxRate;

		return Either<OrderProcessingError, OrderProcessingPayload>.FromRight(
			payload with { TaxAmount = taxAmount });
	}

	private static Either<OrderProcessingError, OrderProcessingPayload> CalculateTotalAmount(
		OrderProcessingPayload payload)
	{
		// Calculate the final total
		decimal totalAmount =
			payload.Subtotal +
			payload.ShippingCost +
			payload.TaxAmount -
			payload.DiscountAmount;

		// Ensure total isn't negative
		totalAmount = Math.Max(0, totalAmount);

		return Either<OrderProcessingError, OrderProcessingPayload>.FromRight(
			payload with { TotalAmount = totalAmount });
	}

	private static async Task<Either<OrderProcessingError, OrderProcessingPayload>> ReserveInventory(
		OrderProcessingPayload payload, CancellationToken cancellationToken)
	{
		// Simulate async inventory check
		await Task.Delay(100, cancellationToken);

		// Check for out-of-stock items (for demo, assume product with ID "OUT-OF-STOCK" is unavailable)
		foreach (var item in payload.Request.Items)
		{
			if (item.ProductId == "OUT-OF-STOCK")
			{
				return Either<OrderProcessingError, OrderProcessingPayload>.FromLeft(
					new OrderProcessingError("INVENTORY_UNAVAILABLE",
						$"The product '{item.ProductId}' is out of stock"));
			}
		}

		return Either<OrderProcessingError, OrderProcessingPayload>.FromRight(
			payload with { IsInventoryReserved = true });
	}

	private static async Task<Either<OrderProcessingError, OrderProcessingPayload>> ProcessPayment(
		OrderProcessingPayload payload, CancellationToken cancellationToken)
	{
		// Simulate payment processing
		await Task.Delay(200, cancellationToken);

		// For demo purposes, assume payments over $1000 fail
		if (payload.TotalAmount > 1000)
		{
			return Either<OrderProcessingError, OrderProcessingPayload>.FromLeft(
				new OrderProcessingError("PAYMENT_DECLINED",
					"Payment was declined for amounts over $1000"));
		}

		return Either<OrderProcessingError, OrderProcessingPayload>.FromRight(
			payload with { IsPaymentProcessed = true });
	}

	private static Either<OrderProcessingError, OrderProcessingPayload> CompleteOrder(
		OrderProcessingPayload payload)
	{
		// This method will only execute if IsInventoryReserved and IsPaymentProcessed are both true,
		// thanks to the automatic dependency validation

		// Simply mark the order as completed
		return Either<OrderProcessingError, OrderProcessingPayload>.FromRight(
			payload with { IsCompleted = true });
	}

	#endregion
}