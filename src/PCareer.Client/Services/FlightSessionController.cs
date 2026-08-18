using PCareer.Client.Models;

namespace PCareer.Client.Services;

public sealed class FlightSessionController
{
    public FlightPhase Phase { get; private set; } = FlightPhase.Ready;

    public Guid? FlightId { get; private set; }

    public DateTimeOffset? StartedAt { get; private set; }

    public string EvaluateReadiness(
        bool simulatorConnected,
        ContractAssignment contract,
        TelemetrySnapshot? telemetry)
    {
        if (Phase is not FlightPhase.Ready)
        {
            return "A flight session is already active.";
        }

        if (!simulatorConnected || telemetry is null)
        {
            return "Start Microsoft Flight Simulator and load into a flight.";
        }

        if (DateTimeOffset.UtcNow - telemetry.ObservedAt > TimeSpan.FromSeconds(5))
        {
            return "Waiting for fresh simulator telemetry.";
        }

        if (!telemetry.OnGround)
        {
            return "The aircraft must be on the ground.";
        }

        if (telemetry.SlewActive)
        {
            return "Disable slew mode before starting.";
        }

        if (Math.Abs(telemetry.SimulationRate - 1d) > 0.01)
        {
            return "Set the simulation rate to 1× before starting.";
        }

        if (!string.IsNullOrWhiteSpace(contract.RequiredAircraftTitleContains)
            && !telemetry.AircraftTitle.Contains(
                contract.RequiredAircraftTitleContains,
                StringComparison.OrdinalIgnoreCase))
        {
            return $"Select the required aircraft: {contract.RequiredAircraftTitleContains}.";
        }

        if (contract.DepartureLatitudeDegrees is double departureLatitude
            && contract.DepartureLongitudeDegrees is double departureLongitude)
        {
            var distance = DistanceNauticalMiles(
                telemetry.LatitudeDegrees,
                telemetry.LongitudeDegrees,
                departureLatitude,
                departureLongitude);
            if (distance > contract.DepartureRadiusNauticalMiles)
            {
                return $"Move to {contract.DepartureName} ({distance:0.0} NM away).";
            }
        }

        return "Ready to start flight.";
    }

    public void Start(Guid flightId, TelemetrySnapshot telemetry)
    {
        if (Phase is not FlightPhase.Ready)
        {
            throw new InvalidOperationException("A flight has already been started.");
        }

        FlightId = flightId;
        StartedAt = telemetry.ObservedAt;
        Phase = FlightPhase.Started;
    }

    public void Observe(TelemetrySnapshot telemetry)
    {
        if (Phase is FlightPhase.Started && !telemetry.OnGround)
        {
            Phase = FlightPhase.Airborne;
        }
        else if (
            Phase is FlightPhase.Airborne
            && telemetry.OnGround
            && telemetry.AltitudeAglFeet < 100)
        {
            Phase = FlightPhase.Landed;
        }
    }

    public bool CanFinish => Phase is FlightPhase.Landed && FlightId.HasValue;

    public void Finish()
    {
        if (!CanFinish)
        {
            throw new InvalidOperationException(
                "The aircraft must take off and land before the flight can finish.");
        }

        Phase = FlightPhase.Finished;
    }

    private static double DistanceNauticalMiles(
        double latitude1,
        double longitude1,
        double latitude2,
        double longitude2)
    {
        const double earthRadiusNauticalMiles = 3440.065;
        var latitudeDelta = DegreesToRadians(latitude2 - latitude1);
        var longitudeDelta = DegreesToRadians(longitude2 - longitude1);
        var firstLatitude = DegreesToRadians(latitude1);
        var secondLatitude = DegreesToRadians(latitude2);

        var haversine =
            Math.Pow(Math.Sin(latitudeDelta / 2), 2)
            + Math.Cos(firstLatitude)
            * Math.Cos(secondLatitude)
            * Math.Pow(Math.Sin(longitudeDelta / 2), 2);
        return 2 * earthRadiusNauticalMiles * Math.Asin(Math.Sqrt(haversine));
    }

    private static double DegreesToRadians(double degrees) => degrees * Math.PI / 180d;
}

