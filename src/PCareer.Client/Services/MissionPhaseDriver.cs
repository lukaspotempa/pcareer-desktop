using System.Text.Json;
using PCareer.Client.Models;

namespace PCareer.Client.Services;

public static class MissionPhaseDriver
{
    public const string ActionShowDialog = "show_dialog";
    public const string ActionSpawnObjects = "spawn_objects";
    public const string ActionRemoveObjects = "remove_objects";
    public const string ActionCompleteMission = "complete_mission";

    public static MissionPhase? FindPhase(MissionState mission, string phaseId) =>
        mission.Script.MissionSequence.FirstOrDefault(phase => phase.Id == phaseId);

    public static MissionPhase? NextPhase(MissionState mission) =>
        FindPhase(mission, mission.CurrentPhaseId) is { } current
            ? mission.Script.MissionSequence
                .SkipWhile(phase => phase.Id != current.Id)
                .Skip(1)
                .FirstOrDefault()
            : null;

    public static bool IsAction(MissionPhase phase, string action) =>
        string.Equals(phase.Action, action, StringComparison.Ordinal);

    public static string? DialogText(MissionState mission, MissionPhase? phase = null)
    {
        phase ??= mission.CurrentPhase;
        if (phase?.DialogId is not { } dialogId)
        {
            return null;
        }
        return mission.Script.DialogLines
            .FirstOrDefault(line => line.Id == dialogId)
            ?.Text;
    }

    public static double? PhaseElapsedSeconds(MissionState mission, DateTimeOffset nowUtc)
    {
        if (mission.PhaseData is not { } phaseData
            || !phaseData.TryGetValue("phase_started_at", out var phaseStarted))
        {
            return null;
        }

        try
        {
            var elapsed = (nowUtc - phaseStarted.GetDateTimeOffset()).TotalSeconds;
            return elapsed > 0 ? elapsed : 0;
        }
        catch (Exception exception) when (exception is FormatException or InvalidOperationException)
        {
            return null;
        }
    }

    public static IReadOnlyList<MissionGroundObject> ObjectsForPhase(
        MissionState mission,
        MissionPhase phase) =>
        phase.ObjectIds
            .Select(objectId => mission.Script.GroundObjects.FirstOrDefault(spec => spec.Id == objectId))
            .Where(spec => spec is not null)
            .Cast<MissionGroundObject>()
            .ToArray();
}