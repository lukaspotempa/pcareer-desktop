using PCareer.Client.Models;

namespace PCareer.Client.Services;

public interface ISimulatorConnection : IDisposable
{
    bool IsConnected { get; }

    bool SupportsObjectSpawning { get; }

    string StatusMessage { get; }

    IReadOnlyList<string> MissionDiagnostics { get; }

    event EventHandler? ConnectionChanged;

    event EventHandler<TelemetrySnapshot>? TelemetryReceived;

    event EventHandler<AircraftSnapshot>? AircraftIdentityReceived;

    void TryConnect(IntPtr windowHandle, int messageId);

    void ReceiveMessage();

    void RequestAircraftIdentity();

    Task<string> ResolveGroundObjectTitleAsync(string objectType, string preferredTitle);

    Task<uint> SpawnGroundObjectAsync(
        string containerTitle,
        string? liveryName,
        double latitudeDegrees,
        double longitudeDegrees,
        double altitudeFeet,
        double headingDegrees);

    Task RemoveGroundObjectAsync(uint objectId);

    void FreezeGroundObject(uint objectId);

    Task SetUserAircraftPositionAsync(MissionTeleport position);

    void SetMissionCamera(MissionCamera camera);

    void ReleaseMissionCamera();

    void SetPayloadKilograms(double payloadKilograms);

    void SetFuelKilograms(double fuelKilograms);
}
