using System;
using Zooper.Bee.Generators;

namespace Zooper.Bee.Examples;

/// <summary>
/// A sample payload class that demonstrates how to use the workflow payload attributes.
/// This class will have dependency tracking code generated for it.
/// </summary>
[WorkflowPayload]
public partial record SamplePayload
{
	/// <summary>
	/// The request that initiated this workflow.
	/// </summary>
	public SampleRequest Request { get; init; } = new();

	/// <summary>
	/// Unique identifier for the transaction.
	/// </summary>
	public Guid TransactionId { get; init; } = Guid.NewGuid();

	/// <summary>
	/// Timestamp of creation.
	/// </summary>
	public DateTime CreatedAt { get; init; } = DateTime.UtcNow();

	/// <summary>
	/// The subtotal before taxes and discounts.
	/// This is a base property with no dependencies.
	/// </summary>
	[WorkflowProperty(Description = "Calculated subtotal of all items")]
	public decimal Subtotal { get; init; }

	/// <summary>
	/// Tax amount that depends on the subtotal.
	/// </summary>
	[WorkflowProperty(DependsOn = new[] { nameof(Subtotal) },
		Description = "Tax calculated based on subtotal")]
	public decimal TaxAmount { get; init; }

	/// <summary>
	/// Discount amount that depends on the subtotal.
	/// </summary>
	[WorkflowProperty(DependsOn = new[] { nameof(Subtotal) },
		Description = "Applied discount amount")]
	public decimal DiscountAmount { get; init; }

	/// <summary>
	/// Shipping cost that has no dependencies.
	/// </summary>
	[WorkflowProperty(Description = "Cost of shipping")]
	public decimal ShippingCost { get; init; }

	/// <summary>
	/// Total amount that depends on all other financial components.
	/// </summary>
	[WorkflowProperty(DependsOn = new[] { nameof(Subtotal), nameof(TaxAmount),
		nameof(DiscountAmount), nameof(ShippingCost) },
		Description = "Final total amount")]
	public decimal TotalAmount { get; init; }

	/// <summary>
	/// Flag indicating whether payment has been processed.
	/// Can only be set when there's a total amount.
	/// </summary>
	[WorkflowProperty(DependsOn = new[] { nameof(TotalAmount) },
		Description = "Whether payment has been processed")]
	public bool IsPaymentProcessed { get; init; }

	/// <summary>
	/// Constructor that takes a request object.
	/// </summary>
	/// <param name="request">The request object</param>
	public SamplePayload(SampleRequest request)
	{
		Request = request;
	}
}

/// <summary>
/// Sample request class to demonstrate workflow inputs.
/// </summary>
public record SampleRequest
{
	/// <summary>
	/// Customer identifier.
	/// </summary>
	public string CustomerId { get; init; } = string.Empty;

	/// <summary>
	/// Optional discount code to apply.
	/// </summary>
	public string? DiscountCode { get; init; }
}

/// <summary>
/// Sample error type for the workflow.
/// </summary>
public record SampleError(string Code, string Message);