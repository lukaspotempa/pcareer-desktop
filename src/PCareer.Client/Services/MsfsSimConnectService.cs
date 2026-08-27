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
        public int PayloadStationCount;
        public double FuelTotalCapacityGallons;
        public double FuelWeightPerGallonPounds;
        public int NewFuelSystem;
        public double ModernTank1Capacity;
        public double ModernTank2Capacity;
        public double ModernTank3Capacity;
        public double ModernTank4Capacity;
        public double ModernTank5Capacity;
        public double ModernTank6Capacity;
        public double ModernTank7Capacity;
        public double ModernTank8Capacity;
        public double ModernTank9Capacity;
        public double ModernTank10Capacity;
        public double ModernTank11Capacity;
        public double ModernTank12Capacity;
        public double ModernTank13Capacity;
        public double ModernTank14Capacity;
        public double ModernTank15Capacity;
        public double ModernTank16Capacity;
        public double ModernTank17Capacity;
        public double ModernTank18Capacity;
        public double ModernTank19Capacity;
        public double ModernTank20Capacity;
        public double LegacyCenterCapacity;
        public double LegacyCenter2Capacity;
        public double LegacyCenter3Capacity;
        public double LegacyExternal1Capacity;
        public double LegacyExternal2Capacity;
        public double LegacyLeftAuxCapacity;
        public double LegacyLeftMainCapacity;
        public double LegacyLeftTipCapacity;
        public double LegacyRightAuxCapacity;
        public double LegacyRightMainCapacity;
        public double LegacyRightTipCapacity;
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
    private int _payloadStationCount;
    private double _fuelWeightPerGallonPounds;
    private bool _usesModernFuelSystem;
    private readonly double[] _modernFuelTankCapacities = new double[20];
    private readonly double[] _legacyFuelTankCapacities = new double[11];
    private readonly HashSet<int> _definedWriteDefinitions = [];

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
        AddInt32(connection, DataDefinition.UserAircraft, "PAYLOAD STATION COUNT", "number");
        AddFloat64(connection, DataDefinition.UserAircraft, "FUEL TOTAL CAPACITY", "gallons");
        AddFloat64(connection, DataDefinition.UserAircraft, "FUEL WEIGHT PER GALLON", "pounds");
        AddInt32(connection, DataDefinition.UserAircraft, "NEW FUEL SYSTEM", "bool");
        for (var tank = 1; tank <= 20; tank++)
        {
            AddFloat64(
                connection,
                DataDefinition.UserAircraft,
                $"FUELSYSTEM TANK CAPACITY:{tank}",
                "gallons");
        }

        string[] legacyFuelCapacityVariables =
        [
            "FUEL TANK CENTER CAPACITY",
            "FUEL TANK CENTER2 CAPACITY",
            "FUEL TANK CENTER3 CAPACITY",
            "FUEL TANK EXTERNAL1 CAPACITY",
            "FUEL TANK EXTERNAL2 CAPACITY",
            "FUEL TANK LEFT AUX CAPACITY",
            "FUEL TANK LEFT MAIN CAPACITY",
            "FUEL TANK LEFT TIP CAPACITY",
            "FUEL TANK RIGHT AUX CAPACITY",
            "FUEL TANK RIGHT MAIN CAPACITY",
            "FUEL TANK RIGHT TIP CAPACITY",
        ];
        foreach (var variable in legacyFuelCapacityVariables)
        {
            AddFloat64(connection, DataDefinition.UserAircraft, variable, "gallons");
        }

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

    public void SetPayloadKilograms(double payloadKilograms)
    {
        var connection = _simConnect
            ?? throw new InvalidOperationException("Microsoft Flight Simulator is not connected.");
        if (!double.IsFinite(payloadKilograms) || payloadKilograms < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(payloadKilograms),
                "Payload must be a non-negative weight.");
        }

        var stationCount = Math.Clamp(_payloadStationCount, 0, 15);
        if (stationCount == 0)
        {
            throw new InvalidOperationException("The loaded aircraft does not expose any payload stations.");
        }

        const double poundsPerKilogram = 2.20462262185d;
        var stationWeight = payloadKilograms * poundsPerKilogram / stationCount;
        for (var station = 1; station <= stationCount; station++)
        {
            SetWritableValue(
                connection,
                100 + station,
                $"PAYLOAD STATION WEIGHT:{station}",
                "pounds",
                stationWeight);
        }
    }

    public void SetFuelKilograms(double fuelKilograms)
    {
        var connection = _simConnect
            ?? throw new InvalidOperationException("Microsoft Flight Simulator is not connected.");
        if (!double.IsFinite(fuelKilograms) || fuelKilograms < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(fuelKilograms),
                "Fuel must be a non-negative weight.");
        }
        if (_fuelWeightPerGallonPounds <= 0)
        {
            throw new InvalidOperationException("The loaded aircraft did not report a usable fuel capacity.");
        }

        var capacities = _usesModernFuelSystem
            ? _modernFuelTankCapacities
            : _legacyFuelTankCapacities;
        var totalCapacityGallons = capacities.Where(capacity => capacity > 0.001d).Sum();
        if (totalCapacityGallons <= 0)
        {
            throw new InvalidOperationException("The loaded aircraft did not expose any writable fuel tanks.");
        }

        const double kilogramsPerPound = 0.45359237d;
        var capacityKilograms =
            totalCapacityGallons * _fuelWeightPerGallonPounds * kilogramsPerPound;
        if (fuelKilograms > capacityKilograms + 1d)
        {
            throw new InvalidOperationException(
                $"The requested {fuelKilograms:0.0} kg exceeds this aircraft's "
                + $"{capacityKilograms:0.0} kg fuel capacity.");
        }

        var level = Math.Clamp(fuelKilograms / capacityKilograms, 0d, 1d);
        if (_usesModernFuelSystem)
        {
            for (var tank = 0; tank < _modernFuelTankCapacities.Length; tank++)
            {
                if (_modernFuelTankCapacities[tank] <= 0.001d)
                {
                    continue;
                }

                SetWritableValue(
                    connection,
                    300 + tank,
                    $"FUELSYSTEM TANK LEVEL:{tank + 1}",
                    "percent over 100",
                    level);
            }
            return;
        }

        string[] legacyFuelLevelVariables =
        [
            "FUEL TANK CENTER LEVEL",
            "FUEL TANK CENTER2 LEVEL",
            "FUEL TANK CENTER3 LEVEL",
            "FUEL TANK EXTERNAL1 LEVEL",
            "FUEL TANK EXTERNAL2 LEVEL",
            "FUEL TANK LEFT AUX LEVEL",
            "FUEL TANK LEFT MAIN LEVEL",
            "FUEL TANK LEFT TIP LEVEL",
            "FUEL TANK RIGHT AUX LEVEL",
            "FUEL TANK RIGHT MAIN LEVEL",
            "FUEL TANK RIGHT TIP LEVEL",
        ];
        for (var tank = 0; tank < _legacyFuelTankCapacities.Length; tank++)
        {
            if (_legacyFuelTankCapacities[tank] <= 0.001d)
            {
                continue;
            }

            SetWritableValue(
                connection,
                200 + tank,
                legacyFuelLevelVariables[tank],
                "percent over 100",
                level);
        }
    }

    private void SetWritableValue(
        SimConnect connection,
        int definitionId,
        string name,
        string units,
        double value)
    {
        var definition = (DataDefinition)definitionId;
        if (_definedWriteDefinitions.Add(definitionId))
        {
            AddFloat64(connection, definition, name, units);
        }

        connection.SetDataOnSimObject(
            definition,
            SimConnect.SIMCONNECT_OBJECT_ID_USER,
            SIMCONNECT_DATA_SET_FLAG.DEFAULT,
            value);
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
        _payloadStationCount = sample.PayloadStationCount;
        _fuelWeightPerGallonPounds = sample.FuelWeightPerGallonPounds;
        _usesModernFuelSystem = sample.NewFuelSystem != 0;
        double[] modernCapacities =
        [
            sample.ModernTank1Capacity, sample.ModernTank2Capacity,
            sample.ModernTank3Capacity, sample.ModernTank4Capacity,
            sample.ModernTank5Capacity, sample.ModernTank6Capacity,
            sample.ModernTank7Capacity, sample.ModernTank8Capacity,
            sample.ModernTank9Capacity, sample.ModernTank10Capacity,
            sample.ModernTank11Capacity, sample.ModernTank12Capacity,
            sample.ModernTank13Capacity, sample.ModernTank14Capacity,
            sample.ModernTank15Capacity, sample.ModernTank16Capacity,
            sample.ModernTank17Capacity, sample.ModernTank18Capacity,
            sample.ModernTank19Capacity, sample.ModernTank20Capacity,
        ];
        modernCapacities.CopyTo(_modernFuelTankCapacities, 0);
        double[] legacyCapacities =
        [
            sample.LegacyCenterCapacity, sample.LegacyCenter2Capacity,
            sample.LegacyCenter3Capacity, sample.LegacyExternal1Capacity,
            sample.LegacyExternal2Capacity, sample.LegacyLeftAuxCapacity,
            sample.LegacyLeftMainCapacity, sample.LegacyLeftTipCapacity,
            sample.LegacyRightAuxCapacity, sample.LegacyRightMainCapacity,
            sample.LegacyRightTipCapacity,
        ];
        legacyCapacities.CopyTo(_legacyFuelTankCapacities, 0);

        TelemetryReceived?.Invoke(
            this,
            new TelemetrySnapshot(
                ObservedAt: DateTimeOffset.UtcNow,
                AircraftTitle: sample.AircraftTitle?.TrimEnd('\0') ?? "Unknown aircraft",
                AircraftAtcModel: SimulatorAircraftIdentity.DecodeAtcModel(sample.AircraftAtcModel),
                AircraftAtcType: SimulatorAircraftIdentity.DecodeAtcType(sample.AircraftAtcType),
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
                AircraftAtcType: SimulatorAircraftIdentity.DecodeAtcType(sample.AircraftAtcType),
                AircraftAtcModel: SimulatorAircraftIdentity.DecodeAtcModel(sample.AircraftAtcModel)));
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
        StatusMessage = data.dwException == 20
            ? $"SimConnect rejected a simulator data write (send ID {data.dwSendID})."
            : $"SimConnect reported {data.dwException} (send ID {data.dwSendID}).";
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
        _payloadStationCount = 0;
        _fuelWeightPerGallonPounds = 0;
        _usesModernFuelSystem = false;
        Array.Clear(_modernFuelTankCapacities);
        Array.Clear(_legacyFuelTankCapacities);
        _definedWriteDefinitions.Clear();
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
