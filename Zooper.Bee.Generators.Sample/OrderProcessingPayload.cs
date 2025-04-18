using System;
using System.Collections.Generic;
using Zooper.Bee.Generators;

namespace Zooper.Bee.Generators.Sample;

/// <summary>
/// Represents an order processing payload with explicit property dependencies.
/// This will have dependency tracking code automatically generated.
/// </summary>
[WorkflowPayload]
public partial record OrderProcessingPayload
{
	/// <summary>
	/// The original order request.
	/// </summary>
	public OrderRequest Request { get; init; }

	/// <summary>
	/// Unique identifier for this order.
	/// </summary>
	public Guid OrderId { get; init; } = Guid.NewGuid();

	/// <summary>
	/// When the order was created.
	/// </summary>
	public DateTime CreatedAt { get; init; } = DateTime.UtcNow;

	/// <summary>
	/// Subtotal of all items before discounts, shipping, or taxes.
	/// This is a base value with no dependencies.
	/// </summary>
	[WorkflowProperty(Description = "Subtotal of all line items")]
	public decimal Subtotal { get; init; }

	/// <summary>
	/// Whether the customer is eligible for free shipping.
	/// </summary>
	[WorkflowProperty(Description = "Whether the customer qualifies for free shipping")]
	public bool IsFreeShippingEligible { get; init; }

	/// <summary>
	/// Discount amount which depends on the subtotal.
	/// </summary>
	[WorkflowProperty(
		DependsOn = new[]
		{
			nameof(Subtotal)
		},
		Description = "Amount of discount applied"
	)]
	public decimal DiscountAmount { get; init; }

	/// <summary>
	/// Shipping cost which depends on eligibility status.
	/// </summary>
	[WorkflowProperty(
		DependsOn = new[]
		{
			nameof(IsFreeShippingEligible)
		},
		Description = "Cost of shipping"
	)]
	public decimal ShippingCost { get; init; }

	/// <summary>
	/// Tax amount which depends on the subtotal and shipping cost.
	/// </summary>
	[WorkflowProperty(
		DependsOn = new[]
		{
			nameof(Subtotal), nameof(ShippingCost)
		},
		Description = "Amount of tax"
	)]
	public decimal TaxAmount { get; init; }

	/// <summary>
	/// Final total for the order which depends on all financial components.
	/// </summary>
	[WorkflowProperty(
		DependsOn = new[]
		{
			nameof(Subtotal), nameof(DiscountAmount), nameof(ShippingCost), nameof(TaxAmount)
		},
		Description = "Final total amount"
	)]
	public decimal TotalAmount { get; init; }

	/// <summary>
	/// Whether inventory has been successfully reserved.
	/// </summary>
	[WorkflowProperty(Description = "Whether inventory is reserved")]
	public bool IsInventoryReserved { get; init; }

	/// <summary>
	/// Whether payment has been successfully processed.
	/// Depends on having a final total.
	/// </summary>
	[WorkflowProperty(
		DependsOn = new[]
		{
			nameof(TotalAmount)
		},
		Description = "Whether payment has been processed"
	)]
	public bool IsPaymentProcessed { get; init; }

	/// <summary>
	/// Whether the order is fully completed.
	/// Depends on both inventory and payment being processed.
	/// </summary>
	[WorkflowProperty(
		DependsOn = new[]
		{
			nameof(IsInventoryReserved), nameof(IsPaymentProcessed)
		},
		Description = "Whether the order is fully completed"
	)]
	public bool IsCompleted { get; init; }

	/// <summary>
	/// Creates a new order processing payload from an order request.
	/// </summary>
	/// <param name="request">The order request</param>
	public OrderProcessingPayload(OrderRequest request)
	{
		Request = request;
	}
}

/// <summary>
/// Represents an order request with customer and item information.
/// </summary>
public record OrderRequest
{
	/// <summary>
	/// Unique identifier for the customer.
	/// </summary>
	public string CustomerId { get; init; } = string.Empty;

	/// <summary>
	/// List of items in the order.
	/// </summary>
	public List<OrderItem> Items { get; init; } = new();

	/// <summary>
	/// Optional discount code to apply.
	/// </summary>
	public string? DiscountCode { get; init; }

	/// <summary>
	/// Customer's shipping address.
	/// </summary>
	public Address ShippingAddress { get; init; } = new();
}

/// <summary>
/// Represents an item in an order.
/// </summary>
public record OrderItem
{
	/// <summary>
	/// Unique identifier for the product.
	/// </summary>
	public string ProductId { get; init; } = string.Empty;

	/// <summary>
	/// Number of units ordered.
	/// </summary>
	public int Quantity { get; init; }

	/// <summary>
	/// Price per unit.
	/// </summary>
	public decimal UnitPrice { get; init; }
}

/// <summary>
/// Represents a shipping address.
/// </summary>
public record Address
{
	/// <summary>
	/// Street address.
	/// </summary>
	public string Street { get; init; } = string.Empty;

	/// <summary>
	/// City name.
	/// </summary>
	public string City { get; init; } = string.Empty;

	/// <summary>
	/// State or province.
	/// </summary>
	public string State { get; init; } = string.Empty;

	/// <summary>
	/// Postal or zip code.
	/// </summary>
	public string PostalCode { get; init; } = string.Empty;

	/// <summary>
	/// Country name.
	/// </summary>
	public string Country { get; init; } = "USA";
}