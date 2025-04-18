using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Zooper.Bee.Generators.Sample;

namespace Zooper.Bee.Generators.Sample;

/// <summary>
/// Demonstrates using the OrderProcessingWorkflow with the generated dependency tracking.
/// </summary>
public static class Program
{
	public static async Task Main(string[] args)
	{
		Console.WriteLine("Order Processing Workflow Demo");
		Console.WriteLine("=============================");
		Console.WriteLine();

		// Create a sample order
		var order = CreateSampleOrder();

		// Process the order
		var result = await OrderProcessingWorkflow.ProcessAsync(order);

		// Display the result
		Console.WriteLine();
		if (result.IsRight)
		{
			var success = result.Right;
			Console.WriteLine($"Order processed successfully!");
			Console.WriteLine($"Order ID: {success.OrderId}");
			Console.WriteLine($"Customer: {success.CustomerId}");
			Console.WriteLine($"Total: ${success.TotalAmount:F2}");
			Console.WriteLine($"Order Date: {success.OrderDate}");
			Console.WriteLine($"Estimated Delivery: {success.EstimatedDeliveryDate}");
			Console.WriteLine($"Tracking Number: {success.TrackingNumber}");
			Console.WriteLine($"Completed: {success.IsCompleted}");
		}
		else
		{
			var error = result.Left;
			Console.WriteLine($"Order processing failed!");
			Console.WriteLine($"Error Code: {error.Code}");
			Console.WriteLine($"Error Message: {error.Message}");
		}

		Console.WriteLine();
		Console.WriteLine("Press any key to exit...");
		Console.ReadKey();
	}

	/// <summary>
	/// Creates a sample order for demonstration.
	/// </summary>
	private static OrderRequest CreateSampleOrder()
	{
		Console.WriteLine("Creating sample order...");

		return new OrderRequest
		{
			CustomerId = "CUST12345",
			DiscountCode = "SAVE10",
			Items = new List<OrderItem>
			{
				new OrderItem
				{
					ProductId = "PROD001",
					Quantity = 2,
					UnitPrice = 29.99m
				},
				new OrderItem
				{
					ProductId = "PROD002",
					Quantity = 1,
					UnitPrice = 49.99m
				}
			},
			ShippingAddress = new Address
			{
				Street = "123 Main St",
				City = "Anytown",
				State = "CA",
				PostalCode = "12345",
				Country = "USA"
			}
		};
	}
}