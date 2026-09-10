using PCareer.Client.Models;

namespace PCareer.Client.Services;

public interface ISimulatorConnection : IDisposable
{
    bool IsConnected { get; }

    bool SupportsObjectSpawning { get; }

    string StatusMessage { get; }

    event EventHandler? ConnectionChanged;

    event EventHandler<TelemetrySnapshot>? TelemetryReceived;

    event EventHandler<AircraftSnapshot>? AircraftIdentityReceived;

    void TryConnect(IntPtr windowHandle, int messageId);

    void ReceiveMessage();

    void RequestAircraftIdentity();

    Task<uint> SpawnGroundObjectAsync(
        string containerTitle,
        double latitudeDegrees,
        double longitudeDegrees,
        double altitudeFeet,
        double headingDegrees);

    Task RemoveGroundObjectAsync(uint objectId);

    void FreezeGroundObject(uint objectId);

    void SetUserAircraftPosition(MissionTeleport position);

    void SetMissionCamera(MissionCamera camera);

    void SetPayloadKilograms(double payloadKilograms);

    void SetFuelKilograms(double fuelKilograms);
}
