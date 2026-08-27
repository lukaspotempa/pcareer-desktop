using PCareer.Client;
using PCareer.Client.Models;
using PCareer.Client.Services;

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

controller.BeginLoading();
Assert(controller.Phase == FlightPhase.Loading, "Start must first enter the loading phase.");
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

Console.WriteLine("VPN desktop flight lifecycle checks passed.");
return;

static TelemetrySnapshot Sample(
    bool onGround,
    double altitudeAgl,
    string aircraftTitle = "Cessna 172 Skyhawk",
    string aircraftAtcModel = "C172") => new(
    ObservedAt: DateTimeOffset.UtcNow,
    AircraftTitle: aircraftTitle,
    AircraftAtcModel: aircraftAtcModel,
    AircraftAtcType: "Cessna",
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
    ParkingBrakeSet: onGround);

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
