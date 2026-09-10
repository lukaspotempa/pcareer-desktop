using PCareer.Client.Models;

namespace PCareer.Client.Services;

public sealed class MissionExecutor
{
    private static readonly TimeSpan TelemetryFreshness = TimeSpan.FromSeconds(8);
    private const int MaximumFollowUpSteps = 10;

    private readonly IMissionClient _missions;
    private readonly ISimulatorConnection _simulator;
    private readonly GroundObjectSpawner _groundObjects;
    private readonly Func<TelemetrySnapshot?> _latestTelemetry;
    private readonly HashSet<string> _handledEntryActions = new(StringComparer.Ordinal);
    private readonly SemaphoreSlim _operationLock = new(1, 1);
    private MissionState? _mission;
    private bool _sceneInitialized;

    public MissionExecutor(
        IMissionClient missions,
        ISimulatorConnection simulator,
        GroundObjectSpawner groundObjects,
        Func<TelemetrySnapshot?> latestTelemetry)
    {
        _missions = missions;
        _simulator = simulator;
        _groundObjects = groundObjects;
        _latestTelemetry = latestTelemetry;
    }

    public event EventHandler<MissionSnapshot>? MissionChanged;

    public MissionState? CurrentMission => _mission;

    public async Task PollAsync()
    {
        if (!await _operationLock.WaitAsync(0))
        {
            return;
        }

        try
        {
            var mission = await _missions.GetActiveMissionAsync();
            if (mission is null)
            {
                await HandleMissionEndedAsync();
                return;
            }

            if (_mission?.StateId != mission.StateId)
            {
                await _groundObjects.ReleaseAllAsync();
                _handledEntryActions.Clear();
                _sceneInitialized = mission.Script.MissionSequence.FirstOrDefault()?.Id
                    != mission.CurrentPhaseId;
            }
            _mission = mission;
            if (!_sceneInitialized)
            {
                Publish(SnapshotFor(
                    mission,
                    $"Select {mission.Script.AircraftIcao} at {mission.Script.PickupAirport.Icao}, then click Start flight to initialize the pickup scene."));
                return;
            }
            await RunMissionLoopAsync();
        }
        catch (Exception exception)
        {
            Publish(SnapshotFor(_mission, $"Mission controller error: {exception.Message}"));
        }
        finally
        {
            _operationLock.Release();
        }
    }

    public async Task InitializeForFlightAsync()
    {
        await _operationLock.WaitAsync();
        try
        {
            var mission = _mission ?? await _missions.GetActiveMissionAsync()
                ?? throw new InvalidOperationException("No active mission is available.");
            if (_sceneInitialized)
            {
                return;
            }
            if (MissionPhaseDriver.NextPhase(mission) is not { Teleport: { } position } scenePhase)
            {
                throw new InvalidOperationException("The mission has no pickup scene to initialize.");
            }

            Publish(SnapshotFor(mission, "Repositioning the aircraft and initializing the pickup scene…"));
            _simulator.SetUserAircraftPosition(position);
            if (scenePhase.Camera is { } camera)
            {
                _simulator.SetMissionCamera(camera);
            }
            mission = await _missions.AdvanceMissionAsync(
                mission.ContractId ?? throw new InvalidOperationException("The mission has no contract."),
                scenePhase.Id);
            _mission = mission;
            _sceneInitialized = true;
            await HandlePhaseEntryAsync(mission, scenePhase);
        }
        finally
        {
            _operationLock.Release();
        }
    }

    private async Task HandleMissionEndedAsync()
    {
        if (_mission is not { } ended)
        {
            return;
        }

        await _groundObjects.ReleaseAllAsync();
        ResetMissionState();
        Publish(MissionSnapshots.Ended(ended.Script.Name));
    }

    private async Task RunMissionLoopAsync()
    {
        var mission = _mission;
        if (mission is null)
        {
            return;
        }

        for (var step = 0; step < MaximumFollowUpSteps; step++)
        {
            if (mission.Status != "active")
            {
                Publish(SnapshotFor(mission, $"Mission status: {mission.Status}."));
                return;
            }
            if (mission.CurrentPhase is not { } currentPhase)
            {
                Publish(SnapshotFor(mission, "The mission has no current phase."));
                return;
            }

            if (!await HandlePhaseEntryAsync(mission, currentPhase))
            {
                return;
            }

            if (MissionPhaseDriver.NextPhase(mission) is not { } nextPhase)
            {
                Publish(SnapshotFor(mission, $"Waiting at “{currentPhase.Label}”."));
                return;
            }

            var updated = await TryAdvanceThroughAsync(mission, currentPhase, nextPhase);
            if (updated is null)
            {
                return;
            }

            mission = updated;
            _mission = mission;
        }
    }

    private async Task<bool> HandlePhaseEntryAsync(MissionState mission, MissionPhase phase)
    {
        if (MissionPhaseDriver.IsAction(phase, MissionPhaseDriver.ActionCompleteMission))
        {
            return await WaitForFlightSettlementAsync(mission);
        }

        if (MissionPhaseDriver.IsAction(phase, MissionPhaseDriver.ActionSpawnObjects))
        {
            return await SpawnForPhaseAsync(mission, phase);
        }

        if (MissionPhaseDriver.IsAction(phase, MissionPhaseDriver.ActionRemoveObjects))
        {
            await RemoveForPhaseAsync(mission, phase);
            return true;
        }

        if (phase.Action is not null && phase.Action != MissionPhaseDriver.ActionShowDialog)
        {
            Publish(SnapshotFor(mission, $"Unsupported mission action: {phase.Action}."));
            return false;
        }

        if (MissionPhaseDriver.IsAction(phase, MissionPhaseDriver.ActionShowDialog)
            && _handledEntryActions.Add($"dialog:{phase.Id}"))
        {
            Publish(SnapshotFor(mission, MissionPhaseDriver.DialogText(mission, phase) ?? $"“{phase.Label}”"));
        }

        return true;
    }

    private async Task<bool> SpawnForPhaseAsync(MissionState mission, MissionPhase phase)
    {
        var specs = MissionPhaseDriver.ObjectsForPhase(mission, phase);
        foreach (var spec in specs)
        {
            if (await _groundObjects.TrySpawnAsync(spec))
            {
                continue;
            }

            Publish(SnapshotFor(
                mission,
                _groundObjects.IsEnabled
                    ? $"Waiting to spawn simulated object “{spec.Title}”."
                    : "Object spawning is unavailable in this build."));
            return false;
        }

        _handledEntryActions.Add($"spawn:{phase.Id}");
        Publish(SnapshotFor(mission, $"Stage set for “{phase.Label}”."));
        return true;
    }

    private async Task RemoveForPhaseAsync(MissionState mission, MissionPhase phase)
    {
        await _groundObjects.RemoveAsync(phase.ObjectIds);
        _handledEntryActions.Add($"remove:{phase.Id}");
        Publish(SnapshotFor(mission, $"Stage cleared at “{phase.Label}”."));
    }

    private async Task<bool> WaitForFlightSettlementAsync(MissionState mission)
    {
        await _groundObjects.ReleaseAllAsync();
        Publish(SnapshotFor(mission, "Objectives complete. Finish the flight to receive your reward."));
        return false;
    }

    private async Task<MissionState?> TryAdvanceThroughAsync(
        MissionState mission,
        MissionPhase currentPhase,
        MissionPhase nextPhase)
    {
        if (currentPhase.DurationSec is { } duration)
        {
            var elapsed = MissionPhaseDriver.PhaseElapsedSeconds(mission, DateTimeOffset.UtcNow);
            if (elapsed is not { } elapsedSeconds)
            {
                Publish(SnapshotFor(mission, $"Measuring the “{currentPhase.Label}” timer."));
                return null;
            }
            if (elapsedSeconds < duration)
            {
                var remaining = Math.Ceiling(duration - elapsedSeconds);
                Publish(SnapshotFor(mission, $"“{currentPhase.Label}”: {remaining:0} s remaining."));
                return null;
            }

        }

        if (currentPhase.Trigger is not null)
        {
            return await EvaluateTriggerAsync(mission);
        }

        return await AdvanceToAsync(mission, nextPhase);
    }

    private async Task<MissionState?> EvaluateTriggerAsync(
        MissionState mission)
    {
        if (mission.ContractId is not { } contractId)
        {
            return null;
        }

        var telemetry = _latestTelemetry();
        if (telemetry is null
            || DateTimeOffset.UtcNow - telemetry.ObservedAt > TelemetryFreshness
            || telemetry.SlewActive
            || telemetry.ObservedAt > DateTimeOffset.UtcNow + TimeSpan.FromSeconds(2)
            || telemetry.SimulationRate < 0.95d
            || telemetry.SimulationRate > 1.05d)
        {
            Publish(SnapshotFor(mission, "Waiting for fresh simulator telemetry."));
            return null;
        }

        try
        {
            var updated = await _missions.EvaluateMissionTriggerAsync(contractId, telemetry);
            if (string.Equals(
                updated.CurrentPhaseId,
                mission.CurrentPhaseId,
                StringComparison.Ordinal))
            {
                Publish(SnapshotFor(mission, $"Waiting for the “{mission.CurrentPhase?.Label}” trigger."));
                return null;
            }

            Publish(SnapshotFor(updated, $"Advanced to “{updated.CurrentPhase?.Label}”."));
            return updated;
        }
        catch (Exception exception)
        {
            Publish(SnapshotFor(mission, $"Trigger check failed: {exception.Message}"));
            return null;
        }
    }

    private async Task<MissionState?> AdvanceToAsync(MissionState mission, MissionPhase nextPhase)
    {
        if (mission.ContractId is not { } contractId)
        {
            return null;
        }

        try
        {
            var updated = await _missions.AdvanceMissionAsync(contractId, nextPhase.Id);
            Publish(SnapshotFor(updated, $"Advanced to “{updated.CurrentPhase?.Label}”."));
            return updated;
        }
        catch (Exception exception)
        {
            Publish(SnapshotFor(mission, $"Phase advance failed: {exception.Message}"));
            return null;
        }
    }

    private MissionSnapshot SnapshotFor(MissionState? mission, string statusText)
    {
        if (mission is null)
        {
            return MissionSnapshots.Idle;
        }

        return new MissionSnapshot(
            true,
            mission.Script.Name,
            mission.CurrentPhase?.Label ?? mission.CurrentPhaseId,
            MissionPhaseDriver.DialogText(mission),
            statusText,
            null);
    }

    private void ResetMissionState()
    {
        _mission = null;
        _sceneInitialized = false;
        _handledEntryActions.Clear();
    }

    private void Publish(MissionSnapshot snapshot) =>
        MissionChanged?.Invoke(this, snapshot);
}
