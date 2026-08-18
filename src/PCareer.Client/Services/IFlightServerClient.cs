using PCareer.Client.Models;

namespace PCareer.Client.Services;

public interface IFlightServerClient
{
    Task<Guid> StartFlightAsync(
        ContractAssignment contract,
        TelemetrySnapshot initialTelemetry,
        CancellationToken cancellationToken = default);

    void QueueTelemetry(Guid flightId, TelemetrySnapshot telemetry);

    Task FinishFlightAsync(
        Guid flightId,
        TelemetrySnapshot finalTelemetry,
        CancellationToken cancellationToken = default);
}

