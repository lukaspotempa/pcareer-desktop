using PCareer.Client.Models;

namespace PCareer.Client.Services;

public sealed class MissionExecutor
{
    private static readonly TimeSpan TelemetryFreshness = TimeSpan.FromSeconds(8);
    private const int MaximumFollowUpSteps = 10;
    private const double AirportRangeNauticalMiles = 5;

    private readonly IMissionClient _missions;
    private readonly ISimulatorConnection _simulator;
    private readonly GroundObjectSpawner _groundObjects;
    private readonly Func<TelemetrySnapshot?> _latestTelemetry;
    private readonly HashSet<string> _handledEntryActions = new(StringComparer.Ordinal);
    private readonly SemaphoreSlim _operationLock = new(1, 1);
    private MissionState? _mission;
    private bool _flightInitialized;

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
    public event EventHandler? FlightSettlementRequested;

    public MissionState? CurrentMission => _mission;

    public bool CanReloadScene => _mission is not null && _groundObjects.Spawned.Count > 0;

    public bool CanAdvanceToNextPhase => _mission?.CurrentPhase?.DebugTeleport is not null;

    public async Task<bool> RestartAsync()
    {
        await _operationLock.WaitAsync();
        try
        {
            var mission = _mission ?? await _missions.GetActiveMissionAsync();
            if (mission?.ContractId is not { } contractId)
            {
                return false;
            }

            await _groundObjects.ReleaseAllAsync();
            _simulator.ReleaseMissionCamera();
            _mission = await _missions.RestartMissionAsync(contractId);
            _flightInitialized = false;
            _handledEntryActions.Clear();
            Publish(SnapshotFor(_mission, $"Mission restarted at {_mission.Script.PickupAirport.Icao}."));
            return true;
        }
        finally
        {
            _operationLock.Release();
        }
    }

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
                _simulator.ReleaseMissionCamera();
                _handledEntryActions.Clear();
                _flightInitialized = mission.CurrentPhaseId != mission.Script.MissionSequence.FirstOrDefault()?.Id;
                foreach (var groundObject in ResolveCurrentObjectDefinitions(
                    mission,
                    mission.SpawnedObjects.Select(item => item.Id)))
                {
                    if (!await _groundObjects.TrySpawnAsync(groundObject))
                    {
                        Publish(SnapshotFor(
                            mission,
                            _groundObjects.LastSpawnError ?? "Could not restore the mission scene."));
                        return;
                    }
                }
            }
            _mission = mission;
            if (!_flightInitialized)
            {
                var firstPhaseId = mission.Script.MissionSequence.FirstOrDefault()?.Id;
                if (string.Equals(mission.CurrentPhaseId, firstPhaseId, StringComparison.Ordinal))
                {
                    Publish(SnapshotFor(mission, $"Start the flight at {mission.Script.PickupAirport.Icao}."));
                    return;
                }
                _flightInitialized = true;
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
            if (mission.CurrentPhase is not { } currentPhase
                || MissionPhaseDriver.NextPhase(mission) is not { } nextPhase)
            {
                throw new InvalidOperationException("The mission cannot be started from its current phase.");
            }
            if (currentPhase.Trigger is not null || currentPhase.DurationSec is not null)
            {
                _flightInitialized = true;
                Publish(SnapshotFor(mission, MissionPhaseDriver.DialogText(mission, currentPhase)
                    ?? $"Flight started. {currentPhase.Label}."));
                await RunMissionLoopAsync();
                return;
            }
            _mission = await _missions.AdvanceMissionAsync(
                mission.ContractId ?? throw new InvalidOperationException("The mission has no contract."),
                nextPhase.Id);
            _flightInitialized = true;
            Publish(SnapshotFor(_mission, $"Flight started. {nextPhase.Label}."));
        }
        finally
        {
            _operationLock.Release();
        }
    }

    public async Task ReloadSceneAsync()
    {
        await _operationLock.WaitAsync();
        try
        {
            var mission = await _missions.GetActiveMissionAsync()
                ?? throw new InvalidOperationException("No active mission is available.");
            _mission = mission;
            var sceneObjects = ResolveCurrentObjectDefinitions(
                mission,
                _groundObjects.Spawned.Select(item => item.ObjectId));
            if (sceneObjects.Count == 0)
            {
                throw new InvalidOperationException("The mission has no reloadable simulator scene.");
            }

            Publish(SnapshotFor(mission, "Reloading the simulator scene…"));
            await _groundObjects.ReleaseAllAsync();
            if (!await SpawnObjectsAsync(mission, sceneObjects))
            {
                throw new InvalidOperationException(
                    _groundObjects.LastSpawnError ?? "The simulator scene could not be reloaded.");
            }
        }
        finally
        {
            _operationLock.Release();
        }
    }

    public async Task AdvanceToNextPhaseAsync()
    {
        await _operationLock.WaitAsync();
        try
        {
            var mission = _mission
                ?? throw new InvalidOperationException("No active mission is available.");
            var phase = mission.CurrentPhase
                ?? throw new InvalidOperationException("The mission has no current phase.");
            var target = phase.DebugTeleport
                ?? throw new InvalidOperationException("This phase has no debug teleport target.");
            var contractId = mission.ContractId
                ?? throw new InvalidOperationException("The mission has no contract.");

            _flightInitialized = true;

            Publish(SnapshotFor(mission, "Debug teleporting to the next mission scene…"));
            await _simulator.SetUserAircraftPositionAsync(target);

            var targetTelemetry = (_latestTelemetry()
                ?? throw new InvalidOperationException("Fresh simulator telemetry is required.")) with
            {
                ObservedAt = DateTimeOffset.UtcNow,
                LatitudeDegrees = target.Lat,
                LongitudeDegrees = target.Lon,
                AltitudeFeet = target.AltitudeFt,
                OnGround = target.OnGround,
                SlewActive = false,
                SimulationRate = 1,
            };
            var triggerTelemetry = targetTelemetry with
            {
                LatitudeDegrees = phase.Trigger?.Lat ?? targetTelemetry.LatitudeDegrees,
                LongitudeDegrees = phase.Trigger?.Lon ?? targetTelemetry.LongitudeDegrees,
                OnGround = phase.Trigger?.OnGround ?? targetTelemetry.OnGround,
            };
            var updated = await _missions.EvaluateMissionTriggerAsync(contractId, triggerTelemetry);
            if (updated.CurrentPhaseId == mission.CurrentPhaseId)
            {
                throw new InvalidOperationException("The debug target did not satisfy the phase trigger.");
            }
            await HandleCompletedPhaseAsync(mission, phase);
            _mission = updated;
            await RunMissionLoopAsync(targetTelemetry);
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
        _simulator.ReleaseMissionCamera();
        ResetMissionState();
        Publish(MissionSnapshots.Ended(ended.Script.Name));
    }

    private async Task RunMissionLoopAsync(TelemetrySnapshot? telemetryOverride = null)
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

            var updated = await TryAdvanceThroughAsync(
                mission,
                currentPhase,
                nextPhase,
                telemetryOverride);
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
            if (phase.Trigger is not null)
            {
                return true;
            }
            await RemoveForPhaseAsync(mission, phase);
            _simulator.ReleaseMissionCamera();
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
        if (_handledEntryActions.Contains($"spawn:{phase.Id}"))
        {
            return true;
        }
        var specs = MissionPhaseDriver.ObjectsForPhase(mission, phase);
        if (!await SpawnObjectsAsync(mission, specs))
        {
            return false;
        }

        _handledEntryActions.Add($"spawn:{phase.Id}");
        Publish(SnapshotFor(mission, $"Stage set for “{phase.Label}”."));
        return true;
    }

    private async Task<bool> SpawnObjectsAsync(
        MissionState mission,
        IReadOnlyList<MissionGroundObject> specs)
    {
        foreach (var spec in specs)
        {
            if (await _groundObjects.TrySpawnAsync(spec))
            {
                continue;
            }

            Publish(SnapshotFor(
                        mission,
                        _groundObjects.IsEnabled
                            ? _groundObjects.LastSpawnError ?? $"Could not spawn simulated object “{spec.Title}”."
                            : "Object spawning is unavailable in this build."));
            return false;
        }

        return true;
    }

    private static IReadOnlyList<MissionGroundObject> ResolveCurrentObjectDefinitions(
        MissionState mission,
        IEnumerable<string> objectIds)
    {
        var definitions = mission.Script.GroundObjects.ToDictionary(item => item.Id, StringComparer.Ordinal);
        var savedObjects = mission.SpawnedObjects.ToDictionary(item => item.Id, StringComparer.Ordinal);
        return objectIds
            .Distinct(StringComparer.Ordinal)
            .Select(objectId => definitions.GetValueOrDefault(objectId)
                ?? savedObjects.GetValueOrDefault(objectId)
                ?? throw new InvalidOperationException(
                    $"The active mission object “{objectId}” has no script definition."))
            .ToArray();
    }

    private async Task RemoveForPhaseAsync(MissionState mission, MissionPhase phase)
    {
        await _groundObjects.RemoveAsync(phase.ObjectIds);
        _handledEntryActions.Add($"remove:{phase.Id}");
        Publish(SnapshotFor(mission, $"Stage cleared at “{phase.Label}”."));
    }

    private Task<bool> WaitForFlightSettlementAsync(MissionState mission)
    {
        _simulator.ReleaseMissionCamera();
        Publish(SnapshotFor(mission, "Patient handover complete. The flight is ending automatically."));
        if (_handledEntryActions.Add($"settle:{mission.CurrentPhaseId}"))
        {
            FlightSettlementRequested?.Invoke(this, EventArgs.Empty);
        }
        return Task.FromResult(false);
    }

    private async Task<MissionState?> TryAdvanceThroughAsync(
        MissionState mission,
        MissionPhase currentPhase,
        MissionPhase nextPhase,
        TelemetrySnapshot? telemetryOverride = null)
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
                Publish(SnapshotFor(
                    mission,
                    $"{currentPhase.Label}: {remaining:0} s remaining.",
                    $"Wait for the patient to be loaded {remaining:0} seconds."));
                return null;
            }

        }

        if (currentPhase.Trigger is not null)
        {
            return await EvaluateTriggerAsync(mission, telemetryOverride);
        }

        return await AdvanceToAsync(mission, nextPhase);
    }

    private async Task<MissionState?> EvaluateTriggerAsync(
        MissionState mission,
        TelemetrySnapshot? telemetryOverride = null)
    {
        if (mission.ContractId is not { } contractId)
        {
            return null;
        }

        var telemetry = telemetryOverride ?? _latestTelemetry();
        if (telemetry is null
            || DateTimeOffset.UtcNow - telemetry.ObservedAt > TelemetryFreshness
            || telemetry.SlewActive
            || telemetry.ObservedAt > DateTimeOffset.UtcNow + TimeSpan.FromSeconds(2)
            || telemetry.SimulationRate < 0.95d)
        {
            Publish(SnapshotFor(mission, "Waiting for fresh simulator telemetry."));
            return null;
        }

        try
        {
            if (!IsWithinAirportRange(mission, telemetry))
            {
                Publish(SnapshotFor(mission, "Waiting for the aircraft to reach the airport."));
                return null;
            }
            var updated = await _missions.EvaluateMissionTriggerAsync(contractId, telemetry);
            if (string.Equals(
                updated.CurrentPhaseId,
                mission.CurrentPhaseId,
                StringComparison.Ordinal))
            {
                Publish(SnapshotFor(mission, $"Waiting for the “{mission.CurrentPhase?.Label}” trigger."));
                return null;
            }

            await HandleCompletedPhaseAsync(mission, mission.CurrentPhase);

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

    private async Task HandleCompletedPhaseAsync(MissionState mission, MissionPhase? completedPhase)
    {
        if (completedPhase is null
            || !MissionPhaseDriver.IsAction(
                completedPhase,
                MissionPhaseDriver.ActionRemoveObjects))
        {
            return;
        }
        await RemoveForPhaseAsync(mission, completedPhase);
        _simulator.ReleaseMissionCamera();
    }

    private MissionSnapshot SnapshotFor(
        MissionState? mission,
        string statusText,
        string? dialogText = null)
    {
        if (mission is null)
        {
            return MissionSnapshots.Idle;
        }

        return new MissionSnapshot(
            true,
            mission.Script.Name,
            mission.CurrentPhase?.Label ?? mission.CurrentPhaseId,
            dialogText ?? MissionPhaseDriver.DialogText(mission),
            statusText,
            null);
    }

    private void ResetMissionState()
    {
        _mission = null;
        _flightInitialized = false;
        _handledEntryActions.Clear();
    }

    private void Publish(MissionSnapshot snapshot) =>
        MissionChanged?.Invoke(this, snapshot);

    private static bool IsWithinAirportRange(MissionState mission, TelemetrySnapshot telemetry)
    {
        if (mission.CurrentPhase is not { } phase)
        {
            return true;
        }

        var site = SiteForCurrentLeg(mission, phase);
        if (site is null)
        {
            return true;
        }

        var distance = DistanceHelper.DistanceNauticalMiles(
            telemetry.LatitudeDegrees,
            telemetry.LongitudeDegrees,
            site.Value.Lat,
            site.Value.Lon);
        return distance <= AirportRangeNauticalMiles;
    }

    private static (double Lat, double Lon)? SiteForCurrentLeg(MissionState mission, MissionPhase phase)
    {
        // Phases that approach a target (return, en-route and handover legs) carry the
        // relevant coordinates in their own trigger.
        if (phase.Trigger?.Lat is double lat && phase.Trigger?.Lon is double lon)
        {
            return (lat, lon);
        }

        // The departure leg has no coordinates of its own; it advances from wherever
        // the previous leg staged the aircraft (the pickup site).
        return mission.Script.MissionSequence
            .TakeWhile(item => item.Id != phase.Id)
            .LastOrDefault(item => item.Trigger is { Lat: not null, Lon: not null }) is { } staged
            && staged.Trigger?.Lat is double stagedLat
            && staged.Trigger?.Lon is double stagedLon
                ? (stagedLat, stagedLon)
                : null;
    }
}
