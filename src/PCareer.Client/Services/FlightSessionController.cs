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

    public double? InitialFuelKg { get; private set; }

    public double? InitialPayloadKg { get; private set; }

    private TelemetrySnapshot? _previousTelemetry;

    public string EvaluateReadiness(
        bool simulatorConnected,
        ContractAssignment contract,
        TelemetrySnapshot? telemetry)
    {
        if (Phase is FlightPhase.Loading)
        {
            return LoadingStatus(contract, telemetry);
        }

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

        return "Ready to begin loading.";
    }

    public void BeginLoading()
    {
        if (Phase is not FlightPhase.Ready)
        {
            throw new InvalidOperationException("The flight is not ready for loading.");
        }
        Phase = FlightPhase.Loading;
    }

    public void AbortLoading()
    {
        if (Phase is FlightPhase.Loading)
        {
            Phase = FlightPhase.Ready;
        }
    }

    public bool LoadsMatch(ContractAssignment contract, TelemetrySnapshot telemetry) =>
        FuelMatches(contract.RequiredFuelKg, telemetry.FuelTotalKg)
        && WithinOnePercent(contract.RequiredPayloadKg, telemetry.PayloadWeightKg);

    public string LoadingStatus(ContractAssignment contract, TelemetrySnapshot? telemetry)
    {
        var fuelTarget = contract.RequiredFuelKg is double fuel ? $"{fuel:0.0} kg" : "the SimBrief plan";
        var payloadTarget = $"{contract.RequiredPayloadKg:0.0} kg";
        if (telemetry is null)
        {
            return $"Load fuel {fuelTarget} and payload {payloadTarget}.";
        }

        if (LoadsMatch(contract, telemetry))
        {
            return "Fuel and payload match — activating flight…";
        }

        return $"Load fuel {fuelTarget} (now {telemetry.FuelTotalKg:0.0} kg) and payload "
            + $"{payloadTarget} (now {telemetry.PayloadWeightKg:0.0} kg).";
    }

    public void Start(Guid flightId, TelemetrySnapshot telemetry)
    {
        if (Phase is not FlightPhase.Loading)
        {
            throw new InvalidOperationException("A flight has already been started.");
        }

        FlightId = flightId;
        StartedAt = telemetry.ObservedAt;
        InitialFuelKg = telemetry.FuelTotalKg;
        InitialPayloadKg = telemetry.PayloadWeightKg;
        _previousTelemetry = telemetry;
        Phase = FlightPhase.Started;
    }

    public string? Observe(TelemetrySnapshot telemetry)
    {
        if (Phase is not (FlightPhase.Started or FlightPhase.Airborne or FlightPhase.Landed))
        {
            return null;
        }

        var cancellationReason = ValidateActiveTelemetry(telemetry);
        if (cancellationReason is not null)
        {
            Phase = FlightPhase.Cancelled;
            return cancellationReason;
        }

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
        _previousTelemetry = telemetry;
        return null;
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

    public void ResetForNextFlight()
    {
        if (Phase is not FlightPhase.Finished)
        {
            throw new InvalidOperationException(
                "Only a finished flight session can be reset.");
        }

        FlightId = null;
        StartedAt = null;
        InitialFuelKg = null;
        InitialPayloadKg = null;
        _previousTelemetry = null;
        Phase = FlightPhase.Ready;
    }

    public void ResetCancelledFlight()
    {
        if (Phase is not FlightPhase.Cancelled)
        {
            throw new InvalidOperationException("The flight session is not cancelled.");
        }
        FlightId = null;
        StartedAt = null;
        InitialFuelKg = null;
        InitialPayloadKg = null;
        _previousTelemetry = null;
        Phase = FlightPhase.Ready;
    }

    private string? ValidateActiveTelemetry(TelemetrySnapshot telemetry)
    {
        if (_previousTelemetry is null || InitialFuelKg is null || InitialPayloadKg is null)
        {
            return null;
        }
        if (!AircraftIdentityIsUnchanged(_previousTelemetry, telemetry))
        {
            return "The simulator aircraft changed after the flight became active.";
        }
        if (telemetry.SlewActive || Math.Abs(telemetry.SimulationRate - 1d) > 0.01)
        {
            return "Slew mode or a simulation rate other than 1× was detected.";
        }
        if (telemetry.FuelTotalKg > InitialFuelKg.Value + ChangeTolerance(InitialFuelKg.Value))
        {
            return "Fuel was increased after the flight became active.";
        }
        if (Math.Abs(telemetry.PayloadWeightKg - InitialPayloadKg.Value)
            > ChangeTolerance(InitialPayloadKg.Value))
        {
            return "The aircraft payload changed after the flight became active.";
        }

        var elapsed = telemetry.ObservedAt - _previousTelemetry.ObservedAt;
        var seconds = Math.Clamp(elapsed.TotalSeconds, 0, 30);
        var plausibleDistance = Math.Max(
            5,
            Math.Max(telemetry.GroundSpeedKnots, _previousTelemetry.GroundSpeedKnots)
                * seconds / 3600d * 2d + 2d);
        var actualDistance = DistanceNauticalMiles(
            _previousTelemetry.LatitudeDegrees,
            _previousTelemetry.LongitudeDegrees,
            telemetry.LatitudeDegrees,
            telemetry.LongitudeDegrees);
        if (actualDistance > plausibleDistance)
        {
            return "The simulator session was left or the aircraft position changed discontinuously.";
        }
        return null;
    }

    private static bool AircraftIdentityIsUnchanged(
        TelemetrySnapshot previous,
        TelemetrySnapshot current) =>
        NormalizeAircraftIdentifier(previous.AircraftAtcModel)
            == NormalizeAircraftIdentifier(current.AircraftAtcModel)
        && NormalizeAircraftIdentifier(previous.AircraftTitle)
            == NormalizeAircraftIdentifier(current.AircraftTitle);

    private static bool FuelMatches(double? target, double actual) =>
        target is null || WithinOnePercent(target.Value, actual);

    private static bool WithinOnePercent(double target, double actual) =>
        Math.Abs(actual - target) <= LoadTolerance(target);

    private static double LoadTolerance(double target) => Math.Max(1d, Math.Abs(target) * 0.01d);

    private static double ChangeTolerance(double target) => Math.Max(0.5d, Math.Abs(target) * 0.001d);

    internal static bool AircraftMatches(
        ContractAssignment contract,
        TelemetrySnapshot telemetry)
    {
        if (contract.AircraftSimulatorIdentities.Any(identity =>
                SimulatorIdentityMatches(identity, telemetry)))
        {
            return true;
        }

        var expectedIcao = NormalizeAircraftIdentifier(contract.AircraftIcao);
        if (expectedIcao.Length >= 3)
        {
            var atcModel = NormalizeAircraftIdentifier(
                SimulatorAircraftIdentity.DecodeAtcModel(telemetry.AircraftAtcModel));
            if (atcModel == expectedIcao || atcModel.StartsWith(expectedIcao, StringComparison.Ordinal))
            {
                return true;
            }

            var titleIdentifier = NormalizeAircraftIdentifier(telemetry.AircraftTitle);
            if (titleIdentifier.Contains(expectedIcao, StringComparison.Ordinal))
            {
                return true;
            }

            if (KnownAircraftFamilyMatches(expectedIcao, telemetry))
            {
                return true;
            }
        }

        return AircraftNamesMatch(
            contract.RequiredAircraftTitleContains ?? string.Empty,
            telemetry.AircraftTitle);
    }

    private static bool KnownAircraftFamilyMatches(
        string expectedIcao,
        TelemetrySnapshot telemetry)
    {
        if (expectedIcao != "A20N")
        {
            return false;
        }

        var atcType = NormalizeAircraftIdentifier(
            SimulatorAircraftIdentity.DecodeAtcType(telemetry.AircraftAtcType));
        var atcModel = NormalizeAircraftIdentifier(
            SimulatorAircraftIdentity.DecodeAtcModel(telemetry.AircraftAtcModel));
        var title = NormalizeAircraftIdentifier(telemetry.AircraftTitle);
        var isFlyByWireFamily =
            title.StartsWith("FBW", StringComparison.Ordinal)
            || title.StartsWith("FWB", StringComparison.Ordinal)
            || title.Contains("FLYBYWIRE", StringComparison.Ordinal);

        return atcType.Contains("AIRBUS", StringComparison.Ordinal)
            && atcModel.StartsWith("A320", StringComparison.Ordinal)
            && isFlyByWireFamily;
    }

    private static bool SimulatorIdentityMatches(
        AircraftSimulatorIdentity identity,
        TelemetrySnapshot telemetry)
    {
        var candidate = identity.IdentityField switch
        {
            "atc_model" => SimulatorAircraftIdentity.DecodeAtcModel(
                telemetry.AircraftAtcModel),
            "title" => telemetry.AircraftTitle,
            _ => string.Empty,
        };
        var normalizedCandidate = NormalizeAircraftIdentifier(candidate);
        var expected = NormalizeAircraftIdentifier(identity.MatchValue);
        if (normalizedCandidate.Length == 0 || expected.Length == 0)
        {
            return false;
        }

        return identity.MatchMode switch
        {
            "exact" => normalizedCandidate == expected,
            "prefix" => normalizedCandidate.StartsWith(expected, StringComparison.Ordinal),
            "contains" => normalizedCandidate.Contains(expected, StringComparison.Ordinal),
            _ => false,
        };
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
