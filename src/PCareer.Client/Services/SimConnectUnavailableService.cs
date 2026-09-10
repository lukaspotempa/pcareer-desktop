using PCareer.Client.Models;

namespace PCareer.Client.Services;

internal sealed class SimConnectUnavailableService : ISimulatorConnection
{
    public bool IsConnected => false;

    public bool SupportsObjectSpawning => false;

    public string StatusMessage =>
        "SimConnect support was not included in this build. Install the MSFS 2024 SDK and rebuild.";

    public event EventHandler? ConnectionChanged;

    public event EventHandler<TelemetrySnapshot>? TelemetryReceived
    {
        add { }
        remove { }
    }

    public event EventHandler<AircraftSnapshot>? AircraftIdentityReceived
    {
        add { }
        remove { }
    }

    public void TryConnect(IntPtr windowHandle, int messageId) =>
        ConnectionChanged?.Invoke(this, EventArgs.Empty);

    public void ReceiveMessage()
    {
    }

    public void RequestAircraftIdentity() =>
        throw new InvalidOperationException(
            "SimConnect support was not included in this build. Install the MSFS 2024 SDK and rebuild.");

    public Task<uint> SpawnGroundObjectAsync(
        string containerTitle,
        double latitudeDegrees,
        double longitudeDegrees,
        double altitudeFeet,
        double headingDegrees) =>
        throw new InvalidOperationException(
            "SimConnect support was not included in this build. Install the MSFS 2024 SDK and rebuild.");

    public Task RemoveGroundObjectAsync(uint objectId) =>
        throw new InvalidOperationException(
            "SimConnect support was not included in this build. Install the MSFS 2024 SDK and rebuild.");

    public void FreezeGroundObject(uint objectId) =>
        throw new InvalidOperationException(
            "SimConnect support was not included in this build. Install the MSFS 2024 SDK and rebuild.");

    public void SetUserAircraftPosition(MissionTeleport position) =>
        throw new InvalidOperationException(
            "SimConnect support was not included in this build. Install the MSFS 2024 SDK and rebuild.");

    public void SetMissionCamera(MissionCamera camera) =>
        throw new InvalidOperationException(
            "SimConnect support was not included in this build. Install the MSFS 2024 SDK and rebuild.");

    public void SetPayloadKilograms(double payloadKilograms) =>
        throw new InvalidOperationException(
            "SimConnect support was not included in this build. Install the MSFS 2024 SDK and rebuild.");

    public void SetFuelKilograms(double fuelKilograms) =>
        throw new InvalidOperationException(
            "SimConnect support was not included in this build. Install the MSFS 2024 SDK and rebuild.");

    public void Dispose()
    {
        GC.SuppressFinalize(this);
    }
}
