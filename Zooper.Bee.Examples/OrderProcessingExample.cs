using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Zooper.Bee;
using Zooper.Fox;

namespace Zooper.Bee.Examples;

/// <summary>
/// A comprehensive example demonstrating all features of the Zooper.Bee workflow system
/// by implementing an order processing workflow.
/// </summary>
public static class OrderProcessingExample
{
	// Request model
	public record OrderRequest(
		string CustomerId,
		List<OrderItem> Items,
		string? DiscountCode,
		PaymentMethod PaymentMethod,
		ShippingAddress ShippingAddress);

	public record OrderItem(string ProductId, int Quantity, decimal UnitPrice);

	public record ShippingAddress(
		string RecipientName,
		string Street,
		string City,
		string State,
		string ZipCode,
		string Country);

	public enum PaymentMethod { CreditCard, PayPal, BankTransfer }

	// Payload model
	public record OrderPayload(
		OrderRequest Request,
		Guid OrderId,
		decimal Subtotal,
		decimal DiscountAmount,
		decimal ShippingCost,
		decimal TaxAmount,
		decimal TotalAmount,
		bool IsExpressShipping,
		bool IsInternationalShipping,
		bool PaymentAuthorized,
		bool InventoryReserved,
		DateTime CreatedAt);

	// Success result model
	public record OrderConfirmation(
		Guid OrderId,
		string CustomerId,
		decimal TotalAmount,
		DateTime EstimatedDeliveryDate,
		string TrackingNumber);

	// Error model
	public record OrderError(string Code, string Message);

	/// <summary>
	/// Create and execute an order processing workflow
	/// </summary>
	public static async Task<Either<OrderError, OrderConfirmation>> ProcessOrderAsync(
		OrderRequest orderRequest,
		CancellationToken cancellationToken = default)
	{
		// Create the workflow
		var workflow = CreateOrderWorkflow();

		// Execute the workflow with the request
		return await workflow.Execute(orderRequest, cancellationToken);
	}

	/// <summary>
	/// Creates the order processing workflow
	/// </summary>
	private static Workflow<OrderRequest, OrderConfirmation, OrderError> CreateOrderWorkflow()
	{
		return new WorkflowBuilder<OrderRequest, OrderPayload, OrderConfirmation, OrderError>(
			// Context factory: creates initial payload from request
			CreateInitialPayload,
			// Result selector: creates success result from final payload
			CreateOrderConfirmation)

			// VALIDATIONS
			.Validate(ValidateCustomerId)
			.Validate(ValidateOrderItems)
			.Validate(ValidateShippingAddress)
			.Validate(ValidatePaymentMethod)

			// MAIN ACTIVITIES
			.Do(CalculateOrderTotals)
			.Do(ApplyDiscountCode)
			.Do(AuthorizePayment)

			// CONDITIONAL ACTIVITIES
			.DoIf(
				payload => payload.IsInternationalShipping,
				VerifyInternationalShipping)

			// BRANCHES
			.Branch(payload => payload.IsExpressShipping)
				.Do(CalculateExpressShippingFee)
				.Do(PrioritizeOrder)
				.EndBranch()
			.Branch(payload => !payload.IsExpressShipping)
				.Do(CalculateStandardShippingFee)
				.EndBranch()

			// MORE ACTIVITIES
			.Do(ReserveInventory)
			.Do(GenerateTrackingNumber)

			// FINALLY ACTIVITIES
			.Finally(LogOrderProcessingResult)

			// BUILD WORKFLOW
			.Build();
	}

	// Initial payload factory
	private static OrderPayload CreateInitialPayload(OrderRequest request)
	{
		return new OrderPayload(
			Request: request,
			OrderId: Guid.NewGuid(),
			Subtotal: 0,
			DiscountAmount: 0,
			ShippingCost: 0,
			TaxAmount: 0,
			TotalAmount: 0,
			IsExpressShipping: false,
			IsInternationalShipping: request.ShippingAddress.Country != "USA",
			PaymentAuthorized: false,
			InventoryReserved: false,
			CreatedAt: DateTime.UtcNow
		);
	}

	// Result selector
	private static OrderConfirmation CreateOrderConfirmation(OrderPayload payload)
	{
		return new OrderConfirmation(
			OrderId: payload.OrderId,
			CustomerId: payload.Request.CustomerId,
			TotalAmount: payload.TotalAmount,
			EstimatedDeliveryDate: payload.IsExpressShipping
				? DateTime.UtcNow.AddDays(2)
				: DateTime.UtcNow.AddDays(5),
			TrackingNumber: $"ZOOPER-{payload.OrderId:N}"
		);
	}

	// Validation methods
	private static Option<OrderError> ValidateCustomerId(OrderRequest request)
	{
		if (string.IsNullOrEmpty(request.CustomerId))
		{
			return Option<OrderError>.Some(new OrderError("INVALID_CUSTOMER", "Customer ID is required"));
		}

		return Option<OrderError>.None();
	}

	private static Task<Option<OrderError>> ValidateOrderItems(OrderRequest request, CancellationToken cancellationToken)
	{
		if (request.Items.Count == 0)
		{
			return Task.FromResult(Option<OrderError>.Some(
				new OrderError("EMPTY_ORDER", "Order must contain at least one item")));
		}

		foreach (var item in request.Items)
		{
			if (string.IsNullOrEmpty(item.ProductId))
			{
				return Task.FromResult(Option<OrderError>.Some(
					new OrderError("INVALID_PRODUCT", "Product ID is required")));
			}

			if (item.Quantity <= 0)
			{
				return Task.FromResult(Option<OrderError>.Some(
					new OrderError("INVALID_QUANTITY", "Quantity must be greater than zero")));
			}

			if (item.UnitPrice <= 0)
			{
				return Task.FromResult(Option<OrderError>.Some(
					new OrderError("INVALID_PRICE", "Unit price must be greater than zero")));
			}
		}

		return Task.FromResult(Option<OrderError>.None());
	}

	private static Option<OrderError> ValidateShippingAddress(OrderRequest request)
	{
		var address = request.ShippingAddress;

		if (string.IsNullOrEmpty(address.RecipientName))
		{
			return Option<OrderError>.Some(
				new OrderError("INVALID_ADDRESS", "Recipient name is required"));
		}

		if (string.IsNullOrEmpty(address.Street))
		{
			return Option<OrderError>.Some(
				new OrderError("INVALID_ADDRESS", "Street address is required"));
		}

		if (string.IsNullOrEmpty(address.City))
		{
			return Option<OrderError>.Some(
				new OrderError("INVALID_ADDRESS", "City is required"));
		}

		if (string.IsNullOrEmpty(address.State))
		{
			return Option<OrderError>.Some(
				new OrderError("INVALID_ADDRESS", "State is required"));
		}

		if (string.IsNullOrEmpty(address.ZipCode))
		{
			return Option<OrderError>.Some(
				new OrderError("INVALID_ADDRESS", "Zip code is required"));
		}

		if (string.IsNullOrEmpty(address.Country))
		{
			return Option<OrderError>.Some(
				new OrderError("INVALID_ADDRESS", "Country is required"));
		}

		return Option<OrderError>.None();
	}

	private static Task<Option<OrderError>> ValidatePaymentMethod(OrderRequest request, CancellationToken cancellationToken)
	{
		// In a real application, we might do more complex validation based on payment method
		return Task.FromResult(Option<OrderError>.None());
	}

	// Activity methods
	private static Either<OrderError, OrderPayload> CalculateOrderTotals(OrderPayload payload)
	{
		decimal subtotal = 0;

		foreach (var item in payload.Request.Items)
		{
			subtotal += item.Quantity * item.UnitPrice;
		}

		// Simple tax calculation (5%)
		decimal taxAmount = subtotal * 0.05m;

		// Updated payload with calculated values
		var updatedPayload = payload with
		{
			Subtotal = subtotal,
			TaxAmount = taxAmount,
			TotalAmount = subtotal + taxAmount // Shipping will be added later
		};

		return Either<OrderError, OrderPayload>.FromRight(updatedPayload);
	}

	private static async Task<Either<OrderError, OrderPayload>> ApplyDiscountCode(
		OrderPayload payload, CancellationToken cancellationToken)
	{
		// Check if discount code was provided
		if (string.IsNullOrEmpty(payload.Request.DiscountCode))
		{
			return Either<OrderError, OrderPayload>.FromRight(payload);
		}

		// Simulate an async API call to validate discount code
		await Task.Delay(100, cancellationToken);

		// Simple discount logic - in real app would check against database
		decimal discountAmount = 0;
		string code = payload.Request.DiscountCode.ToUpperInvariant();

		if (code == "SAVE10")
		{
			discountAmount = payload.Subtotal * 0.10m; // 10% discount
		}
		else if (code == "SAVE20")
		{
			discountAmount = payload.Subtotal * 0.20m; // 20% discount
		}
		else if (code == "FREE")
		{
			// Invalid code
			return Either<OrderError, OrderPayload>.FromLeft(
				new OrderError("INVALID_DISCOUNT", "The discount code is invalid or expired"));
		}

		// Update payload with discount
		var updatedPayload = payload with
		{
			DiscountAmount = discountAmount,
			TotalAmount = payload.Subtotal + payload.TaxAmount - discountAmount
		};

		return Either<OrderError, OrderPayload>.FromRight(updatedPayload);
	}

	private static async Task<Either<OrderError, OrderPayload>> AuthorizePayment(
		OrderPayload payload, CancellationToken cancellationToken)
	{
		// Simulate payment processing with external service
		await Task.Delay(200, cancellationToken);

		// In a real app, we would call a payment provider here

		// For demo purposes, we'll fail bank transfers with large amounts
		if (payload.Request.PaymentMethod == PaymentMethod.BankTransfer && payload.TotalAmount > 1000)
		{
			return Either<OrderError, OrderPayload>.FromLeft(
				new OrderError("PAYMENT_FAILED", "Bank transfer failed for large amount"));
		}

		// Mark payment as authorized
		var updatedPayload = payload with { PaymentAuthorized = true };

		return Either<OrderError, OrderPayload>.FromRight(updatedPayload);
	}

	private static async Task<Either<OrderError, OrderPayload>> VerifyInternationalShipping(
		OrderPayload payload, CancellationToken cancellationToken)
	{
		// Simulate verification with shipping provider API
		await Task.Delay(150, cancellationToken);

		string country = payload.Request.ShippingAddress.Country;

		// Example of countries we don't ship to
		var restrictedCountries = new[] { "North Korea", "Iran" };

		if (Array.IndexOf(restrictedCountries, country) >= 0)
		{
			return Either<OrderError, OrderPayload>.FromLeft(
				new OrderError("SHIPPING_RESTRICTED", $"We cannot ship to {country}"));
		}

		return Either<OrderError, OrderPayload>.FromRight(payload);
	}

	private static Either<OrderError, OrderPayload> CalculateExpressShippingFee(OrderPayload payload)
	{
		// Express shipping costs more
		decimal shippingCost = payload.IsInternationalShipping ? 39.99m : 19.99m;

		var updatedPayload = payload with
		{
			ShippingCost = shippingCost,
			TotalAmount = payload.Subtotal + payload.TaxAmount - payload.DiscountAmount + shippingCost
		};

		return Either<OrderError, OrderPayload>.FromRight(updatedPayload);
	}

	private static Either<OrderError, OrderPayload> PrioritizeOrder(OrderPayload payload)
	{
		// In a real application, we might call a service to prioritize this order
		// or add it to a different queue

		Console.WriteLine($"Order {payload.OrderId} marked as priority express shipping");

		return Either<OrderError, OrderPayload>.FromRight(payload);
	}

	private static Either<OrderError, OrderPayload> CalculateStandardShippingFee(OrderPayload payload)
	{
		// Standard shipping
		decimal shippingCost = payload.IsInternationalShipping ? 19.99m : 5.99m;

		var updatedPayload = payload with
		{
			ShippingCost = shippingCost,
			TotalAmount = payload.Subtotal + payload.TaxAmount - payload.DiscountAmount + shippingCost
		};

		return Either<OrderError, OrderPayload>.FromRight(updatedPayload);
	}

	private static async Task<Either<OrderError, OrderPayload>> ReserveInventory(
		OrderPayload payload, CancellationToken cancellationToken)
	{
		// Simulate inventory system call
		await Task.Delay(300, cancellationToken);

		// In a real app, we would check inventory availability for each item

		// For demo purposes, let's assume a product with ID "OUT-OF-STOCK" is unavailable
		foreach (var item in payload.Request.Items)
		{
			if (item.ProductId == "OUT-OF-STOCK")
			{
				return Either<OrderError, OrderPayload>.FromLeft(
					new OrderError("INVENTORY_UNAVAILABLE", $"Product {item.ProductId} is out of stock"));
			}
		}

		// Mark inventory as reserved
		var updatedPayload = payload with { InventoryReserved = true };

		return Either<OrderError, OrderPayload>.FromRight(updatedPayload);
	}

	private static Either<OrderError, OrderPayload> GenerateTrackingNumber(OrderPayload payload)
	{
		// In a real app, we might get a tracking number from the shipping provider
		// Here we just use the order ID

		return Either<OrderError, OrderPayload>.FromRight(payload);
	}

	private static async Task<Either<OrderError, OrderPayload>> LogOrderProcessingResult(
		OrderPayload payload, CancellationToken cancellationToken)
	{
		// This is a finally activity, so it always runs
		// even if the workflow fails earlier

		// Simulate logging to an external system
		await Task.Delay(50, cancellationToken);

		Console.WriteLine($"Order {payload.OrderId} processing completed at {DateTime.UtcNow}");
		Console.WriteLine($"Total amount: ${payload.TotalAmount}");

		return Either<OrderError, OrderPayload>.FromRight(payload);
	}
}