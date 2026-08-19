using PCareer.Client.Models;

namespace PCareer.Client.Services;

public sealed class FlightSessionController
{
    private static readonly HashSet<string> IgnoredAircraftNameTokens = new(
        new[]
        {
            "airbus",
            "beechcraft",
            "boeing",
            "cessna",
            "cirrus",
            "dehavilland",
            "diamond",
            "embraer",
            "piper",
            "pilatus",
        },
        StringComparer.OrdinalIgnoreCase);

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
            && !AircraftMatches(contract, telemetry))
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

    internal static bool AircraftMatches(
        ContractAssignment contract,
        TelemetrySnapshot telemetry)
    {
        var expectedIcao = NormalizeAircraftIdentifier(contract.AircraftIcao);
        if (expectedIcao.Length >= 3)
        {
            var atcModel = NormalizeAircraftIdentifier(telemetry.AircraftAtcModel);
            if (atcModel == expectedIcao || atcModel.StartsWith(expectedIcao, StringComparison.Ordinal))
            {
                return true;
            }

            var titleIdentifier = NormalizeAircraftIdentifier(telemetry.AircraftTitle);
            if (titleIdentifier.Contains(expectedIcao, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return AircraftNamesMatch(
            contract.RequiredAircraftTitleContains ?? string.Empty,
            telemetry.AircraftTitle);
    }

    private static bool AircraftNamesMatch(string requiredName, string simulatorTitle)
    {
        if (simulatorTitle.Contains(requiredName, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        var requiredTokens = AircraftNameTokens(requiredName)
            .Where(token => !IgnoredAircraftNameTokens.Contains(token))
            .ToArray();
        if (requiredTokens.Length == 0)
        {
            requiredTokens = AircraftNameTokens(requiredName).ToArray();
        }
        var simulatorTokens = AircraftNameTokens(simulatorTitle).ToHashSet(
            StringComparer.OrdinalIgnoreCase);
        return requiredTokens.Length > 0
            && requiredTokens.All(simulatorTokens.Contains);
    }

    private static string NormalizeAircraftIdentifier(string? value) =>
        string.Concat((value ?? string.Empty)
            .Where(char.IsLetterOrDigit))
            .ToUpperInvariant();

    private static IEnumerable<string> AircraftNameTokens(string value)
    {
        var token = new System.Text.StringBuilder();
        foreach (var character in value)
        {
            if (char.IsLetterOrDigit(character))
            {
                token.Append(char.ToLowerInvariant(character));
            }
            else if (token.Length > 0)
            {
                yield return token.ToString();
                token.Clear();
            }
        }
        if (token.Length > 0)
        {
            yield return token.ToString();
        }
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
