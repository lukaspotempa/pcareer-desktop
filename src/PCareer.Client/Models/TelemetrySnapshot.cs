namespace PCareer.Client.Models;

public sealed record TelemetrySnapshot(
    DateTimeOffset ObservedAt,
    string AircraftTitle,
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
    double FuelTotalGallons,
    double TotalWeightPounds,
    int EngineCount,
    double GearPositionPercent,
    bool ParkingBrakeSet);

