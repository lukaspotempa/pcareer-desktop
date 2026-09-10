using System.Text.Json;

namespace PCareer.Client.Models;

public sealed record MissionState(
    int StateId,
    int ScriptId,
    MissionScriptData Script,
    string Status,
    string CurrentPhaseId,
    IReadOnlyDictionary<string, JsonElement>? PhaseData,
    IReadOnlyList<MissionGroundObject> SpawnedObjects,
    DateTimeOffset StartedAt,
    DateTimeOffset? CompletedAt,
    string? ContractId)
{
    public MissionPhase? CurrentPhase =>
        Script.MissionSequence.FirstOrDefault(phase => phase.Id == CurrentPhaseId);
}

public sealed record MissionScriptData(
    int Id,
    string Code,
    string Name,
    string Description,
    string AircraftIcao,
    MissionAirfield PickupAirport,
    MissionAirfield DeliveryAirport,
    int EstimatedDurationMin,
    IReadOnlyList<MissionGroundObject> GroundObjects,
    IReadOnlyList<MissionPhase> MissionSequence,
    IReadOnlyList<MissionDialogLine> DialogLines,
    string? WeatherPreset,
    int BaseRewardCents,
    int CancellationFeeCents,
    bool IsActive);

public sealed record MissionAirfield(int Id, string Icao, string Name, double Latitude, double Longitude);

public sealed record MissionGroundObject(
    string Id,
    string ObjectType,
    string Title,
    MissionPosition Position,
    double HeadingDegrees,
    bool Freeze);

public sealed record MissionPosition(double Lat, double Lon, double AltM);

public sealed record MissionPhase(
    string Id,
    string Label,
    string? Action,
    IReadOnlyList<MissionWaypoint> Waypoints,
    MissionTrigger? Trigger,
    double? DurationSec,
    string? DialogId,
    MissionCamera? Camera,
    MissionTeleport? Teleport,
    IReadOnlyList<string> ObjectIds);

public sealed record MissionWaypoint(double Lat, double Lon);

public sealed record MissionTrigger(
    string Type,
    double? Lat,
    double? Lon,
    double? RadiusM,
    double? MinAltitudeFt,
    bool? OnGround,
    double? ElapsedSec);

public sealed record MissionCamera(
    string? TargetObjectId,
    double OffsetMX,
    double OffsetMY,
    double OffsetMZ,
    double? FovDegrees,
    double TransitionSec);

public sealed record MissionTeleport(
    double Lat,
    double Lon,
    double AltitudeFt,
    double HeadingDegrees,
    bool OnGround);

public sealed record MissionDialogLine(string Id, string Text, string? Speaker, string? AudioHint);

public sealed record MissionCompleteResult(int MissionStateId, string Status, int RewardCents, string Message);

public sealed record SpawnedGroundObject(string ObjectId, string Title, uint SimObjectId);

public sealed record MissionSnapshot(
    bool IsActive,
    string? MissionName,
    string? PhaseLabel,
    string? DialogText,
    string? StatusText,
    string? Outcome);

public static class MissionSnapshots
{
    public static MissionSnapshot Idle { get; } =
        new(false, null, null, null, "No active MedEvac mission.", null);

    public static MissionSnapshot Completed(string? missionName) =>
        new(false, missionName, null, null, "MedEvac mission completed.", "completed");

    public static MissionSnapshot Cancelled(string? missionName) =>
        new(false, missionName, null, null, "MedEvac mission was cancelled.", "cancelled");

    public static MissionSnapshot Ended(string? missionName) =>
        new(false, missionName, null, null, "MedEvac mission ended.", null);
}
