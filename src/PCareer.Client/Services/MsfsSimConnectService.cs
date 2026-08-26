#if SIMCONNECT_AVAILABLE
using System.Runtime.InteropServices;
using Microsoft.FlightSimulator.SimConnect;
using PCareer.Client.Models;

namespace PCareer.Client.Services;

internal sealed class MsfsSimConnectService : ISimulatorConnection
{
    private enum DataDefinition
    {
        UserAircraft,
        AircraftIdentity
    }

    private enum DataRequest
    {
        UserAircraft,
        AircraftIdentity
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi, Pack = 1)]
    private struct UserAircraftData
    {
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)]
        public string AircraftTitle;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)]
        public string AircraftAtcModel;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)]
        public string AircraftAtcType;
        public double LatitudeDegrees;
        public double LongitudeDegrees;
        public double AltitudeFeet;
        public double AltitudeAglFeet;
        public double IndicatedAirspeedKnots;
        public double GroundSpeedKnots;
        public double VerticalSpeedFeetPerMinute;
        public double HeadingTrueRadians;
        public double PitchRadians;
        public double BankRadians;
        public int OnGround;
        public int SlewActive;
        public double SimulationRate;
        public double FuelTotalKg;
        public double TotalWeightPounds;
        public double EmptyWeightPounds;
        public int EngineCount;
        public double GearPositionPercent;
        public int ParkingBrakeSet;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi, Pack = 1)]
    private struct AircraftIdentityData
    {
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)]
        public string AircraftTitle;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)]
        public string AircraftAtcType;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)]
        public string AircraftAtcModel;
    }

    private SimConnect? _simConnect;

    public bool IsConnected { get; private set; }

    public string StatusMessage { get; private set; } = "Microsoft Flight Simulator is not running.";

    public event EventHandler? ConnectionChanged;

    public event EventHandler<TelemetrySnapshot>? TelemetryReceived;

    public event EventHandler<AircraftSnapshot>? AircraftIdentityReceived;

    public void TryConnect(IntPtr windowHandle, int messageId)
    {
        if (_simConnect is not null)
        {
            return;
        }

        try
        {
            var connection = new SimConnect(
                "Virtual Pilot Network",
                windowHandle,
                (uint)messageId,
                null,
                0);

            connection.OnRecvOpen += OnOpen;
            connection.OnRecvQuit += OnQuit;
            connection.OnRecvException += OnException;
            connection.OnRecvSimobjectData += OnSimObjectData;
            _simConnect = connection;
            StatusMessage = "Connecting to Microsoft Flight Simulator…";
        }
        catch (COMException)
        {
            StatusMessage = "Microsoft Flight Simulator is not running.";
            IsConnected = false;
            DisposeConnection();
        }
        catch (Exception exception)
        {
            StatusMessage = $"SimConnect error: {exception.Message}";
            IsConnected = false;
            DisposeConnection();
        }

        ConnectionChanged?.Invoke(this, EventArgs.Empty);
    }

    public void ReceiveMessage()
    {
        try
        {
            _simConnect?.ReceiveMessage();
        }
        catch (Exception exception)
        {
            StatusMessage = $"Simulator connection lost: {exception.Message}";
            IsConnected = false;
            DisposeConnection();
            ConnectionChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    private void OnOpen(SimConnect sender, SIMCONNECT_RECV_OPEN data)
    {
        try
        {
            DefineTelemetry(sender);
            DefineAircraftIdentity(sender);
            IsConnected = true;
            StatusMessage = "Connected to Microsoft Flight Simulator 2024.";
        }
        catch (Exception exception)
        {
            IsConnected = false;
            StatusMessage = $"Could not request simulator telemetry: {exception.Message}";
            DisposeConnection();
        }

        ConnectionChanged?.Invoke(this, EventArgs.Empty);
    }

    private static void AddFloat64(
        SimConnect connection,
        DataDefinition definition,
        string name,
        string units) =>
        connection.AddToDataDefinition(
            definition,
            name,
            units,
            SIMCONNECT_DATATYPE.FLOAT64,
            0,
            SimConnect.SIMCONNECT_UNUSED);

    private static void AddInt32(
        SimConnect connection,
        DataDefinition definition,
        string name,
        string units) =>
        connection.AddToDataDefinition(
            definition,
            name,
            units,
            SIMCONNECT_DATATYPE.INT32,
            0,
            SimConnect.SIMCONNECT_UNUSED);

    private static void DefineTelemetry(SimConnect connection)
    {
        connection.AddToDataDefinition(
            DataDefinition.UserAircraft,
            "TITLE",
            null,
            SIMCONNECT_DATATYPE.STRING256,
            0,
            SimConnect.SIMCONNECT_UNUSED);
        connection.AddToDataDefinition(
            DataDefinition.UserAircraft,
            "ATC MODEL",
            null,
            SIMCONNECT_DATATYPE.STRING256,
            0,
            SimConnect.SIMCONNECT_UNUSED);
        connection.AddToDataDefinition(
            DataDefinition.UserAircraft,
            "ATC TYPE",
            null,
            SIMCONNECT_DATATYPE.STRING256,
            0,
            SimConnect.SIMCONNECT_UNUSED);
        AddFloat64(connection, DataDefinition.UserAircraft, "PLANE LATITUDE", "degrees");
        AddFloat64(connection, DataDefinition.UserAircraft, "PLANE LONGITUDE", "degrees");
        AddFloat64(connection, DataDefinition.UserAircraft, "PLANE ALTITUDE", "feet");
        AddFloat64(connection, DataDefinition.UserAircraft, "PLANE ALT ABOVE GROUND", "feet");
        AddFloat64(connection, DataDefinition.UserAircraft, "AIRSPEED INDICATED", "knots");
        AddFloat64(connection, DataDefinition.UserAircraft, "GROUND VELOCITY", "knots");
        AddFloat64(connection, DataDefinition.UserAircraft, "VERTICAL SPEED", "feet per minute");
        AddFloat64(connection, DataDefinition.UserAircraft, "PLANE HEADING DEGREES TRUE", "radians");
        AddFloat64(connection, DataDefinition.UserAircraft, "PLANE PITCH DEGREES", "radians");
        AddFloat64(connection, DataDefinition.UserAircraft, "PLANE BANK DEGREES", "radians");
        AddInt32(connection, DataDefinition.UserAircraft, "SIM ON GROUND", "bool");
        AddInt32(connection, DataDefinition.UserAircraft, "IS SLEW ACTIVE", "bool");
        AddFloat64(connection, DataDefinition.UserAircraft, "SIMULATION RATE", "number");
        AddFloat64(connection, DataDefinition.UserAircraft, "FUEL TOTAL QUANTITY WEIGHT", "kilograms");
        AddFloat64(connection, DataDefinition.UserAircraft, "TOTAL WEIGHT", "pounds");
        AddFloat64(connection, DataDefinition.UserAircraft, "EMPTY WEIGHT", "pounds");
        AddInt32(connection, DataDefinition.UserAircraft, "NUMBER OF ENGINES", "number");
        AddFloat64(connection, DataDefinition.UserAircraft, "GEAR TOTAL PCT EXTENDED", "percent");
        AddInt32(connection, DataDefinition.UserAircraft, "BRAKE PARKING POSITION", "bool");

        connection.RegisterDataDefineStruct<UserAircraftData>(DataDefinition.UserAircraft);
        connection.RequestDataOnSimObject(
            DataRequest.UserAircraft,
            DataDefinition.UserAircraft,
            SimConnect.SIMCONNECT_OBJECT_ID_USER,
            SIMCONNECT_PERIOD.SIM_FRAME,
            SIMCONNECT_DATA_REQUEST_FLAG.DEFAULT,
            0,
            5,
            0);
    }

    private static void DefineAircraftIdentity(SimConnect connection)
    {
        connection.AddToDataDefinition(
            DataDefinition.AircraftIdentity,
            "TITLE",
            null,
            SIMCONNECT_DATATYPE.STRING256,
            0,
            SimConnect.SIMCONNECT_UNUSED);
        connection.AddToDataDefinition(
            DataDefinition.AircraftIdentity,
            "ATC TYPE",
            null,
            SIMCONNECT_DATATYPE.STRING256,
            0,
            SimConnect.SIMCONNECT_UNUSED);
        connection.AddToDataDefinition(
            DataDefinition.AircraftIdentity,
            "ATC MODEL",
            null,
            SIMCONNECT_DATATYPE.STRING256,
            0,
            SimConnect.SIMCONNECT_UNUSED);
        connection.AddToDataDefinition(
            DataDefinition.AircraftIdentity,
            "ATC MODEL",
            null,
            SIMCONNECT_DATATYPE.STRING256,
            0,
            SimConnect.SIMCONNECT_UNUSED);

        connection.RegisterDataDefineStruct<AircraftIdentityData>(DataDefinition.AircraftIdentity);
    }

    public void RequestAircraftIdentity()
    {
        var connection = _simConnect
            ?? throw new InvalidOperationException("Microsoft Flight Simulator is not connected.");
        connection.RequestDataOnSimObject(
            DataRequest.AircraftIdentity,
            DataDefinition.AircraftIdentity,
            SimConnect.SIMCONNECT_OBJECT_ID_USER,
            SIMCONNECT_PERIOD.ONCE,
            SIMCONNECT_DATA_REQUEST_FLAG.DEFAULT,
            0,
            0,
            0);
    }

    private void OnSimObjectData(SimConnect sender, SIMCONNECT_RECV_SIMOBJECT_DATA data)
    {
        if (data.dwData.Length == 0)
        {
            return;
        }

        switch ((DataRequest)data.dwRequestID)
        {
            case DataRequest.UserAircraft:
                PublishTelemetry((UserAircraftData)data.dwData[0]);
                break;
            case DataRequest.AircraftIdentity:
                PublishIdentity((AircraftIdentityData)data.dwData[0]);
                break;
        }
    }

    private void PublishTelemetry(UserAircraftData sample)
    {
        TelemetryReceived?.Invoke(
            this,
            new TelemetrySnapshot(
                ObservedAt: DateTimeOffset.UtcNow,
                AircraftTitle: sample.AircraftTitle?.TrimEnd('\0') ?? "Unknown aircraft",
                AircraftAtcModel: sample.AircraftAtcModel?.TrimEnd('\0') ?? string.Empty,
                AircraftAtcType: sample.AircraftAtcType?.TrimEnd('\0') ?? string.Empty,
                LatitudeDegrees: sample.LatitudeDegrees,
                LongitudeDegrees: sample.LongitudeDegrees,
                AltitudeFeet: sample.AltitudeFeet,
                AltitudeAglFeet: sample.AltitudeAglFeet,
                IndicatedAirspeedKnots: sample.IndicatedAirspeedKnots,
                GroundSpeedKnots: sample.GroundSpeedKnots,
                VerticalSpeedFeetPerMinute: sample.VerticalSpeedFeetPerMinute,
                HeadingTrueDegrees: RadiansToNormalizedDegrees(sample.HeadingTrueRadians),
                PitchDegrees: RadiansToSignedDegrees(sample.PitchRadians),
                BankDegrees: RadiansToSignedDegrees(sample.BankRadians),
                OnGround: sample.OnGround != 0,
                SlewActive: sample.SlewActive != 0,
                SimulationRate: sample.SimulationRate,
                FuelTotalKg: sample.FuelTotalKg,
                TotalWeightPounds: sample.TotalWeightPounds,
                EmptyWeightPounds: sample.EmptyWeightPounds,
                EngineCount: sample.EngineCount,
                GearPositionPercent: sample.GearPositionPercent,
                ParkingBrakeSet: sample.ParkingBrakeSet != 0));
    }

    private void PublishIdentity(AircraftIdentityData sample)
    {
        AircraftIdentityReceived?.Invoke(
            this,
            new AircraftSnapshot(
                ObservedAt: DateTimeOffset.UtcNow,
                AircraftTitle: sample.AircraftTitle?.TrimEnd('\0') ?? "Unknown aircraft",
                AircraftAtcType: sample.AircraftAtcType?.TrimEnd('\0') ?? string.Empty,
                AircraftAtcModel: sample.AircraftAtcModel?.TrimEnd('\0') ?? string.Empty));
    }

    private void OnQuit(SimConnect sender, SIMCONNECT_RECV data)
    {
        IsConnected = false;
        StatusMessage = "Microsoft Flight Simulator has closed.";
        DisposeConnection();
        ConnectionChanged?.Invoke(this, EventArgs.Empty);
    }

    private void OnException(SimConnect sender, SIMCONNECT_RECV_EXCEPTION data)
    {
        StatusMessage = $"SimConnect reported {data.dwException} (send ID {data.dwSendID}).";
        ConnectionChanged?.Invoke(this, EventArgs.Empty);
    }

    private static double RadiansToNormalizedDegrees(double radians)
    {
        var degrees = radians * 180d / Math.PI;
        return (degrees % 360d + 360d) % 360d;
    }

    private static double RadiansToSignedDegrees(double radians) => radians * 180d / Math.PI;

    private void DisposeConnection()
    {
        var connection = _simConnect;
        _simConnect = null;
        if (connection is not null)
        {
            try
            {
                connection.Dispose();
            }
            catch
            {
                // The simulator may already have torn down the native connection.
            }
        }
    }

    public void Dispose()
    {
        IsConnected = false;
        DisposeConnection();
        GC.SuppressFinalize(this);
    }
}
#endif
