using Zooper.Fox;

namespace Zooper.Bee.Example;

public class BranchingExample
{
	// Request model
	public record RegistrationRequest(
		string Email,
		string Password,
		bool IsVipMember);

	// Success model
	public record RegistrationSuccess(
		Guid UserId,
		string Email,
		bool IsVipMember,
		string? WelcomeMessage);

	// Error model
	public record RegistrationError(string Code, string Message);

	// Registration payload model
	public record RegistrationPayload(
		Guid UserId,
		string Email,
		string Password,
		bool IsVipMember,
		string? WelcomeMessage = null);

	public static async Task RunExample()
	{
		Console.WriteLine("\n=== Workflow Grouping Example ===\n");

		// Create sample requests
		var standardUserRequest = new RegistrationRequest("user@example.com", "Password123!", false);
		var vipUserRequest = new RegistrationRequest("vip@example.com", "VIPPassword123!", true);
		var invalidEmailRequest = new RegistrationRequest("invalid-email", "Password123!", false);

		// Build the registration workflow
		var workflow = CreateRegistrationWorkflow();

		// Process standard user registration
		Console.WriteLine("Registering standard user:");
		await ProcessRegistration(workflow, standardUserRequest);

		Console.WriteLine();

		// Process VIP user registration
		Console.WriteLine("Registering VIP user:");
		await ProcessRegistration(workflow, vipUserRequest);

		Console.WriteLine();

		// Process invalid registration
		Console.WriteLine("Attempting to register user with invalid email:");
		await ProcessRegistration(workflow, invalidEmailRequest);
	}

	private static async Task ProcessRegistration(
		Workflow<RegistrationRequest, RegistrationSuccess, RegistrationError> workflow,
		RegistrationRequest request)
	{
		var result = await workflow.Execute(request);

		if (result.IsRight)
		{
			var success = result.Right;
			Console.WriteLine($"Registration successful for {success.Email}");
			Console.WriteLine($"User ID: {success.UserId}");
			Console.WriteLine($"VIP Member: {success.IsVipMember}");

			if (success.WelcomeMessage != null)
			{
				Console.WriteLine($"Welcome message: {success.WelcomeMessage}");
			}
		}
		else
		{
			var error = result.Left;
			Console.WriteLine($"Registration failed: [{error.Code}] {error.Message}");
		}
	}

	private static Workflow<RegistrationRequest, RegistrationSuccess, RegistrationError> CreateRegistrationWorkflow()
	{
		return new WorkflowBuilder<RegistrationRequest, RegistrationPayload, RegistrationSuccess, RegistrationError>(
			// Create initial payload from request
			request => new RegistrationPayload(
				Guid.NewGuid(),  // Generate a new unique ID
				request.Email,
				request.Password,
				request.IsVipMember),

			// Create result from final payload
			payload => new RegistrationSuccess(
				payload.UserId,
				payload.Email,
				payload.IsVipMember,
				payload.WelcomeMessage)
		)
		// Validate email format
		.Validate(request =>
		{
			if (!request.Email.Contains('@'))
			{
				return Option<RegistrationError>.Some(
					new RegistrationError("INVALID_EMAIL", "Email must contain @ symbol"));
			}

			return Option<RegistrationError>.None();
		})
		// Register the user
		.Do(payload =>
		{
			Console.WriteLine($"Registering user with email: {payload.Email}");

			// In a real app, this would save the user to a database
			return Either<RegistrationError, RegistrationPayload>.FromRight(payload);
		})
		// Conditional group for VIP members
		.Group(
			// Condition: only execute for VIP members
			payload => payload.IsVipMember,

			// Configure the group with VIP-specific activities
			group => group
				.Do(payload =>
				{
					Console.WriteLine("Activating VIP benefits...");

					// In a real app, this would activate VIP-specific features
					return Either<RegistrationError, RegistrationPayload>.FromRight(payload);
				})
				.Do(payload =>
				{
					Console.WriteLine("Sending VIP welcome package notification...");

					// Update the welcome message for VIP users
					return Either<RegistrationError, RegistrationPayload>.FromRight(
						payload with { WelcomeMessage = "Welcome to our VIP program! Your welcome package is on the way." });
				})
		)
		// Send welcome email to all users
		.Do(payload =>
		{
			Console.WriteLine($"Sending welcome email to: {payload.Email}");

			// Only set a default welcome message if one hasn't been set (for non-VIP users)
			if (payload.WelcomeMessage == null)
			{
				payload = payload with { WelcomeMessage = "Welcome to our platform!" };
			}

			return Either<RegistrationError, RegistrationPayload>.FromRight(payload);
		})
		// Log the registration
		.Finally(payload =>
		{
			Console.WriteLine($"Logging registration for user: {payload.Email} (ID: {payload.UserId})");

			// Return the unmodified payload to satisfy the lambda return type
			return Either<RegistrationError, RegistrationPayload>.FromRight(payload);
		})
		.Build();
	}
}