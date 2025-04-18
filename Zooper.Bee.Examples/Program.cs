using System;
using System.Threading.Tasks;

namespace Zooper.Bee.Examples;

public static class Program
{
	public static async Task Main(string[] args)
	{
		Console.WriteLine("==============================================");
		Console.WriteLine("ZOOPER.BEE WORKFLOW EXAMPLE - ORDER PROCESSING");
		Console.WriteLine("==============================================");
		Console.WriteLine();

		// Choose which example to run based on args
		if (args.Length > 0 && args[0].Equals("pattern", StringComparison.OrdinalIgnoreCase))
		{
			Console.WriteLine("Running example with pattern matching...");
			Console.WriteLine();
			await OrderProcessingUsage.RunExampleWithPatternMatching();
		}
		else
		{
			Console.WriteLine("Running basic example...");
			Console.WriteLine();
			await OrderProcessingUsage.RunExample();
		}

		// Run the example with configuration
		await RunCustomExample();

		Console.WriteLine();
		Console.WriteLine("Example completed. Press any key to exit.");
		Console.ReadKey();
	}

	private static async Task RunCustomExample()
	{
		Console.WriteLine();
		Console.WriteLine("==============================================");
		Console.WriteLine("CUSTOM ORDER EXAMPLE");
		Console.WriteLine("==============================================");

		try
		{
			// Let the user decide if they want a successful or failing order
			Console.WriteLine("Do you want to create an order that will succeed or fail?");
			Console.WriteLine("1. Create a successful order");
			Console.WriteLine("2. Create a failing order (out of stock item)");
			Console.WriteLine("3. Create a failing order (payment issue)");
			Console.WriteLine("4. Create a failing order (shipping restriction)");
			Console.Write("Enter your choice (1-4): ");

			var choice = Console.ReadLine();
			var orderRequest = choice switch
			{
				"2" => CreateOutOfStockOrder(),
				"3" => CreatePaymentFailureOrder(),
				"4" => CreateShippingRestrictedOrder(),
				_ => OrderProcessingUsage.CreateSampleOrder()  // Default to successful order
			};

			Console.WriteLine();
			Console.WriteLine("Processing order...");
			Console.WriteLine();

			var result = await OrderProcessingExample.ProcessOrderAsync(orderRequest);

			// Use extension methods for cleaner code
			if (result.IsRight)
			{
				var confirmation = result.Right;
				Console.WriteLine("=== Order Processed Successfully ===");
				Console.WriteLine($"Order ID: {confirmation.OrderId}");
				Console.WriteLine($"Total Amount: ${confirmation.TotalAmount:F2}");
				Console.WriteLine($"Estimated Delivery: {confirmation.EstimatedDeliveryDate:d}");
				Console.WriteLine($"Tracking Number: {confirmation.TrackingNumber}");
			}
			else
			{
				var error = result.Left;
				Console.WriteLine("=== Order Processing Failed ===");
				Console.WriteLine($"Error Code: {error.Code}");
				Console.WriteLine($"Error Message: {error.Message}");
			}
		}
		catch (Exception ex)
		{
			Console.WriteLine($"An unexpected error occurred: {ex.Message}");
		}
	}

	// Create an order with an out-of-stock item
	private static OrderProcessingExample.OrderRequest CreateOutOfStockOrder()
	{
		var order = OrderProcessingUsage.CreateSampleOrder() with
		{
			Items = new System.Collections.Generic.List<OrderProcessingExample.OrderItem>
			{
				new("OUT-OF-STOCK", 1, 99.99m),
				new("PROD-001", 2, 29.99m)
			}
		};
		return order;
	}

	// Create an order that will fail payment processing
	private static OrderProcessingExample.OrderRequest CreatePaymentFailureOrder()
	{
		var order = OrderProcessingUsage.CreateSampleOrder() with
		{
			Items = new System.Collections.Generic.List<OrderProcessingExample.OrderItem>
			{
				new("PROD-001", 10, 100.00m), // High value to trigger payment failure
                new("PROD-002", 5, 100.00m)   // Total over $1000
            },
			PaymentMethod = OrderProcessingExample.PaymentMethod.BankTransfer
		};
		return order;
	}

	// Create an order that will fail due to shipping restrictions
	private static OrderProcessingExample.OrderRequest CreateShippingRestrictedOrder()
	{
		var order = OrderProcessingUsage.CreateSampleOrder() with
		{
			ShippingAddress = new OrderProcessingExample.ShippingAddress(
				RecipientName: "Restricted Destination",
				Street: "123 Restricted Ave",
				City: "Pyongyang",
				State: "N/A",
				ZipCode: "12345",
				Country: "North Korea" // Restricted country
			)
		};
		return order;
	}
}