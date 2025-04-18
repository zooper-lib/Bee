using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Zooper.Fox;

namespace Zooper.Bee.Examples;

/// <summary>
/// Shows how to use the order processing workflow in actual code
/// </summary>
public static class OrderProcessingUsage
{
	/// <summary>
	/// Example method demonstrating how to use the order processing workflow
	/// </summary>
	public static async Task RunExample()
	{
		// Create a sample order
		var order = CreateSampleOrder();

		// Process the order through the workflow
		var result = await OrderProcessingExample.ProcessOrderAsync(order);

		// Handle the result based on success or failure
		if (result.IsRight)
		{
			// Get the success result
			var confirmation = result.Right;

			Console.WriteLine("=== Order Processed Successfully ===");
			Console.WriteLine($"Order ID: {confirmation.OrderId}");
			Console.WriteLine($"Customer ID: {confirmation.CustomerId}");
			Console.WriteLine($"Total Amount: ${confirmation.TotalAmount}");
			Console.WriteLine($"Estimated Delivery: {confirmation.EstimatedDeliveryDate:d}");
			Console.WriteLine($"Tracking Number: {confirmation.TrackingNumber}");
		}
		else
		{
			// Get the error result
			var error = result.Left;

			Console.WriteLine("=== Order Processing Failed ===");
			Console.WriteLine($"Error Code: {error.Code}");
			Console.WriteLine($"Error Message: {error.Message}");
		}
	}

	/// <summary>
	/// Example showing how to handle failures with pattern matching
	/// </summary>
	public static async Task RunExampleWithPatternMatching()
	{
		// Create orders - one will succeed, one will fail
		var successOrder = CreateSampleOrder();
		var failingOrder = CreateFailingOrder();

		// Process both orders
		var result1 = await OrderProcessingExample.ProcessOrderAsync(successOrder);
		var result2 = await OrderProcessingExample.ProcessOrderAsync(failingOrder);

		// Handle results with pattern matching
		HandleResult(result1);
		HandleResult(result2);
	}

	// Handle the result with pattern matching
	private static void HandleResult(Either<OrderProcessingExample.OrderError, OrderProcessingExample.OrderConfirmation> result)
	{
		// Pattern match on the result
		switch (result)
		{
			case var r when r.IsRight:
				var confirmation = r.Right;
				Console.WriteLine($"Order {confirmation.OrderId} processed successfully");
				Console.WriteLine($"Total: ${confirmation.TotalAmount}, Delivery: {confirmation.EstimatedDeliveryDate:d}");
				break;

			case var r when r.IsLeft:
				var error = r.Left;
				Console.WriteLine($"Order processing failed: [{error.Code}] {error.Message}");
				// Additional error handling based on error code
				switch (error.Code)
				{
					case "PAYMENT_FAILED":
						Console.WriteLine("Please try another payment method");
						break;
					case "INVENTORY_UNAVAILABLE":
						Console.WriteLine("Please remove unavailable items and try again");
						break;
					case "SHIPPING_RESTRICTED":
						Console.WriteLine("We are unable to ship to your country");
						break;
					default:
						Console.WriteLine("Please check your order details and try again");
						break;
				}
				break;
		}
	}

	/// <summary>
	/// Creates a sample order that should process successfully
	/// </summary>
	public static OrderProcessingExample.OrderRequest CreateSampleOrder()
	{
		return new OrderProcessingExample.OrderRequest(
			CustomerId: "CUST12345",
			Items: new List<OrderProcessingExample.OrderItem>
			{
				new("PROD-001", 2, 29.99m),
				new("PROD-002", 1, 49.99m)
			},
			DiscountCode: "SAVE10",
			PaymentMethod: OrderProcessingExample.PaymentMethod.CreditCard,
			ShippingAddress: new OrderProcessingExample.ShippingAddress(
				RecipientName: "John Doe",
				Street: "123 Main St",
				City: "Anytown",
				State: "CA",
				ZipCode: "90210",
				Country: "USA"
			)
		);
	}

	/// <summary>
	/// Creates a sample order that will fail during processing
	/// </summary>
	public static OrderProcessingExample.OrderRequest CreateFailingOrder()
	{
		return new OrderProcessingExample.OrderRequest(
			CustomerId: "CUST67890",
			Items: new List<OrderProcessingExample.OrderItem>
			{
				new("PROD-003", 5, 199.99m),
				new("OUT-OF-STOCK", 1, 599.99m) // This product is out of stock
            },
			DiscountCode: "SAVE20",
			PaymentMethod: OrderProcessingExample.PaymentMethod.BankTransfer, // Will fail for large amounts
			ShippingAddress: new OrderProcessingExample.ShippingAddress(
				RecipientName: "Jane Smith",
				Street: "456 Elm St",
				City: "Othertown",
				State: "NY",
				ZipCode: "10001",
				Country: "USA"
			)
		);
	}
}