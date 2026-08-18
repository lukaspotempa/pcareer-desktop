// Compile-time contract only. This assembly is never copied into a release build.
#pragma warning disable CS0067
namespace Microsoft.FlightSimulator.SimConnect;

public enum SIMCONNECT_DATATYPE { FLOAT64, INT32, STRING256 }
public enum SIMCONNECT_PERIOD { SIM_FRAME }
public enum SIMCONNECT_DATA_REQUEST_FLAG { DEFAULT }

public class SIMCONNECT_RECV { }
public sealed class SIMCONNECT_RECV_OPEN : SIMCONNECT_RECV { }
public sealed class SIMCONNECT_RECV_EXCEPTION : SIMCONNECT_RECV
{
    public uint dwException;
    public uint dwSendID;
}
public sealed class SIMCONNECT_RECV_SIMOBJECT_DATA : SIMCONNECT_RECV
{
    public uint dwRequestID;
    public object[] dwData = [];
}

public sealed class SimConnect : IDisposable
{
    public const uint SIMCONNECT_UNUSED = uint.MaxValue;
    public const uint SIMCONNECT_OBJECT_ID_USER = 0;

    public SimConnect(string name, IntPtr windowHandle, uint messageId, object? waitHandle, uint configIndex) { }

    public event Action<SimConnect, SIMCONNECT_RECV_OPEN>? OnRecvOpen;
    public event Action<SimConnect, SIMCONNECT_RECV>? OnRecvQuit;
    public event Action<SimConnect, SIMCONNECT_RECV_EXCEPTION>? OnRecvException;
    public event Action<SimConnect, SIMCONNECT_RECV_SIMOBJECT_DATA>? OnRecvSimobjectData;

    public void AddToDataDefinition(Enum definitionId, string name, string? units, SIMCONNECT_DATATYPE dataType, float epsilon, uint datumId) { }
    public void RegisterDataDefineStruct<T>(Enum definitionId) where T : struct { }
    public void RequestDataOnSimObject(Enum requestId, Enum definitionId, uint objectId, SIMCONNECT_PERIOD period, SIMCONNECT_DATA_REQUEST_FLAG flags, uint origin, uint interval, uint limit) { }
    public void ReceiveMessage() { }
    public void Dispose() { }
}
