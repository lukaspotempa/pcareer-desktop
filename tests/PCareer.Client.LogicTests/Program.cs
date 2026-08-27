using PCareer.Client;
using PCareer.Client.Models;
using PCareer.Client.Services;

Assert(
    PCareer.Client.Program.SelectServerUrl(
        "http://localhost:8000/",
        allowDevelopmentOverride: false)
        == PCareer.Client.Program.ProductionServerUrl,
    "Production builds must ignore a development server environment override.");
Assert(
    PCareer.Client.Program.SelectServerUrl(
        "http://localhost:8000/",
        allowDevelopmentOverride: true)
        == "http://localhost:8000/",
    "Development builds should allow an explicit local server override.");
Assert(
    PCareer.Client.Program.SelectServerUrl(" ", allowDevelopmentOverride: true)
        == PCareer.Client.Program.ProductionServerUrl,
    "An empty development override should fall back to the production server.");

Assert(
    PortableUpdater.ParseVersion("v1.2.3") == new Version(1, 2, 3),
    "Portable update versions should accept the release tag format.");
var validManifest = new PortableUpdateManifest(
    "1.2.3",
    "https://github.com/lukaspotempa/pcareer-desktop/releases/download/v1.2.3/VirtualPilotNetwork.exe",
    new string('a', 64),
    1024);
validManifest.Validate();
Assert(
    validManifest.ParsedVersion > new Version(1, 2, 2),
    "A newer portable release should compare above the installed version.");
AssertThrows<InvalidDataException>(
    () => new PortableUpdateManifest(
        "1.2.3",
        "http://example.com/VirtualPilotNetwork.exe",
        new string('a', 64),
        1024).Validate(),
    "Portable updates must reject non-HTTPS downloads.");
AssertThrows<InvalidDataException>(
    () => new PortableUpdateManifest(
        "1.2.3",
        "https://example.com/VirtualPilotNetwork.exe",
        "not-a-hash",
        1024).Validate(),
    "Portable updates must reject invalid checksums.");

var controller = new FlightSessionController();
var contract = ContractAssignment.DevelopmentFlight;
var onGround = Sample(onGround: true, altitudeAgl: 5);

Assert(
    controller.EvaluateReadiness(true, contract, onGround) == "Ready to begin loading.",
    "An on-ground 1x telemetry sample should be ready.");

var c172Contract = new ContractAssignment(
    ContractId: "C172-TEST",
    DepartureName: "Munich",
    ArrivalName: "Nuremberg",
    RequiredAircraftTitleContains: "Cessna 172 Skyhawk",
    DepartureLatitudeDegrees: null,
    DepartureLongitudeDegrees: null,
    DepartureRadiusNauticalMiles: 2)
{
    AircraftIcao = "C172",
    AircraftSimulatorIdentities = new[]
    {
        new AircraftSimulatorIdentity("msfs_2024", "atc_model", "prefix", "C172"),
        new AircraftSimulatorIdentity("msfs_2024", "title", "contains", "C172SP"),
    },
    AirlineIcao = "pcx",
    FlightNumber = "4821",
};
Assert(
    c172Contract.FlightDesignator == "PCX4821",
    "The public flight designator should not expose the internal contract ID.");
Assert(
    controller.EvaluateReadiness(
        true,
        c172Contract,
        Sample(
            onGround: true,
            altitudeAgl: 5,
            aircraftTitle: "C172SP G1000 Passengers",
            aircraftAtcModel: "C172"))
        == "Ready to begin loading.",
    "A configurable C172 passenger variant should match the required Cessna 172 Skyhawk.");
Assert(
    controller.EvaluateReadiness(
        true,
        c172Contract,
        Sample(
            onGround: true,
            altitudeAgl: 5,
            aircraftTitle: "Beechcraft Baron G58",
            aircraftAtcModel: "BE58"))
        == "Select the required aircraft: Cessna 172 Skyhawk.",
    "An unrelated aircraft must not pass the normalized aircraft check.");

var a320NeoContract = new ContractAssignment(
    ContractId: "A20N-TEST",
    DepartureName: "Munich",
    ArrivalName: "Frankfurt",
    RequiredAircraftTitleContains: "Airbus A-320neo",
    DepartureLatitudeDegrees: null,
    DepartureLongitudeDegrees: null,
    DepartureRadiusNauticalMiles: 2)
{
    AircraftIcao = "A20N",
    AircraftSimulatorIdentities = new[]
    {
        new AircraftSimulatorIdentity("msfs_2024", "atc_model", "exact", "A20N"),
        new AircraftSimulatorIdentity(
            "msfs_2024",
            "title",
            "contains",
            "Airbus A320 Neo FlyByWire"),
    },
};
Assert(
    controller.EvaluateReadiness(
        true,
        a320NeoContract,
        Sample(
            onGround: true,
            altitudeAgl: 5,
            aircraftTitle: "FWB SA Lufthansa D-AIJA",
            aircraftAtcModel: "A320",
            aircraftAtcType: "Airbus"))
        == "Ready to begin loading.",
    "A FlyByWire A320neo livery should match without depending on its airline title.");
Assert(
    SimulatorAircraftIdentity.DecodeAtcModel("ATCCOM.AC_MODEL_A20N.0.text") == "A20N"
        && SimulatorAircraftIdentity.DecodeAtcType("ATCCOM.ATC_NAME AIRBUS.0.text") == "AIRBUS",
    "Localized MSFS ATC resource keys should decode to stable aircraft identifiers.");
Assert(
    controller.EvaluateReadiness(
        true,
        a320NeoContract,
        Sample(
            onGround: true,
            altitudeAgl: 5,
            aircraftTitle: "FWB SA Lufthansa D-AIJA",
            aircraftAtcModel: "ATCCOM.AC_MODEL_A20N.0.text",
            aircraftAtcType: "ATCCOM.ATC_NAME AIRBUS.0.text"))
        == "Ready to begin loading.",
    "Localized MSFS identity keys should still match an A20N contract.");
var fbwPayloadPlan = FlyByWireA32NxLoadPlanner.CreatePayloadPlan(19_218.71);
Assert(
    fbwPayloadPlan.PassengersByZone.Sum() <= FlyByWireA32NxLoadPlanner.MaximumPassengers
        && Math.Abs(
            fbwPayloadPlan.PassengersByZone.Sum()
                * FlyByWireA32NxLoadPlanner.PassengerWeightKilograms
                + fbwPayloadPlan.CargoKilogramsByHold.Sum()
                - 19_218.71) < 0.01,
    "The FlyByWire payload plan should preserve the requested total mass.");
var fbwFuelPlan = FlyByWireA32NxLoadPlanner.CreateFuelPlan(5_000, 6.7);
Assert(
    Math.Abs(
        fbwFuelPlan.CenterGallons
            + fbwFuelPlan.LeftInnerGallons
            + fbwFuelPlan.LeftOuterGallons
            + fbwFuelPlan.RightInnerGallons
            + fbwFuelPlan.RightOuterGallons
            - fbwFuelPlan.TotalGallons) < 0.01,
    "The FlyByWire fuel plan should preserve the requested total volume.");
Assert(
    controller.EvaluateReadiness(
        true,
        a320NeoContract,
        Sample(
            onGround: true,
            altitudeAgl: 5,
            aircraftTitle: "FenixA320 CFM SL Lufthansa",
            aircraftAtcModel: "A320",
            aircraftAtcType: "Airbus"))
        == "Select the required aircraft: Airbus A-320neo.",
    "A different Airbus A320 family must not match the FlyByWire A320neo fallback.");

controller.BeginLoading();
Assert(controller.Phase == FlightPhase.Loading, "Start must first enter the loading phase.");
Assert(
    !controller.LoadingStatus(contract, onGround).Contains("±", StringComparison.Ordinal),
    "Player-facing readiness text must not reveal the load tolerance.");
var toleranceContract = contract with
{
    RequiredFuelKg = 100,
    RequiredPayloadKg = 200,
};
Assert(
    controller.LoadsMatch(
        toleranceContract,
        onGround with
        {
            FuelTotalKg = toleranceContract.RequiredFuelKg!.Value * 0.971,
            PayloadStationWeightPounds =
                toleranceContract.RequiredPayloadKg * 1.029 / 0.45359237,
        }),
    "Fuel and payload deviations within three percent must be accepted.");
Assert(
    !controller.LoadsMatch(
        toleranceContract,
        onGround with
        {
            FuelTotalKg = toleranceContract.RequiredFuelKg!.Value * 0.969,
            PayloadStationWeightPounds =
                toleranceContract.RequiredPayloadKg / 0.45359237,
        }),
    "A load deviation above three percent must still be rejected.");
controller.Start(Guid.NewGuid(), onGround);
Assert(controller.Phase == FlightPhase.Started, "Start must enter Started.");

controller.Observe(Sample(onGround: false, altitudeAgl: 800));
Assert(controller.Phase == FlightPhase.Airborne, "Leaving the ground must enter Airborne.");

controller.Observe(Sample(onGround: true, altitudeAgl: 8));
Assert(controller.Phase == FlightPhase.Landed, "Touchdown must enter Landed.");
Assert(controller.CanFinish, "A landed flight must be finishable.");

controller.Finish();
Assert(controller.Phase == FlightPhase.Finished, "Finish must enter Finished.");

controller.ResetForNextFlight();
Assert(controller.Phase == FlightPhase.Ready, "Reset must return the controller to Ready.");
Assert(controller.FlightId is null, "Reset must clear the previous flight identifier.");
Assert(controller.StartedAt is null, "Reset must clear the previous start time.");
Assert(!controller.CanFinish, "A reset flight must not remain finishable.");
Assert(
    controller.EvaluateReadiness(true, contract, onGround) == "Ready to begin loading.",
    "A reset controller must allow the next eligible flight to start.");

controller.BeginLoading();
controller.Start(Guid.NewGuid(), onGround);
Assert(controller.Phase == FlightPhase.Started, "A second flight must start without restarting the app.");
var cancellation = controller.Observe(onGround with { FuelTotalKg = 112 });
Assert(cancellation?.Contains("Fuel was increased") == true, "Refuelling an active flight must cancel it.");
Assert(controller.Phase == FlightPhase.Cancelled, "A load violation must enter Cancelled.");
controller.ResetCancelledFlight();
Assert(controller.Phase == FlightPhase.Ready, "A cancelled session must be resettable.");
controller.BeginLoading();
controller.Start(Guid.NewGuid(), onGround);
var jumped = controller.Observe(onGround with { LatitudeDegrees = 53.3667 });
Assert(
    jumped?.Contains("position changed discontinuously") == true,
    "Reloading at another location must cancel the active flight.");

var payloadTelemetry = Sample(onGround: true, altitudeAgl: 5);
var payloadController = new FlightSessionController();
payloadController.BeginLoading();
payloadController.Start(Guid.NewGuid(), payloadTelemetry);
var asynchronousWeightSample = payloadTelemetry with
{
    ObservedAt = payloadTelemetry.ObservedAt.AddSeconds(1),
    FuelTotalKg = payloadTelemetry.FuelTotalKg - 20,
    TotalWeightPounds = payloadTelemetry.TotalWeightPounds - 10,
};
Assert(
    payloadController.Observe(asynchronousWeightSample) is null,
    "Normal fuel burn must not look like a payload change when weight values update asynchronously.");
var changedPayloadSample = asynchronousWeightSample with
{
    ObservedAt = asynchronousWeightSample.ObservedAt.AddSeconds(1),
    PayloadStationWeightPounds = asynchronousWeightSample.PayloadStationWeightPounds + 20,
};
Assert(
    payloadController.Observe(changedPayloadSample)?.Contains("payload changed") == true,
    "A real payload-station weight change must still cancel an active flight.");

Console.WriteLine("VPN desktop flight lifecycle checks passed.");
return;

static TelemetrySnapshot Sample(
    bool onGround,
    double altitudeAgl,
    string aircraftTitle = "Cessna 172 Skyhawk",
    string aircraftAtcModel = "C172",
    string aircraftAtcType = "Cessna") => new(
    ObservedAt: DateTimeOffset.UtcNow,
    AircraftTitle: aircraftTitle,
    AircraftAtcModel: aircraftAtcModel,
    AircraftAtcType: aircraftAtcType,
    LatitudeDegrees: 52.3667,
    LongitudeDegrees: 13.5033,
    AltitudeFeet: 2500,
    AltitudeAglFeet: altitudeAgl,
    IndicatedAirspeedKnots: onGround ? 0 : 105,
    GroundSpeedKnots: onGround ? 0 : 110,
    VerticalSpeedFeetPerMinute: 0,
    HeadingTrueDegrees: 90,
    PitchDegrees: 0,
    BankDegrees: 0,
    OnGround: onGround,
    SlewActive: false,
    SimulationRate: 1,
    FuelTotalKg: 108.9,
    TotalWeightPounds: 2300,
    EmptyWeightPounds: 1663,
    EngineCount: 1,
    GearPositionPercent: 100,
    ParkingBrakeSet: onGround,
    PayloadStationWeightPounds: (2300d - 1663d) - 108.9d / 0.45359237d);

static void Assert(bool condition, string message)
{
    if (!condition)
    {
        throw new InvalidOperationException(message);
    }
}

static void AssertThrows<TException>(Action action, string message)
    where TException : Exception
{
    try
    {
        action();
    }
    catch (TException)
    {
        return;
    }

    throw new InvalidOperationException(message);
}
