namespace PCareer.Client.Models;

public sealed record TelemetrySnapshot(
    DateTimeOffset ObservedAt,
    string AircraftTitle,
    string AircraftAtcModel,
    string AircraftAtcType,
    double LatitudeDegrees,
    double LongitudeDegrees,
    double AltitudeFeet,
    double AltitudeAglFeet,
    double IndicatedAirspeedKnots,
    double GroundSpeedKnots,
    double VerticalSpeedFeetPerMinute,
    double HeadingTrueDegrees,
    double PitchDegrees,
    double BankDegrees,
    bool OnGround,
    bool SlewActive,
    double SimulationRate,
    double FuelTotalKg,
    double TotalWeightPounds,
    double EmptyWeightPounds,
    int EngineCount,
    double GearPositionPercent,
    bool ParkingBrakeSet,
    double PayloadStationWeightPounds = double.NaN)
{
    private const double KilogramsPerPound = 0.45359237;

    public double PayloadWeightKg => Math.Max(
        0,
        double.IsFinite(PayloadStationWeightPounds)
            ? PayloadStationWeightPounds * KilogramsPerPound
            : (TotalWeightPounds - EmptyWeightPounds) * KilogramsPerPound - FuelTotalKg);
}
