using PCareer.Client.Models;
using PCareer.Client.Services;

var controller = new FlightSessionController();
var contract = ContractAssignment.DevelopmentFlight;
var onGround = Sample(onGround: true, altitudeAgl: 5);

Assert(
    controller.EvaluateReadiness(true, contract, onGround) == "Ready to start flight.",
    "An on-ground 1x telemetry sample should be ready.");

controller.Start(Guid.NewGuid(), onGround);
Assert(controller.Phase == FlightPhase.Started, "Start must enter Started.");

controller.Observe(Sample(onGround: false, altitudeAgl: 800));
Assert(controller.Phase == FlightPhase.Airborne, "Leaving the ground must enter Airborne.");

controller.Observe(Sample(onGround: true, altitudeAgl: 8));
Assert(controller.Phase == FlightPhase.Landed, "Touchdown must enter Landed.");
Assert(controller.CanFinish, "A landed flight must be finishable.");

controller.Finish();
Assert(controller.Phase == FlightPhase.Finished, "Finish must enter Finished.");

Console.WriteLine("PCareer desktop flight lifecycle checks passed.");
return;

static TelemetrySnapshot Sample(bool onGround, double altitudeAgl) => new(
    ObservedAt: DateTimeOffset.UtcNow,
    AircraftTitle: "Cessna 172 Skyhawk",
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
    FuelTotalGallons: 40,
    TotalWeightPounds: 2300,
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

