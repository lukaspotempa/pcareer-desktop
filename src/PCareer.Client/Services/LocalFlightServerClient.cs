using PCareer.Client.Models;

namespace PCareer.Client.Services;

public sealed class LocalFlightServerClient : IFlightServerClient
{
    public Task<Guid> StartFlightAsync(
        ContractAssignment contract,
        TelemetrySnapshot initialTelemetry,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(Guid.NewGuid());

    public void QueueTelemetry(Guid flightId, TelemetrySnapshot telemetry)
    {
        // Phase 2 replaces this adapter with buffered HTTPS telemetry batches.
    }

    public Task FinishFlightAsync(
        Guid flightId,
        TelemetrySnapshot finalTelemetry,
        CancellationToken cancellationToken = default) => Task.CompletedTask;
}

