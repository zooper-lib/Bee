using System;
using System.Threading.Tasks;
using FluentAssertions;
using Xunit;
using Zooper.Fox;

namespace Zooper.Bee.Tests;

public class WorkflowWithContextTests
{
	#region Test Models
	// Request model
	private record ProductRequest(int Id, string Name, decimal Price, bool NeedsCustomProcessing);

	// Main workflow payload model
	private record ProductPayload(
		int Id,
		string Name,
		decimal Price,
		bool NeedsCustomProcessing,
		string? ProcessingResult = null,
		string? CustomizationDetails = null,
		decimal FinalPrice = 0);

	// Local payload for customization context
	private record CustomizationPayload(
		string[] AvailableOptions,
		string[] SelectedOptions,
		decimal CustomizationCost,
		string CustomizationDetails);

	// Success result model
	private record ProductResult(
		int Id,
		string Name,
		decimal FinalPrice,
		string? ProcessingResult,
		string? CustomizationDetails);

	// Error model
	private record ProductError(string Code, string Message);
	#endregion

	[Fact]
	public async Task WithContext_ExecutesWhenConditionIsTrue()
	{
		// Arrange
		var workflow = new WorkflowBuilder<ProductRequest, ProductPayload, ProductResult, ProductError>(
			// Create the main payload from the request
			request => new ProductPayload(
				request.Id,
				request.Name,
				request.Price,
				request.NeedsCustomProcessing),

			// Create the result from the final payload
			payload => new ProductResult(
				payload.Id,
				payload.Name,
				payload.FinalPrice,
				payload.ProcessingResult,
				payload.CustomizationDetails)
		)
		.Do(payload =>
		{
			// Initial processing
			return Either<ProductError, ProductPayload>.FromRight(payload with
			{
				ProcessingResult = "Standard processing complete",
				FinalPrice = payload.Price
			});
		})
		// Context with local payload for products that need customization
		.WithContext(
			// Condition: Product needs custom processing
			payload => payload.NeedsCustomProcessing,

			// Create the local customization payload
			payload => new CustomizationPayload(
				AvailableOptions: new[] { "Engraving", "Gift Wrap", "Extended Warranty" },
				SelectedOptions: new[] { "Engraving", "Gift Wrap" },
				CustomizationCost: 25.99m,
				CustomizationDetails: "Custom initialized"
			),

			// Context configuration
			context => context
				// First customization activity - process options
				.Do((mainPayload, localPayload) =>
				{
					// Process the selected options
					string optionsProcessed = string.Join(", ", localPayload.SelectedOptions);

					// Update both payloads
					var updatedLocalPayload = localPayload with
					{
						CustomizationDetails = $"Options: {optionsProcessed}"
					};

					return Either<ProductError, (ProductPayload, CustomizationPayload)>.FromRight(
						(mainPayload, updatedLocalPayload));
				})
				// Second customization activity - apply costs and finalize customization
				.Do((mainPayload, localPayload) =>
				{
					// Calculate total price
					decimal totalPrice = mainPayload.Price + localPayload.CustomizationCost;

					// Update both payloads
					var updatedMainPayload = mainPayload with
					{
						FinalPrice = totalPrice,
						CustomizationDetails = localPayload.CustomizationDetails,
						ProcessingResult = $"{mainPayload.ProcessingResult} with customization"
					};

					return Either<ProductError, (ProductPayload, CustomizationPayload)>.FromRight(
						(updatedMainPayload, localPayload));
				})
		)
		.Build();

		var customizableProduct = new ProductRequest(1001, "Custom Widget", 99.99m, true);
		var standardProduct = new ProductRequest(1002, "Standard Widget", 49.99m, false);

		// Act
		var customResult = await workflow.Execute(customizableProduct);
		var standardResult = await workflow.Execute(standardProduct);

		// Assert

		// Custom product should go through customization
		customResult.IsRight.Should().BeTrue();
		customResult.Right.FinalPrice.Should().Be(125.98m); // 99.99 + 25.99
		customResult.Right.ProcessingResult.Should().Be("Standard processing complete with customization");
		customResult.Right.CustomizationDetails.Should().Be("Options: Engraving, Gift Wrap");

		// Standard product should not go through customization
		standardResult.IsRight.Should().BeTrue();
		standardResult.Right.FinalPrice.Should().Be(49.99m); // Just base price
		standardResult.Right.ProcessingResult.Should().Be("Standard processing complete");
		standardResult.Right.CustomizationDetails.Should().BeNull();
	}

	[Fact]
	public async Task WithContext_LocalPayloadIsolated_NotAffectedByOtherActivities()
	{
		// Arrange
		var workflow = new WorkflowBuilder<ProductRequest, ProductPayload, ProductResult, ProductError>(
			request => new ProductPayload(request.Id, request.Name, request.Price, request.NeedsCustomProcessing),
			payload => new ProductResult(
				payload.Id, payload.Name, payload.FinalPrice,
				payload.ProcessingResult, payload.CustomizationDetails)
		)
		.Do(payload => Either<ProductError, ProductPayload>.FromRight(payload with
		{
			ProcessingResult = "Initial processing",
			FinalPrice = payload.Price
		}))
		.WithContext(
			// Condition
			payload => true,

			// Create local payload
			_ => new CustomizationPayload(
				AvailableOptions: new[] { "Option1", "Option2" },
				SelectedOptions: new[] { "Option1" },
				CustomizationCost: 10.00m,
				CustomizationDetails: "Context 1 customization"
			),

			// Context configuration
			context => context
				.Do((mainPayload, localPayload) =>
				{
					var updatedMainPayload = mainPayload with
					{
						ProcessingResult = mainPayload.ProcessingResult + " -> Context 1",
						FinalPrice = mainPayload.FinalPrice + localPayload.CustomizationCost,
						CustomizationDetails = localPayload.CustomizationDetails
					};

					return Either<ProductError, (ProductPayload, CustomizationPayload)>.FromRight(
						(updatedMainPayload, localPayload));
				})
		)
		// Another main activity that changes the main payload but shouldn't affect the next context's local payload
		.Do(payload => Either<ProductError, ProductPayload>.FromRight(payload with
		{
			ProcessingResult = payload.ProcessingResult + " -> Main activity"
		}))
		.WithContext(
			// Second context
			payload => true,

			// Create a different local payload
			_ => new CustomizationPayload(
				AvailableOptions: new[] { "OptionA", "OptionB" },
				SelectedOptions: new[] { "OptionA", "OptionB" },
				CustomizationCost: 20.00m,
				CustomizationDetails: "Context 2 customization"
			),

			// Context configuration
			context => context
				.Do((mainPayload, localPayload) =>
				{
					var updatedMainPayload = mainPayload with
					{
						ProcessingResult = mainPayload.ProcessingResult + " -> Context 2",
						FinalPrice = mainPayload.FinalPrice + localPayload.CustomizationCost,
						CustomizationDetails = mainPayload.CustomizationDetails + " + " + localPayload.CustomizationDetails
					};

					return Either<ProductError, (ProductPayload, CustomizationPayload)>.FromRight(
						(updatedMainPayload, localPayload));
				})
		)
		.Build();

		var request = new ProductRequest(1001, "Test Product", 100.00m, true);

		// Act
		var result = await workflow.Execute(request);

		// Assert
		result.IsRight.Should().BeTrue();
		result.Right.ProcessingResult.Should().Be("Initial processing -> Main activity -> Context 1 -> Context 2");
		result.Right.FinalPrice.Should().Be(130.00m); // 100 + 10 + 20
		result.Right.CustomizationDetails.Should().Be("Context 1 customization + Context 2 customization");
	}

	[Fact]
	public async Task WithContext_ErrorInContext_StopsExecutionAndReturnsError()
	{
		// Arrange
		var workflow = new WorkflowBuilder<ProductRequest, ProductPayload, ProductResult, ProductError>(
			request => new ProductPayload(request.Id, request.Name, request.Price, request.NeedsCustomProcessing),
			payload => new ProductResult(
				payload.Id, payload.Name, payload.FinalPrice,
				payload.ProcessingResult, payload.CustomizationDetails)
		)
		.Do(payload => Either<ProductError, ProductPayload>.FromRight(
			payload with { ProcessingResult = "Initial processing" }))
		.WithContext(
			payload => payload.NeedsCustomProcessing,
			_ => new CustomizationPayload(
				AvailableOptions: new string[0],
				SelectedOptions: new[] { "Unavailable Option" }, // This will cause an error
				CustomizationCost: 10.00m,
				CustomizationDetails: "Should fail"
			),
			context => context
				.Do((mainPayload, localPayload) =>
				{
					// Validate selected options are available
					foreach (var option in localPayload.SelectedOptions)
					{
						if (Array.IndexOf(localPayload.AvailableOptions, option) < 0)
						{
							return Either<ProductError, (ProductPayload, CustomizationPayload)>.FromLeft(
								new ProductError("INVALID_OPTION", $"Option '{option}' is not available"));
						}
					}

					return Either<ProductError, (ProductPayload, CustomizationPayload)>.FromRight(
						(mainPayload, localPayload));
				})
		)
		.Do(payload => Either<ProductError, ProductPayload>.FromRight(
			payload with { ProcessingResult = payload.ProcessingResult + " -> Final processing" }))
		.Build();

		var request = new ProductRequest(1001, "Test Product", 100.00m, true);

		// Act
		var result = await workflow.Execute(request);

		// Assert
		result.IsLeft.Should().BeTrue();
		result.Left.Code.Should().Be("INVALID_OPTION");
		result.Left.Message.Should().Be("Option 'Unavailable Option' is not available");
	}

	[Fact]
	public async Task WithContext_MultipleActivitiesInSameContext_ShareLocalPayload()
	{
		// Arrange
		var workflow = new WorkflowBuilder<ProductRequest, ProductPayload, ProductResult, ProductError>(
			request => new ProductPayload(request.Id, request.Name, request.Price, request.NeedsCustomProcessing),
			payload => new ProductResult(
				payload.Id, payload.Name, payload.FinalPrice,
				payload.ProcessingResult, payload.CustomizationDetails)
		)
		.WithContext(
			_ => true,
			_ => new CustomizationPayload(
				AvailableOptions: new[] { "Option1", "Option2", "Option3" },
				SelectedOptions: new string[0], // Start with no selected options
				CustomizationCost: 0m,          // Start with no cost
				CustomizationDetails: "Start"
			),
			context => context
				// First activity - select Option1
				.Do((mainPayload, localPayload) =>
				{
					var updatedOptions = new string[localPayload.SelectedOptions.Length + 1];
					Array.Copy(localPayload.SelectedOptions, updatedOptions, localPayload.SelectedOptions.Length);
					updatedOptions[updatedOptions.Length - 1] = "Option1";

					var updatedLocalPayload = localPayload with
					{
						SelectedOptions = updatedOptions,
						CustomizationCost = localPayload.CustomizationCost + 10m,
						CustomizationDetails = localPayload.CustomizationDetails + " -> Added Option1"
					};

					return Either<ProductError, (ProductPayload, CustomizationPayload)>.FromRight(
						(mainPayload, updatedLocalPayload));
				})
				// Second activity - select Option2
				.Do((mainPayload, localPayload) =>
				{
					var updatedOptions = new string[localPayload.SelectedOptions.Length + 1];
					Array.Copy(localPayload.SelectedOptions, updatedOptions, localPayload.SelectedOptions.Length);
					updatedOptions[updatedOptions.Length - 1] = "Option2";

					var updatedLocalPayload = localPayload with
					{
						SelectedOptions = updatedOptions,
						CustomizationCost = localPayload.CustomizationCost + 15m,
						CustomizationDetails = localPayload.CustomizationDetails + " -> Added Option2"
					};

					return Either<ProductError, (ProductPayload, CustomizationPayload)>.FromRight(
						(mainPayload, updatedLocalPayload));
				})
				// Third activity - finalize and update main payload
				.Do((mainPayload, localPayload) =>
				{
					var updatedMainPayload = mainPayload with
					{
						FinalPrice = mainPayload.Price + localPayload.CustomizationCost,
						CustomizationDetails = localPayload.CustomizationDetails,
						ProcessingResult = $"Processed with {localPayload.SelectedOptions.Length} options"
					};

					return Either<ProductError, (ProductPayload, CustomizationPayload)>.FromRight(
						(updatedMainPayload, localPayload));
				})
		)
		.Build();

		var request = new ProductRequest(1001, "Test Product", 100.00m, true);

		// Act
		var result = await workflow.Execute(request);

		// Assert
		result.IsRight.Should().BeTrue();
		result.Right.FinalPrice.Should().Be(125.00m); // 100 + 10 + 15
		result.Right.ProcessingResult.Should().Be("Processed with 2 options");
		result.Right.CustomizationDetails.Should().Be("Start -> Added Option1 -> Added Option2");
	}

	[Fact]
	public async Task WithContext_UnconditionalContext_AlwaysExecutes()
	{
		// Arrange
		var workflow = new WorkflowBuilder<ProductRequest, ProductPayload, ProductResult, ProductError>(
			request => new ProductPayload(request.Id, request.Name, request.Price, request.NeedsCustomProcessing),
			payload => new ProductResult(
				payload.Id, payload.Name, payload.FinalPrice,
				payload.ProcessingResult, payload.CustomizationDetails)
		)
		.WithContext(
			// Local payload factory only
			_ => new CustomizationPayload(
				AvailableOptions: new[] { "Default Option" },
				SelectedOptions: new[] { "Default Option" },
				CustomizationCost: 5.00m,
				CustomizationDetails: "Default customization"
			),
			context => context
				.Do((mainPayload, localPayload) =>
				{
					var updatedMainPayload = mainPayload with
					{
						FinalPrice = mainPayload.Price + localPayload.CustomizationCost,
						CustomizationDetails = localPayload.CustomizationDetails,
						ProcessingResult = "Processed with default customization"
					};

					return Either<ProductError, (ProductPayload, CustomizationPayload)>.FromRight(
						(updatedMainPayload, localPayload));
				})
		)
		.Build();

		var request = new ProductRequest(1001, "Test Product", 100.00m, false);

		// Act
		var result = await workflow.Execute(request);

		// Assert
		result.IsRight.Should().BeTrue();
		result.Right.FinalPrice.Should().Be(105.00m); // 100 + 5
		result.Right.ProcessingResult.Should().Be("Processed with default customization");
		result.Right.CustomizationDetails.Should().Be("Default customization");
	}

	[Fact]
	public async Task WithContext_UnconditionalContextFluentApi_AlwaysExecutes()
	{
		// Arrange
		var workflow = new WorkflowBuilder<ProductRequest, ProductPayload, ProductResult, ProductError>(
			request => new ProductPayload(request.Id, request.Name, request.Price, request.NeedsCustomProcessing),
			payload => new ProductResult(
				payload.Id, payload.Name, payload.FinalPrice,
				payload.ProcessingResult, payload.CustomizationDetails)
		)
		.WithContext(
			// Local payload factory only (no condition parameter)
			_ => new CustomizationPayload(
				AvailableOptions: new[] { "Default Option" },
				SelectedOptions: new[] { "Default Option" },
				CustomizationCost: 5.00m,
				CustomizationDetails: "Default customization (fluent API)"
			),
			// Use callback pattern instead of fluent API
			context => context.Do((mainPayload, localPayload) =>
			{
				var updatedMainPayload = mainPayload with
				{
					FinalPrice = mainPayload.Price + localPayload.CustomizationCost,
					CustomizationDetails = localPayload.CustomizationDetails,
					ProcessingResult = "Processed with fluent API"
				};

				return Either<ProductError, (ProductPayload, CustomizationPayload)>.FromRight(
					(updatedMainPayload, localPayload));
			})
		)
		.Build();

		var request = new ProductRequest(1001, "Test Product", 100.00m, false);

		// Act
		var result = await workflow.Execute(request);

		// Assert
		result.IsRight.Should().BeTrue();
		result.Right.FinalPrice.Should().Be(105.00m); // 100 + 5
		result.Right.ProcessingResult.Should().Be("Processed with fluent API");
		result.Right.CustomizationDetails.Should().Be("Default customization (fluent API)");
	}
}