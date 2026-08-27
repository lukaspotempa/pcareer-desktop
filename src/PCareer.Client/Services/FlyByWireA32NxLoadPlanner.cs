namespace PCareer.Client.Services;

internal sealed record FlyByWirePayloadPlan(
    int[] PassengersByZone,
    double[] CargoKilogramsByHold);

internal sealed record FlyByWireFuelPlan(
    double TotalGallons,
    double CenterGallons,
    double LeftInnerGallons,
    double LeftOuterGallons,
    double RightInnerGallons,
    double RightOuterGallons);

internal static class FlyByWireA32NxLoadPlanner
{
    public const double PassengerWeightKilograms = 84d;
    public const int MaximumPassengers = 174;
    public const double MaximumCargoKilograms = 9_435d;
    public const double MaximumFuelGallons = 6_267d;

    private static readonly int[] PassengerZoneCapacities = [36, 42, 48, 48];
    private static readonly double[] CargoHoldCapacitiesKilograms = [3_402d, 2_426d, 2_110d, 1_497d];

    public static FlyByWirePayloadPlan CreatePayloadPlan(double payloadKilograms)
    {
        if (!double.IsFinite(payloadKilograms) || payloadKilograms < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(payloadKilograms));
        }

        var maximumPayload =
            MaximumPassengers * PassengerWeightKilograms + MaximumCargoKilograms;
        if (payloadKilograms > maximumPayload + 0.5d)
        {
            throw new InvalidOperationException(
                $"The requested {payloadKilograms:0.0} kg exceeds the FlyByWire A320neo's "
                + $"{maximumPayload:0.0} kg supported passenger and cargo load.");
        }

        var minimumPassengersForCargoCapacity = (int)Math.Ceiling(
            Math.Max(0d, payloadKilograms - MaximumCargoKilograms)
            / PassengerWeightKilograms);
        var balancedPassengers = (int)Math.Floor(
            payloadKilograms / (PassengerWeightKilograms + 20d));
        var passengerCount = Math.Clamp(
            Math.Max(minimumPassengersForCargoCapacity, balancedPassengers),
            0,
            MaximumPassengers);
        var cargoKilograms = payloadKilograms - passengerCount * PassengerWeightKilograms;

        var passengers = new int[PassengerZoneCapacities.Length];
        var passengersRemaining = passengerCount;
        for (var zone = PassengerZoneCapacities.Length - 1; zone > 0; zone--)
        {
            var ratio = Math.Ceiling(
                PassengerZoneCapacities[zone] / (double)MaximumPassengers * 100d) / 100d;
            passengers[zone] = Math.Min(
                (int)Math.Truncate(ratio * passengerCount),
                PassengerZoneCapacities[zone]);
            passengersRemaining -= passengers[zone];
        }
        passengers[0] = Math.Min(passengersRemaining, PassengerZoneCapacities[0]);

        var cargo = new double[CargoHoldCapacitiesKilograms.Length];
        var cargoRemaining = cargoKilograms;
        for (var hold = CargoHoldCapacitiesKilograms.Length - 1; hold > 0; hold--)
        {
            cargo[hold] = Math.Round(
                CargoHoldCapacitiesKilograms[hold] / MaximumCargoKilograms * cargoKilograms);
            cargoRemaining -= cargo[hold];
        }
        cargo[0] = cargoRemaining;

        return new FlyByWirePayloadPlan(passengers, cargo);
    }

    public static FlyByWireFuelPlan CreateFuelPlan(
        double fuelKilograms,
        double fuelWeightPerGallonPounds)
    {
        if (!double.IsFinite(fuelKilograms) || fuelKilograms < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(fuelKilograms));
        }
        if (!double.IsFinite(fuelWeightPerGallonPounds) || fuelWeightPerGallonPounds <= 0)
        {
            throw new InvalidOperationException("The aircraft did not report a usable fuel density.");
        }

        const double kilogramsPerPound = 0.45359237d;
        var totalGallons = fuelKilograms / fuelWeightPerGallonPounds / kilogramsPerPound;
        if (totalGallons > MaximumFuelGallons + 0.5d)
        {
            var maximumKilograms =
                MaximumFuelGallons * fuelWeightPerGallonPounds * kilogramsPerPound;
            throw new InvalidOperationException(
                $"The requested {fuelKilograms:0.0} kg exceeds the FlyByWire A320neo's "
                + $"{maximumKilograms:0.0} kg fuel capacity.");
        }

        const double outerCellGallons = 228d;
        const double innerCellGallons = 1_816d;
        var remaining = totalGallons - outerCellGallons * 2d;
        var outer = (outerCellGallons * 2d + Math.Min(remaining, 0d)) / 2d;
        remaining = Math.Max(remaining, 0d) - innerCellGallons * 2d;
        var inner = (innerCellGallons * 2d + Math.Min(remaining, 0d)) / 2d;
        var center = Math.Max(remaining, 0d);

        return new FlyByWireFuelPlan(
            totalGallons,
            center,
            inner,
            outer,
            inner,
            outer);
    }
}
