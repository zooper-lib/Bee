using System;
using System.Threading.Tasks;
using FluentAssertions;
using Xunit;
using Zooper.Fox;

namespace Zooper.Bee.Tests;

public class BranchWithLocalPayloadTests
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
		decimal FinalPrice = 0);

	// Local payload for customization context
	private record CustomizationPayload(
		string[] AvailableOptions,
		string[] SelectedOptions,
		decimal CustomizationCost);

	// Success result model
	private record ProductResult(
		int Id,
		string Name,
		decimal FinalPrice,
		string? ProcessingResult);

	// Error model
	private record ProductError(string Code, string Message);
	#endregion

	[Fact]
	public async Task BranchWithLocalPayload_ExecutesWhenConditionIsTrue()
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
				payload.ProcessingResult)
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
				CustomizationCost: 25.99m
			),

			// Context configuration
			context => context
				// Apply customization costs
				.Do((mainPayload, localPayload) =>
				{
					// Calculate total price
					decimal totalPrice = mainPayload.Price + localPayload.CustomizationCost;

					// Update the main payload with customization results
					var updatedMainPayload = mainPayload with
					{
						FinalPrice = totalPrice,
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

		// Standard product should not go through customization
		standardResult.IsRight.Should().BeTrue();
		standardResult.Right.FinalPrice.Should().Be(49.99m); // Just base price
		standardResult.Right.ProcessingResult.Should().Be("Standard processing complete");
	}

	[Fact]
	public async Task BranchWithLocalPayload_LocalPayloadIsolated_NotAffectedByOtherActivities()
	{
		// Arrange
		var workflow = new WorkflowBuilder<ProductRequest, ProductPayload, ProductResult, ProductError>(
			request => new ProductPayload(request.Id, request.Name, request.Price, request.NeedsCustomProcessing),
			payload => new ProductResult(
				payload.Id, payload.Name, payload.FinalPrice, payload.ProcessingResult)
		)
		.Do(payload => Either<ProductError, ProductPayload>.FromRight(payload with
		{
			ProcessingResult = "Initial processing",
			FinalPrice = payload.Price
		}))
		// First context
		.WithContext(
			// Always execute
			payload => true,

			// Create local payload for first context
			_ => new CustomizationPayload(
				AvailableOptions: new[] { "Option1", "Option2" },
				SelectedOptions: new[] { "Option1" },
				CustomizationCost: 10.00m
			),

			// Configure first context
			context => context
				.Do((mainPayload, localPayload) =>
				{
					var updatedMainPayload = mainPayload with
					{
						ProcessingResult = mainPayload.ProcessingResult + " -> Context 1",
						FinalPrice = mainPayload.FinalPrice + localPayload.CustomizationCost
					};

					return Either<ProductError, (ProductPayload, CustomizationPayload)>.FromRight(
						(updatedMainPayload, localPayload));
				})
		)
		// Main activity that changes the main payload but shouldn't affect the next context's local payload
		.Do(payload => Either<ProductError, ProductPayload>.FromRight(payload with
		{
			ProcessingResult = payload.ProcessingResult + " -> Main activity"
		}))
		// Second context - should have its own isolated local payload
		.WithContext(
			// Always execute
			payload => true,

			// Create a different local payload
			_ => new CustomizationPayload(
				AvailableOptions: new[] { "OptionA", "OptionB" },
				SelectedOptions: new[] { "OptionA", "OptionB" },
				CustomizationCost: 20.00m
			),

			// Configure second context
			context => context
				.Do((mainPayload, localPayload) =>
				{
					var updatedMainPayload = mainPayload with
					{
						ProcessingResult = mainPayload.ProcessingResult + " -> Context 2",
						FinalPrice = mainPayload.FinalPrice + localPayload.CustomizationCost
					};

					return Either<ProductError, (ProductPayload, CustomizationPayload)>.FromRight(
						(updatedMainPayload, localPayload));
				})
		)
		.Build();

		// Act
		var result = await workflow.Execute(new ProductRequest(1, "Test Product", 100.00m, false));

		// Assert
		result.IsRight.Should().BeTrue();
		result.Right.ProcessingResult.Should().Be("Initial processing -> Main activity -> Context 1 -> Context 2");
		result.Right.FinalPrice.Should().Be(130.00m); // Base (100) + Context 1 (10) + Context 2 (20)
	}

	[Fact]
	public async Task BranchWithLocalPayload_ErrorInBranch_StopsExecutionAndReturnsError()
	{
		// Arrange
		var workflow = new WorkflowBuilder<ProductRequest, ProductPayload, ProductResult, ProductError>(
			request => new ProductPayload(request.Id, request.Name, request.Price, request.NeedsCustomProcessing),
			payload => new ProductResult(
				payload.Id, payload.Name, payload.FinalPrice, payload.ProcessingResult)
		)
		.Do(payload => Either<ProductError, ProductPayload>.FromRight(payload with
		{
			ProcessingResult = "Initial processing",
			FinalPrice = payload.Price
		}))
		.WithContext(
			// Condition
			payload => payload.NeedsCustomProcessing,

			// Create local payload
			payload => new CustomizationPayload(
				AvailableOptions: new[] { "Option1", "Option2" },
				SelectedOptions: new[] { "Option1" },
				CustomizationCost: payload.Price * 0.10m // 10% surcharge
			),

			// Configure context
			context => context
				.Do((mainPayload, localPayload) =>
				{
					// Simulate error if price is too high
					if (mainPayload.Price + localPayload.CustomizationCost > 150)
					{
						return Either<ProductError, (ProductPayload, CustomizationPayload)>.FromLeft(
							new ProductError("PRICE_TOO_HIGH", "Product with customization exceeds price limit"));
					}

					var updatedMainPayload = mainPayload with
					{
						ProcessingResult = "Customization applied",
						FinalPrice = mainPayload.Price + localPayload.CustomizationCost
					};

					return Either<ProductError, (ProductPayload, CustomizationPayload)>.FromRight(
						(updatedMainPayload, localPayload));
				})
		)
		// This activity should not execute if the context returns an error
		.Do(payload => Either<ProductError, ProductPayload>.FromRight(payload with
		{
			ProcessingResult = payload.ProcessingResult + " -> Final processing"
		}))
		.Build();

		// Act
		var expensiveProduct = new ProductRequest(1, "Expensive Product", 150.00m, true);
		var result = await workflow.Execute(expensiveProduct);

		// Assert
		result.IsLeft.Should().BeTrue();
		result.Left.Code.Should().Be("PRICE_TOO_HIGH");
		result.Left.Message.Should().Be("Product with customization exceeds price limit");
	}

	[Fact]
	public async Task BranchWithLocalPayload_MultipleActivitiesInSameBranch_ShareLocalPayload()
	{
		// Arrange
		var workflow = new WorkflowBuilder<ProductRequest, ProductPayload, ProductResult, ProductError>(
			request => new ProductPayload(request.Id, request.Name, request.Price, request.NeedsCustomProcessing),
			payload => new ProductResult(
				payload.Id, payload.Name, payload.FinalPrice, payload.ProcessingResult)
		)
		.Do(payload => Either<ProductError, ProductPayload>.FromRight(payload with
		{
			ProcessingResult = "Initial processing",
			FinalPrice = payload.Price
		}))
		.WithContext(
			// Always execute
			payload => true,

			// Create local payload
			_ => new CustomizationPayload(
				AvailableOptions: new[] { "Option1", "Option2", "Option3" },
				SelectedOptions: Array.Empty<string>(), // Start with no selections
				CustomizationCost: 0 // Start with no cost
			),

			// Configure context with multiple activities that share local payload
			context => context
				// First activity selects options
				.Do((mainPayload, localPayload) =>
				{
					// Add options based on product price
					var selectedOptions = mainPayload.Price > 100
						? new[] { "Option1", "Option2" }
						: new[] { "Option1" };

					var updatedLocalPayload = localPayload with
					{
						SelectedOptions = selectedOptions
					};

					return Either<ProductError, (ProductPayload, CustomizationPayload)>.FromRight(
						(mainPayload, updatedLocalPayload));
				})
				// Second activity calculates cost based on selections
				.Do((mainPayload, localPayload) =>
				{
					// Calculate cost based on selected options
					decimal cost = localPayload.SelectedOptions.Length * 15.00m;

					var updatedLocalPayload = localPayload with
					{
						CustomizationCost = cost
					};

					return Either<ProductError, (ProductPayload, CustomizationPayload)>.FromRight(
						(mainPayload, updatedLocalPayload));
				})
				// Third activity applies the customization to the main payload
				.Do((mainPayload, localPayload) =>
				{
					string optionsDescription = string.Join(", ", localPayload.SelectedOptions);

					var updatedMainPayload = mainPayload with
					{
						ProcessingResult = $"Customized with options: {optionsDescription}",
						FinalPrice = mainPayload.Price + localPayload.CustomizationCost
					};

					return Either<ProductError, (ProductPayload, CustomizationPayload)>.FromRight(
						(updatedMainPayload, localPayload));
				})
		)
		.Build();

		// Act
		var expensiveProduct = new ProductRequest(1, "Expensive Product", 150.00m, true);
		var cheapProduct = new ProductRequest(2, "Cheap Product", 50.00m, true);

		var expensiveResult = await workflow.Execute(expensiveProduct);
		var cheapResult = await workflow.Execute(cheapProduct);

		// Assert
		expensiveResult.IsRight.Should().BeTrue();
		expensiveResult.Right.ProcessingResult.Should().Be("Customized with options: Option1, Option2");
		expensiveResult.Right.FinalPrice.Should().Be(180.00m); // 150 + (2 options * 15)

		cheapResult.IsRight.Should().BeTrue();
		cheapResult.Right.ProcessingResult.Should().Be("Customized with options: Option1");
		cheapResult.Right.FinalPrice.Should().Be(65.00m); // 50 + (1 option * 15)
	}

	[Fact]
	public async Task BranchWithLocalPayload_UnconditionalBranch_AlwaysExecutes()
	{
		// Arrange
		var workflow = new WorkflowBuilder<ProductRequest, ProductPayload, ProductResult, ProductError>(
			request => new ProductPayload(request.Id, request.Name, request.Price, request.NeedsCustomProcessing),
			payload => new ProductResult(
				payload.Id, payload.Name, payload.FinalPrice, payload.ProcessingResult)
		)
		// Use WithContext without condition (which means it always executes)
		.WithContext(
			// Create local payload
			_ => new CustomizationPayload(
				AvailableOptions: new[] { "Standard Option" },
				SelectedOptions: new[] { "Standard Option" },
				CustomizationCost: 5.00m
			),

			// Configure context
			context => context
				.Do((mainPayload, localPayload) =>
				{
					var updatedMainPayload = mainPayload with
					{
						ProcessingResult = "Standard processing applied",
						FinalPrice = mainPayload.Price + localPayload.CustomizationCost
					};

					return Either<ProductError, (ProductPayload, CustomizationPayload)>.FromRight(
						(updatedMainPayload, localPayload));
				})
		)
		.Build();

		// Act
		var product = new ProductRequest(1, "Test Product", 100.00m, false);
		var result = await workflow.Execute(product);

		// Assert
		result.IsRight.Should().BeTrue();
		result.Right.ProcessingResult.Should().Be("Standard processing applied");
		result.Right.FinalPrice.Should().Be(105.00m); // 100 + 5
	}
}