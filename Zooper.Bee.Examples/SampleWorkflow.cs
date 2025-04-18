using System;
using System.Threading;
using System.Threading.Tasks;
using Zooper.Fox;

namespace Zooper.Bee.Examples;

/// <summary>
/// Demonstrates how to use the workflow builder with the generated property extensions.
/// </summary>
public static class SampleWorkflow
{
	/// <summary>
	/// Creates and executes a sample workflow that demonstrates property dependency tracking.
	/// </summary>
	/// <param name="request">The request to process</param>
	/// <param name="cancellationToken">Cancellation token</param>
	/// <returns>Either a success result or an error</returns>
	public static async Task<Either<SampleError, SampleResult>> ProcessAsync(
		SampleRequest request,
		CancellationToken cancellationToken = default)
	{
		// Create the workflow
		var workflow = CreateSampleWorkflow();

		// Execute the workflow
		return await workflow.Execute(request, cancellationToken);
	}

	/// <summary>
	/// Creates a sample workflow with property dependency validation.
	/// </summary>
	/// <returns>A configured workflow</returns>
	private static Workflow<SampleRequest, SampleResult, SampleError> CreateSampleWorkflow()
	{
		return new WorkflowBuilder<SampleRequest, SamplePayload, SampleResult, SampleError>(
			// Create the initial payload from the request
			request => new SamplePayload(request),
			// Create the success result from the final payload
			payload => new SampleResult(
				payload.TransactionId,
				payload.TotalAmount,
				payload.IsPaymentProcessed))

			// Validate the request
			.Validate(ValidateRequest)

			// Calculate subtotal - no dependencies
			// Notice we're using the generated extension method with property expressions
			.Do(p => p.Subtotal, CalculateSubtotal)

			// Calculate tax - depends on subtotal (automatic validation)
			.Do(p => p.TaxAmount, CalculateTax)

			// Apply discount if provided - depends on subtotal (automatic validation)
			.DoIf(
				p => !string.IsNullOrEmpty(p.Request.DiscountCode),
				p => p.DiscountAmount,
				ApplyDiscount)

			// Calculate shipping cost - no dependencies
			.Do(p => p.ShippingCost, CalculateShipping)

			// Calculate total - depends on subtotal, tax, discount, shipping (automatic validation)
			.Do(p => p.TotalAmount, CalculateTotal)

			// Process payment - depends on total amount (automatic validation)
			.Do(p => p.IsPaymentProcessed, ProcessPayment)

			// Build the workflow
			.Build();
	}

	#region Validation Methods

	private static Option<SampleError> ValidateRequest(SampleRequest request)
	{
		if (string.IsNullOrEmpty(request.CustomerId))
		{
			return Option<SampleError>.Some(
				new SampleError("INVALID_CUSTOMER", "Customer ID is required"));
		}

		return Option<SampleError>.None();
	}

	#endregion

	#region Activity Methods

	private static Either<SampleError, SamplePayload> CalculateSubtotal(SamplePayload payload)
	{
		// In a real application, we would calculate this based on items in the request
		decimal subtotal = 100.00m;

		return Either<SampleError, SamplePayload>.FromRight(
			payload with { Subtotal = subtotal });
	}

	private static Either<SampleError, SamplePayload> CalculateTax(SamplePayload payload)
	{
		// Simple tax calculation at 7%
		decimal taxAmount = payload.Subtotal * 0.07m;

		return Either<SampleError, SamplePayload>.FromRight(
			payload with { TaxAmount = taxAmount });
	}

	private static Either<SampleError, SamplePayload> ApplyDiscount(SamplePayload payload)
	{
		// Simple discount logic
		string code = payload.Request.DiscountCode!.ToUpperInvariant();
		decimal discountAmount = 0;

		if (code == "SAVE10")
		{
			discountAmount = payload.Subtotal * 0.10m;
		}
		else if (code == "SAVE20")
		{
			discountAmount = payload.Subtotal * 0.20m;
		}
		else
		{
			return Either<SampleError, SamplePayload>.FromLeft(
				new SampleError("INVALID_DISCOUNT", "The discount code is invalid"));
		}

		return Either<SampleError, SamplePayload>.FromRight(
			payload with { DiscountAmount = discountAmount });
	}

	private static Either<SampleError, SamplePayload> CalculateShipping(SamplePayload payload)
	{
		// Fixed shipping cost
		decimal shippingCost = 5.99m;

		return Either<SampleError, SamplePayload>.FromRight(
			payload with { ShippingCost = shippingCost });
	}

	private static Either<SampleError, SamplePayload> CalculateTotal(SamplePayload payload)
	{
		decimal totalAmount =
			payload.Subtotal +
			payload.TaxAmount +
			payload.ShippingCost -
			payload.DiscountAmount;

		return Either<SampleError, SamplePayload>.FromRight(
			payload with { TotalAmount = totalAmount });
	}

	private static async Task<Either<SampleError, SamplePayload>> ProcessPayment(
		SamplePayload payload, CancellationToken cancellationToken)
	{
		// Simulate payment processing with a short delay
		await Task.Delay(500, cancellationToken);

		// For demo purposes, let's fail payments over $100
		if (payload.TotalAmount > 100)
		{
			return Either<SampleError, SamplePayload>.FromLeft(
				new SampleError("PAYMENT_DECLINED", "Payment declined for amounts over $100"));
		}

		return Either<SampleError, SamplePayload>.FromRight(
			payload with { IsPaymentProcessed = true });
	}

	#endregion
}

/// <summary>
/// Sample success result type.
/// </summary>
public record SampleResult(
	Guid TransactionId,
	decimal TotalAmount,
	bool IsPaymentProcessed);