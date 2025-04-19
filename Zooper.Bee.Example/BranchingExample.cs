using System;
using System.Threading.Tasks;
using Zooper.Bee;
using Zooper.Fox;

namespace Zooper.Bee.Example;

public class BranchingExample
{
	// Request models
	public record UserRegistrationRequest(
		string Username,
		string Email,
		bool IsVipMember);

	// Success model
	public record RegistrationResult(
		string Username,
		string Email,
		string AccountType,
		bool WelcomeEmailSent,
		bool VipBenefitsActivated);

	// Error model
	public record RegistrationError(string Code, string Message);

	// Payload model
	public record RegistrationPayload(
		string Username,
		string Email,
		bool IsVipMember,
		bool IsRegistered = false,
		bool WelcomeEmailSent = false,
		bool VipBenefitsActivated = false,
		string AccountType = "Standard");

	public static async Task RunExample()
	{
		Console.WriteLine("\n=== Workflow Branching Example ===\n");

		// Create sample requests
		var standardUser = new UserRegistrationRequest("john_doe", "john@example.com", false);
		var vipUser = new UserRegistrationRequest("jane_smith", "jane@example.com", true);

		// Build the registration workflow
		var workflow = CreateRegistrationWorkflow();

		// Process the standard user
		Console.WriteLine("Processing standard user registration:");
		await ProcessRegistration(workflow, standardUser);

		Console.WriteLine();

		// Process the VIP user
		Console.WriteLine("Processing VIP user registration:");
		await ProcessRegistration(workflow, vipUser);
	}

	private static async Task ProcessRegistration(
		Workflow<UserRegistrationRequest, RegistrationResult, RegistrationError> workflow,
		UserRegistrationRequest request)
	{
		var result = await workflow.Execute(request);

		if (result.IsRight)
		{
			var registration = result.Right;
			Console.WriteLine($"Registration successful for {registration.Username}");
			Console.WriteLine($"Account Type: {registration.AccountType}");
			Console.WriteLine($"Welcome Email Sent: {registration.WelcomeEmailSent}");
			Console.WriteLine($"VIP Benefits Activated: {registration.VipBenefitsActivated}");
		}
		else
		{
			var error = result.Left;
			Console.WriteLine($"Registration failed: [{error.Code}] {error.Message}");
		}
	}

	private static Workflow<UserRegistrationRequest, RegistrationResult, RegistrationError> CreateRegistrationWorkflow()
	{
		return new WorkflowBuilder<UserRegistrationRequest, RegistrationPayload, RegistrationResult, RegistrationError>(
			// Create initial payload from request
			request => new RegistrationPayload(
				request.Username,
				request.Email,
				request.IsVipMember),

			// Create result from final payload
			payload => new RegistrationResult(
				payload.Username,
				payload.Email,
				payload.AccountType,
				payload.WelcomeEmailSent,
				payload.VipBenefitsActivated)
		)
		// Validate email format
		.Validate(request =>
		{
			if (!request.Email.Contains('@'))
			{
				return Option<RegistrationError>.Some(
					new RegistrationError("INVALID_EMAIL", "Email address is not in a valid format"));
			}

			return Option<RegistrationError>.None();
		})
		// Register the user
		.Do(payload =>
		{
			Console.WriteLine($"Registering user {payload.Username}...");

			// Simulate registration
			return Either<RegistrationError, RegistrationPayload>.FromRight(
				payload with { IsRegistered = true });
		})
		// Branch the workflow based on membership type
		.Branch(
			payload => payload.IsVipMember,
			branch => branch
				.Do(payload =>
				{
					Console.WriteLine("Activating VIP benefits...");

					return Either<RegistrationError, RegistrationPayload>.FromRight(
						payload with
						{
							VipBenefitsActivated = true,
							AccountType = "VIP"
						});
				})
				.Do(payload =>
				{
					Console.WriteLine("Setting up premium support access...");

					return Either<RegistrationError, RegistrationPayload>.FromRight(payload);
				})
		)
		// Branch for standard users
		.Branch(
			payload => !payload.IsVipMember,
			branch => branch
				.Do(payload =>
				{
					Console.WriteLine("Setting up standard account features...");

					return Either<RegistrationError, RegistrationPayload>.FromRight(payload);
				})
		)
		// Send welcome email to all users
		.Do(payload =>
		{
			Console.WriteLine($"Sending welcome email to {payload.Email}...");

			// Simulate sending email
			return Either<RegistrationError, RegistrationPayload>.FromRight(
				payload with { WelcomeEmailSent = true });
		})
		// Finally log the registration
		.Finally(payload =>
		{
			Console.WriteLine($"Logging registration of {payload.Username} ({payload.AccountType} account)");
			return Either<RegistrationError, RegistrationPayload>.FromRight(payload);
		})
		.Build();
	}
}