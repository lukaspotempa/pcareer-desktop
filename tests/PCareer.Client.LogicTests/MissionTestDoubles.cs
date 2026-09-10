using System.Text.Json;
using PCareer.Client.Models;
using PCareer.Client.Services;

namespace PCareer.Client.LogicTests;

internal sealed class FakeSimulatorConnection : ISimulatorConnection
{
    public FakeSimulatorConnection(bool supportsObjectSpawning = true) =>
        SupportsObjectSpawning = supportsObjectSpawning;

    public bool IsConnected { get; } = true;

    public bool SupportsObjectSpawning { get; }

    public string StatusMessage { get; } = "Fake simulator";

    public List<string> SpawnCalls { get; } = [];

    public List<string> RemoveCalls { get; } = [];

    public List<string> FreezeCalls { get; } = [];

    public List<MissionTeleport> PositionCalls { get; } = [];

    public List<MissionCamera> CameraCalls { get; } = [];

    private uint _nextObjectId = 1;

    public event EventHandler? ConnectionChanged { add { } remove { } }

    public event EventHandler<TelemetrySnapshot>? TelemetryReceived { add { } remove { } }

    public event EventHandler<AircraftSnapshot>? AircraftIdentityReceived { add { } remove { } }

    public void TryConnect(IntPtr windowHandle, int messageId)
    {
    }

    public void ReceiveMessage()
    {
    }

    public void RequestAircraftIdentity()
    {
    }

    public Task<uint> SpawnGroundObjectAsync(
        string containerTitle,
        double latitudeDegrees,
        double longitudeDegrees,
        double altitudeFeet,
        double headingDegrees)
    {
        SpawnCalls.Add(containerTitle);
        return Task.FromResult(_nextObjectId++);
    }

    public Task RemoveGroundObjectAsync(uint objectId)
    {
        RemoveCalls.Add(objectId.ToString());
        return Task.CompletedTask;
    }

    public void FreezeGroundObject(uint objectId) => FreezeCalls.Add(objectId.ToString());

    public void SetUserAircraftPosition(MissionTeleport position) => PositionCalls.Add(position);

    public void SetMissionCamera(MissionCamera camera) => CameraCalls.Add(camera);

    public void SetPayloadKilograms(double payloadKilograms)
    {
    }

    public void SetFuelKilograms(double fuelKilograms)
    {
    }

    public void Dispose()
    {
    }
}

internal sealed class FakeMissionClient : IMissionClient
{
    public MissionState? Active { get; set; }

    public int AdvanceCalls { get; private set; }

    public int EvaluateCalls { get; private set; }

    public int CompleteCalls { get; private set; }

    public Task<MissionState?> GetActiveMissionAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult(Active);

    public Task<MissionState> AdvanceMissionAsync(
        string contractId,
        string phaseId,
        CancellationToken cancellationToken = default)
    {
        AdvanceCalls++;
        var current = RequireActive();
        if (MissionPhaseDriver.NextPhase(current) is not { } next
            || next.Id != phaseId)
        {
            throw new InvalidOperationException($"Cannot advance to {phaseId}.");
        }
        var updated = Updated(next);
        Active = updated;
        return Task.FromResult(updated);
    }

    public Task<MissionState> EvaluateMissionTriggerAsync(
        string contractId,
        TelemetrySnapshot telemetry,
        CancellationToken cancellationToken = default)
    {
        EvaluateCalls++;
        var current = RequireActive();
        if (current.CurrentPhase?.Trigger is not { } trigger
            || MissionPhaseDriver.NextPhase(current) is not { } next)
        {
            return Task.FromResult(current);
        }
        if (TriggerFires(trigger, telemetry))
        {
            var updated = Updated(next);
            Active = updated;
            return Task.FromResult(updated);
        }
        return Task.FromResult(current);
    }

    public Task<MissionCompleteResult> CompleteMissionAsync(
        string contractId,
        string completionType,
        CancellationToken cancellationToken = default)
    {
        CompleteCalls++;
        var completed = RequireActive() with
        {
            Status = "completed",
            CompletedAt = DateTimeOffset.UtcNow,
        };
        Active = null;
        return Task.FromResult(new MissionCompleteResult(
            completed.StateId,
            completed.Status,
            12500,
            "Mission completed"));
    }

    private MissionState RequireActive() =>
        Active is { Status: "active" } current
            ? current
            : throw new InvalidOperationException("Mission is not active.");

    private MissionState Updated(MissionPhase to)
    {
        var current = RequireActive();
        var completedPhases = new List<string>();
        if (current.PhaseData is not null
            && current.PhaseData.TryGetValue("completed_phases", out var existing)
            && existing.ValueKind == JsonValueKind.Array)
        {
            completedPhases = existing
                .EnumerateArray()
                .Select(line => line.GetString() ?? string.Empty)
                .ToList();
        }
        completedPhases.Add(current.CurrentPhaseId);
        var phaseData = new Dictionary<string, JsonElement>
        {
            ["completed_phases"] = JsonSerializer.SerializeToElement(completedPhases),
            ["phase_started_at"] = JsonSerializer.SerializeToElement(
                to.DurationSec is not null
                    ? DateTimeOffset.UtcNow.AddSeconds(-40)
                    : DateTimeOffset.UtcNow),
        };
        return current with
        {
            CurrentPhaseId = to.Id,
            PhaseData = phaseData,
        };
    }

    private static bool TriggerFires(MissionTrigger trigger, TelemetrySnapshot telemetry)
    {
        var proximity = trigger.Type == "proximity";
        if (proximity
            && trigger.Lat is double latitude
            && trigger.Lon is double longitude
            && DistanceMeters(telemetry.LatitudeDegrees, telemetry.LongitudeDegrees, latitude, longitude)
                > (trigger.RadiusM ?? 4000))
        {
            return false;
        }
        if (trigger.MinAltitudeFt is double minimumAltitude
            && telemetry.AltitudeFeet < minimumAltitude)
        {
            return false;
        }
        if (trigger.OnGround is bool onGround && telemetry.OnGround != onGround)
        {
            return false;
        }
        return proximity
            || trigger.MinAltitudeFt is not null
            || trigger.OnGround is not null
            || trigger.ElapsedSec is not null;
    }

    private static double DistanceMeters(
        double latitudeDegrees1,
        double longitudeDegrees1,
        double latitudeDegrees2,
        double longitudeDegrees2)
    {
        const double earthRadiusMeters = 6_371_000d;
        var lat1 = latitudeDegrees1 * Math.PI / 180d;
        var lat2 = latitudeDegrees2 * Math.PI / 180d;
        var dLat = (latitudeDegrees2 - latitudeDegrees1) * Math.PI / 180d;
        var dLon = (longitudeDegrees2 - longitudeDegrees1) * Math.PI / 180d;
        var a = Math.Sin(dLat / 2d) * Math.Sin(dLat / 2d)
            + Math.Cos(lat1) * Math.Cos(lat2) * Math.Sin(dLon / 2d) * Math.Sin(dLon / 2d);
        return earthRadiusMeters * 2d * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
    }
}

internal static class MissionTestData
{
    public const double PickupLatitude = 48.3538;
    public const double PickupLongitude = 11.7861;
    public const double DeliveryLatitude = 52.1346;
    public const double DeliveryLongitude = 13.5044;

    public const string ContractId = "MEDEVAC-001";

    public static MissionState NewActiveMission(string currentPhaseId)
    {
        var now = DateTimeOffset.UtcNow;
        var script = new MissionScriptData(
            Id: 1,
            Code: "ALPINE_EMERGENCY",
            Name: "Alpine Emergency",
            Description: "Test mission",
            AircraftIcao: "TBM9",
            PickupAirport: new MissionAirfield(1, "EDDM", "Munich", PickupLatitude, PickupLongitude),
            DeliveryAirport: new MissionAirfield(2, "EDCG", "Rügen", DeliveryLatitude, DeliveryLongitude),
            EstimatedDurationMin: 45,
            GroundObjects:
            [
                new MissionGroundObject(
                    "obj_stretcher",
                    "ASO_Stretcher",
                    "Stretcher trolley",
                    new MissionPosition(PickupLatitude, PickupLongitude, 520),
                    0,
                    Freeze: true),
                new MissionGroundObject(
                    "obj_nurse",
                    "ASO_Nurse",
                    "Nurse figure",
                    new MissionPosition(PickupLatitude, PickupLongitude, 520),
                    90,
                    Freeze: true),
                new MissionGroundObject(
                    "obj_vehicle",
                    "ASO_Veh_MB_GWagen_01",
                    "Ambulance vehicle",
                    new MissionPosition(PickupLatitude, PickupLongitude, 520),
                    180,
                    Freeze: false),
            ],
            MissionSequence: Phases,
            DialogLines:
            [
                new MissionDialogLine("medevac_briefing", "Climb out and reach the pickup point.", "Dispatcher", null),
                new MissionDialogLine("loading_dialog", "The medics are loading the patient.", "Dispatcher", null),
            ],
            WeatherPreset: null,
            BaseRewardCents: 12500,
            CancellationFeeCents: 2500,
            IsActive: true);
        return new MissionState(
            StateId: 7,
            ScriptId: 1,
            Script: script,
            Status: "active",
            CurrentPhaseId: currentPhaseId,
            PhaseData: new Dictionary<string, JsonElement>
            {
                ["started_at"] = JsonSerializer.SerializeToElement(now),
            },
            SpawnedObjects: script.GroundObjects,
            StartedAt: now,
            CompletedAt: null,
            ContractId: ContractId);
    }

    private static IReadOnlyList<MissionPhase> Phases { get; } =
    [
        new MissionPhase(
            "briefing",
            "Briefing",
            "show_dialog",
            [],
            null,
            null,
            "medevac_briefing",
            null,
            null,
            []),
        new MissionPhase(
            "pickup_scene",
            "Patient loading",
            "spawn_objects",
            [],
            null,
            30,
            "loading_dialog",
            new MissionCamera(null, 6, 2, -12, 55, 2),
            new MissionTeleport(
                PickupLatitude,
                PickupLongitude,
                1487,
                120,
                true),
            ["obj_stretcher", "obj_nurse", "obj_vehicle"]),
        new MissionPhase(
            "departure",
            "Departure",
            "remove_objects",
            [],
            new MissionTrigger("altitude", null, null, null, 500, null, null),
            null,
            null,
            null,
            null,
            ["obj_stretcher", "obj_nurse", "obj_vehicle"]),
        new MissionPhase(
            "en_route",
            "En route",
            null,
            [],
            new MissionTrigger(
                "proximity",
                DeliveryLatitude,
                DeliveryLongitude,
                4000,
                null,
                null,
                null),
            null,
            null,
            null,
            null,
            []),
        new MissionPhase(
            "delivery",
            "Delivery",
            "complete_mission",
            [],
            null,
            null,
            null,
            null,
            null,
            []),
    ];
}
