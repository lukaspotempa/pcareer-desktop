using PCareer.Client.Models;

namespace PCareer.Client.Services;

internal sealed class SimConnectUnavailableService : ISimulatorConnection
{
    public bool IsConnected => false;

    public string StatusMessage =>
        "SimConnect support was not included in this build. Install the MSFS 2024 SDK and rebuild.";

    public event EventHandler? ConnectionChanged;

    public event EventHandler<TelemetrySnapshot>? TelemetryReceived
    {
        add { }
        remove { }
    }

    public void TryConnect(IntPtr windowHandle, int messageId) =>
        ConnectionChanged?.Invoke(this, EventArgs.Empty);

    public void ReceiveMessage()
    {
    }

    public void Dispose()
    {
        GC.SuppressFinalize(this);
    }
}
