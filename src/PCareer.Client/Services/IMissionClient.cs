using PCareer.Client.Models;

namespace PCareer.Client.Services;

public interface IMissionClient
{
    Task<MissionState?> GetActiveMissionAsync(CancellationToken cancellationToken = default);

    Task<MissionState> AdvanceMissionAsync(
        string contractId,
        string phaseId,
        CancellationToken cancellationToken = default);

    Task<MissionState> EvaluateMissionTriggerAsync(
        string contractId,
        TelemetrySnapshot telemetry,
        CancellationToken cancellationToken = default);

    Task<MissionCompleteResult> CompleteMissionAsync(
        string contractId,
        string completionType,
        CancellationToken cancellationToken = default);
}