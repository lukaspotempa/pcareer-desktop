// Compile-time contract only. This assembly is never copied into a release build.
#pragma warning disable CS0067
namespace Microsoft.FlightSimulator.SimConnect;

public enum SIMCONNECT_DATATYPE { FLOAT64, INT32, STRING256, INITPOSITION }
public enum SIMCONNECT_PERIOD { SIM_FRAME, ONCE }
public enum SIMCONNECT_DATA_REQUEST_FLAG { DEFAULT }
public enum SIMCONNECT_DATA_SET_FLAG { DEFAULT }

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
public sealed class SIMCONNECT_RECV_ASSIGNED_OBJECT_ID : SIMCONNECT_RECV
{
    public uint dwRequestID;
    public uint dwObjectID;
}
public sealed class SIMCONNECT_RECV_ENUMERATE_SIMOBJECT_AND_LIVERY_LIST : SIMCONNECT_RECV
{
    public uint dwRequestID;
    public uint dwArraySize;
    public uint dwEntryNumber;
    public uint dwOutOf;
    public object[] rgData = [];
}

public struct SIMCONNECT_ENUMERATE_SIMOBJECT_LIVERY
{
    public string AircraftTitle;
    public string LiveryName;
}

public struct SIMCONNECT_DATA_XYZ
{
    public double x;
    public double y;
    public double z;
}

public struct SIMCONNECT_DATA_PBH
{
    public float Pitch;
    public float Bank;
    public float Heading;
}

public struct SIMCONNECT_DATA_CAMERA
{
    public SIMCONNECT_DATA_XYZ Position;
    public SIMCONNECT_POSITION_REFERENTIAL PositionReferential;
    public uint PositionReferentialObjectId;
    public SIMCONNECT_DATA_XYZ TargetedPos;
    public SIMCONNECT_DATA_PBH Pbh;
    public SIMCONNECT_POSITION_REFERENTIAL RotationReferential;
    public uint RotationReferentialObjectId;
    public double Fov;
}

public enum SIMCONNECT_POSITION_REFERENTIAL
{
    NONE = 0,
    SIMOBJECT = 1,
    WORLD = 2,
    EYEPOINT = 3,
    SIMOBJECT_DATUM = 4
}

public enum SIMCONNECT_CAMERA_DATA_MASK
{
    NONE = 0,
    POSITION = 1,
    ROTATION = 2,
    TARGETED = 4,
    FOV = 8,
    ALL_ROTATION = 6,
    ALL_TARGETED = 12
}

public enum SIMCONNECT_SIMOBJECT_TYPE
{
    USER,
    USER_AIRCRAFT,
    ALL,
    AIRCRAFT,
    HELICOPTER,
    BOAT,
    GROUND,
    HOT_AIR_BALLOON,
    ANIMAL,
    USER_AVATAR,
    USER_CURRENT
}

public enum SIMCONNECT_EXCEPTION
{
    NONE = 0,
    ERROR = 1,
    SIZE_MISMATCH = 2,
    UNRECOGNIZED_ID = 3,
    UNOPENED = 4,
    VERSION_MISMATCH = 5,
    TOO_MANY_GROUPS = 6,
    NAME_UNRECOGNIZED = 7,
    TOO_MANY_EVENT_NAMES = 8,
    EVENT_ID_DUPLICATE = 9,
    TOO_MANY_MAPS = 10,
    TOO_MANY_OBJECTS = 11,
    TOO_MANY_REQUESTS = 12,
    WEATHER_INVALID_PORT = 13,
    WEATHER_INVALID_METAR = 14,
    WEATHER_UNABLE_TO_GET_OBSERVATION = 15,
    WEATHER_UNABLE_TO_CREATE_STATION = 16,
    WEATHER_UNABLE_TO_REMOVE_STATION = 17,
    INVALID_DATA_TYPE = 18,
    INVALID_DATA_SIZE = 19,
    DATA_ERROR = 20,
    INVALID_ARRAY = 21,
    CREATE_OBJECT_FAILED = 22,
    LOAD_FLIGHTPLAN_FAILED = 23,
    OPERATION_INVALID_FOR_OBJECT_TYPE = 24,
    ILLEGAL_OPERATION = 25,
    ALREADY_SUBSCRIBED = 26,
    INVALID_ENUM = 27,
    DEFINITION_ERROR = 28,
    DUPLICATE_ID = 29,
    DATUM_ID = 30,
    OUT_OF_BOUNDS = 31,
    ALREADY_CREATED = 32,
    OBJECT_OUTSIDE_REALITY_BUBBLE = 33,
    OBJECT_CONTAINER = 34,
    OBJECT_AI = 35,
    OBJECT_ATC = 36,
    OBJECT_SCHEDULE = 37,
    JETWAY_DATA = 38,
    ACTION_NOT_FOUND = 39,
    NOT_AN_ACTION = 40,
    INCORRECT_ACTION_PARAMS = 41,
    GET_INPUT_EVENT_FAILED = 42,
    SET_INPUT_EVENT_FAILED = 43,
    EVENT_NAME_RESERVED = 44,
    INTERNAL = 45,
    CAMERA_API = 46
}

public struct SIMCONNECT_DATA_INITPOSITION
{
    public double Latitude;
    public double Longitude;
    public double Altitude;
    public double Pitch;
    public double Bank;
    public double Heading;
    public uint OnGround;
    public uint Airspeed;
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
    public event Action<SimConnect, SIMCONNECT_RECV_ASSIGNED_OBJECT_ID>? OnRecvAssignedObjectId;
    public event Action<SimConnect, SIMCONNECT_RECV_ENUMERATE_SIMOBJECT_AND_LIVERY_LIST>? OnRecvEnumerateSimobjectAndLiveryList;

    public void AddToDataDefinition(Enum definitionId, string name, string? units, SIMCONNECT_DATATYPE dataType, float epsilon, uint datumId) { }
    public void RegisterDataDefineStruct<T>(Enum definitionId) where T : struct { }
    public void RequestDataOnSimObject(Enum requestId, Enum definitionId, uint objectId, SIMCONNECT_PERIOD period, SIMCONNECT_DATA_REQUEST_FLAG flags, uint origin, uint interval, uint limit) { }
    public void SetDataOnSimObject(Enum definitionId, uint objectId, SIMCONNECT_DATA_SET_FLAG flags, object data) { }
    public const uint SIMCONNECT_GROUP_PRIORITY_HIGHEST = 1;
    public void AICreateSimulatedObject_EX1(string szContainerTitle, string? szLivery, SIMCONNECT_DATA_INITPOSITION InitPos, Enum RequestID) { }
    public void AIRemoveObject(uint ObjectID, Enum RequestID) { }
    public void MapClientEventToSimEvent(Enum eventId, string eventName) { }
    public void TransmitClientEvent(uint objectId, Enum eventId, uint data, Enum groupId, SIMCONNECT_EVENT_FLAG flags) { }
    public void EnumerateSimObjectsAndLiveries(Enum RequestID, SIMCONNECT_SIMOBJECT_TYPE Type) { }
    public void CameraAcquire(string ClientId) { }
    public void CameraRelease(string CameraDefName) { }
    public void CameraSet(SIMCONNECT_DATA_CAMERA CameraData, uint DataMask) { }
    public void CameraSetRelative6DOF(float x, float y, float z, float pitch, float bank, float heading) { }
    public uint GetLastSentPacketID() => 0;
    public void ReceiveMessage() { }
    public void Dispose() { }
}

public enum SIMCONNECT_EVENT_FLAG { GROUPID_IS_PRIORITY = 16 }
