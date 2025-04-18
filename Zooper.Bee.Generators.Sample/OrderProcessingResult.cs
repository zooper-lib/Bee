using System;

namespace Zooper.Bee.Generators.Sample;

/// <summary>
/// Represents the successful result of an order processing workflow.
/// </summary>
public record OrderProcessingResult
{
	/// <summary>
	/// Unique identifier for the order.
	/// </summary>
	public Guid OrderId { get; init; }

	/// <summary>
	/// Identifier of the customer who placed the order.
	/// </summary>
	public string CustomerId { get; init; } = string.Empty;

	/// <summary>
	/// Total amount charged for the order.
	/// </summary>
	public decimal TotalAmount { get; init; }

	/// <summary>
	/// When the order was submitted.
	/// </summary>
	public DateTime OrderDate { get; init; }

	/// <summary>
	/// Expected date of delivery.
	/// </summary>
	public DateTime EstimatedDeliveryDate { get; init; }

	/// <summary>
	/// Tracking number for the shipment.
	/// </summary>
	public string TrackingNumber { get; init; } = string.Empty;

	/// <summary>
	/// Whether the order is fully completed.
	/// </summary>
	public bool IsCompleted { get; init; }
}

/// <summary>
/// Represents an error that occurred during order processing.
/// </summary>
public record OrderProcessingError
{
	/// <summary>
	/// Error code for categorization.
	/// </summary>
	public string Code { get; init; } = string.Empty;

	/// <summary>
	/// Human-readable error message.
	/// </summary>
	public string Message { get; init; } = string.Empty;

	/// <summary>
	/// Creates a new order processing error.
	/// </summary>
	/// <param name="code">Error code</param>
	/// <param name="message">Error message</param>
	public OrderProcessingError(string code, string message)
	{
		Code = code;
		Message = message;
	}
}