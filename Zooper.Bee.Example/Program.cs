using System;
using System.Threading.Tasks;
using Zooper.Bee;
using Zooper.Bee.Extensions;
using Zooper.Fox;

// ===== Entry point =====
await RunOrderProcessingExample();
await RunBranchingExample();
await RunRecoveryExample();
await RunParameterlessExample();
await RunPollForReadyExample();
await RunRetryWithMutationExample();
Console.WriteLine("\nAll examples complete.");
return;

static async Task RunOrderProcessingExample()
{
    Console.WriteLine("=== Order Processing ===");
    var railway = Railway.Create<OrderRequest, OrderPayload, OrderSuccess, OrderError>(
        r => new OrderPayload(r.CustomerId, r.Amount),
        p => new OrderSuccess(p.OrderId ?? "N/A", p.Status ?? "Unknown"),
        guards => guards
            .Guard(r => string.IsNullOrWhiteSpace(r.CustomerId)
                ? Either<OrderError, Unit>.FromLeft(new OrderError("INVALID_CUSTOMER", "Customer ID is required"))
                : Either<OrderError, Unit>.FromRight(Unit.Value)),
        steps => steps
            .Ensure(p => p.Amount > 0, p => new OrderError("INVALID_AMOUNT", "Amount must be positive"))
            .Do(p => Either<OrderError, OrderPayload>.FromRight(
                p with { OrderId = $"ORD-{DateTime.UtcNow:yyyyMMddHHmmss}", Status = "Created" }))
            .Tap(p => Console.WriteLine($"  Order created: {p.OrderId}"))
            .Do(async (p, ct) =>
            {
                await Task.Delay(10, ct);
                return Either<OrderError, OrderPayload>.FromRight(p with { Status = "Confirmed" });
            })
            .Finally(p => Console.WriteLine($"  Finally: order {p.OrderId} is {p.Status}")));

    var r1 = await railway.Execute(new OrderRequest("CUST-001", 99.99m));
    Console.WriteLine(r1.IsRight
        ? $"  Success: {r1.Right.OrderId} / {r1.Right.Status}"
        : $"  Error: {r1.Left.Code}");

    var r2 = await railway.Execute(new OrderRequest("", 10m));
    Console.WriteLine($"  Empty customer: {r2.Left.Code}");
}

static async Task RunBranchingExample()
{
    Console.WriteLine("\n=== Branching ===");
    var railway = Railway.Create<ShipRequest, ShipPayload, ShipSuccess, ShipError>(
        r => new ShipPayload(r.Category, r.Weight),
        p => new ShipSuccess(p.Method ?? "Standard", p.ShippingCost),
        steps => steps
            .When(
                p => p.Category == "Express",
                br => br
                    .Ensure(p => p.Weight <= 30, p => new ShipError("OVERWEIGHT"))
                    .Do(p => Either<ShipError, ShipPayload>.FromRight(
                        p with { Method = "Express", ShippingCost = p.Weight * 5m })))
            .When(
                p => p.Category == "Standard",
                br => br
                    .Do(p => Either<ShipError, ShipPayload>.FromRight(
                        p with { Method = "Standard", ShippingCost = p.Weight * 1.5m }))));

    var express = await railway.Execute(new ShipRequest("Express", 10));
    Console.WriteLine($"  Express 10kg: {express.Right.Method} ${express.Right.Cost:F2}");
    var standard = await railway.Execute(new ShipRequest("Standard", 50));
    Console.WriteLine($"  Standard 50kg: {standard.Right.Method} ${standard.Right.Cost:F2}");
    var overweight = await railway.Execute(new ShipRequest("Express", 50));
    Console.WriteLine($"  Express 50kg (overweight): Error {overweight.Left.Code}");
}

static async Task RunRecoveryExample()
{
    Console.WriteLine("\n=== Recovery ===");
    var railway = Railway.Create<PayRequest, PayPayload, PaySuccess, PayError>(
        r => new PayPayload(r.UserId, r.Amount),
        p => new PaySuccess(p.Note ?? "ok"),
        steps => steps
            .Do(p => p.Amount > 100
                ? Either<PayError, PayPayload>.FromLeft(
                    new InsufficientFundsError("INSUFFICIENT_FUNDS", p.Amount - 100))
                : Either<PayError, PayPayload>.FromRight(p with { Note = "Paid" }))
            .Recover<InsufficientFundsError>((err, last) =>
                last with { Note = $"Partial; shortfall was {err.Shortfall:C}" }));

    var ok = await railway.Execute(new PayRequest("user1", 50));
    Console.WriteLine($"  $50: {ok.Right.Note}");
    var recovered = await railway.Execute(new PayRequest("user2", 150));
    Console.WriteLine($"  $150: {recovered.Right.Note}");
}

static async Task RunParameterlessExample()
{
    Console.WriteLine("\n=== Parameterless Railway ===");
    var railway = Railway.Create<SyncPayload, SyncSuccess, SyncError>(
        () => new SyncPayload(),
        p => new SyncSuccess(p.Counter),
        steps => steps
            .Do(p => Either<SyncError, SyncPayload>.FromRight(p with { Counter = p.Counter + 1 }))
            .Do(p => Either<SyncError, SyncPayload>.FromRight(p with { Counter = p.Counter + 1 }))
            .Do(p => Either<SyncError, SyncPayload>.FromRight(p with { Counter = p.Counter + 1 })));

    var result = await railway.Execute();
    Console.WriteLine($"  Counter after 3 steps: {result.Right.FinalCount}");
}

// ── Loop: poll-for-ready ─────────────────────────────────────────────────────

static async Task RunPollForReadyExample()
{
    Console.WriteLine("\n=== Loop: Poll-for-Ready ===");

    // Simulate a job that becomes ready on the 3rd poll.
    var railway = Railway.Create<PollRequest, PollPayload, PollSuccess, PollError>(
        r => new PollPayload(r.JobId),
        p => new PollSuccess(p.JobId, p.PollCount),
        steps => steps
            .Loop(
                body: lb => lb
                    .Do(async (p, ct) =>
                    {
                        // Simulate checking job readiness.
                        await Task.Delay(5, ct);
                        var ready = p.PollCount + 1 >= 3;
                        Console.WriteLine($"  Poll {p.PollCount + 1}: ready={ready}");
                        return Either<PollError, PollPayload>.FromRight(
                            p with { PollCount = p.PollCount + 1, Ready = ready });
                    }),
                until: (p, _) => p.Ready,
                maxAttempts: 10,
                exhausted: (p, a) => new PollError("TIMEOUT", a)));

    var result = await railway.Execute(new PollRequest("job-abc"));
    Console.WriteLine(result.IsRight
        ? $"  Job ready after {result.Right.PollCount} poll(s)"
        : $"  Timed out after {result.Left!.Attempts} attempt(s)");
}

// ── Loop: retry-with-mutation ────────────────────────────────────────────────

static async Task RunRetryWithMutationExample()
{
    Console.WriteLine("\n=== Loop: Retry-with-Mutation ===");

    // Simulate a flaky service: fails (503) on attempts 1 and 2, succeeds on 3.
    var railway = Railway.Create<RetryRequest, RetryPayload, RetrySuccess, RetryError>(
        r => new RetryPayload(r.Endpoint),
        p => new RetrySuccess(p.Response!, p.Attempt),
        steps => steps
            .Loop(
                body: lb => lb
                    .Do(async (p, ct) =>
                    {
                        // Simulate network call with optional back-off delay.
                        if (p.DelayMs > 0) await Task.Delay(p.DelayMs, ct);
                        Console.WriteLine($"  Attempt {p.Attempt + 1} → calling {p.Endpoint}");
                        // Fail on attempts 1 and 2.
                        if (p.Attempt < 2)
                            return Either<RetryError, RetryPayload>.FromLeft(
                                new ServiceUnavailableError("SERVICE_UNAVAILABLE", 503));
                        return Either<RetryError, RetryPayload>.FromRight(
                            p with { Attempt = p.Attempt + 1, Response = "OK" });
                    })
                    // Recover per-iteration: a 503 is transient — keep iterating.
                    .Recover<ServiceUnavailableError>((err, last) =>
                    {
                        Console.WriteLine($"  Transient {err.StatusCode} on attempt {last.Attempt + 1} — will retry");
                        return last with { Attempt = last.Attempt + 1 };
                    }),
                until: (p, _) => p.Response != null,
                maxAttempts: 5,
                exhausted: (p, a) => new RetryError("MAX_RETRIES", a),
                // Mutate: increase delay between iterations (simple linear back-off).
                mutate: (p, a) => p with { DelayMs = a * 10 }));

    var result = await railway.Execute(new RetryRequest("https://api.example.com/data"));
    Console.WriteLine(result.IsRight
        ? $"  Succeeded: '{result.Right.Response}' after {result.Right.Attempts} attempt(s)"
        : $"  Failed: {result.Left!.Code} after {result.Left.Attempts} attempt(s)");
}

// ===== Records =====
record OrderRequest(string CustomerId, decimal Amount);
record OrderPayload(string CustomerId, decimal Amount, string? OrderId = null, string? Status = null);
record OrderSuccess(string OrderId, string Status);
record OrderError(string Code, string Message);

record ShipRequest(string Category, decimal Weight);
record ShipPayload(string Category, decimal Weight, decimal ShippingCost = 0m, string? Method = null);
record ShipSuccess(string Method, decimal Cost);
record ShipError(string Code);

record PayRequest(string UserId, decimal Amount);
record PayPayload(string UserId, decimal Amount, string? Note = null);
record PaySuccess(string Note);
record PayError(string Code);
record InsufficientFundsError(string Code, decimal Shortfall) : PayError(Code);

record SyncPayload(int Counter = 0);
record SyncSuccess(int FinalCount);
record SyncError(string Code);

record PollRequest(string JobId);
record PollPayload(string JobId, int PollCount = 0, bool Ready = false);
record PollSuccess(string JobId, int PollCount);
record PollError(string Code, int Attempts);

record RetryRequest(string Endpoint);
record RetryPayload(string Endpoint, int Attempt = 0, string? Response = null, int DelayMs = 0);
record RetrySuccess(string Response, int Attempts);
record RetryError(string Code, int Attempts);
record ServiceUnavailableError(string Code, int StatusCode) : RetryError(Code, 0);
