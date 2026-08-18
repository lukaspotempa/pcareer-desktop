using PCareer.Client.Models;

namespace PCareer.Client.Services;

public interface ISimulatorConnection : IDisposable
{
    bool IsConnected { get; }

    string StatusMessage { get; }

    event EventHandler? ConnectionChanged;

    event EventHandler<TelemetrySnapshot>? TelemetryReceived;

    void TryConnect(IntPtr windowHandle, int messageId);

    void ReceiveMessage();
}

