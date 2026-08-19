namespace PCareer.Client.Models;

public sealed record ContractAssignment(
    string ContractId,
    string DepartureName,
    string ArrivalName,
    string? RequiredAircraftTitleContains,
    double? DepartureLatitudeDegrees,
    double? DepartureLongitudeDegrees,
    double DepartureRadiusNauticalMiles)
{
    public string DepartureCode { get; init; } = string.Empty;

    public string ArrivalCode { get; init; } = string.Empty;

    public string AircraftIcao { get; init; } = string.Empty;

    public string RouteDisplay =>
        string.IsNullOrWhiteSpace(DepartureCode) || string.IsNullOrWhiteSpace(ArrivalCode)
            ? $"{DepartureName} → {ArrivalName}"
            : $"{DepartureCode} → {ArrivalCode}";

    public static ContractAssignment DevelopmentFlight { get; } = new(
        ContractId: "DEV-LOCAL-001",
        DepartureName: "Any airport",
        ArrivalName: "Any airport",
        RequiredAircraftTitleContains: null,
        DepartureLatitudeDegrees: null,
        DepartureLongitudeDegrees: null,
        DepartureRadiusNauticalMiles: 2);

    public string RequiredAircraftDisplay =>
        string.IsNullOrWhiteSpace(RequiredAircraftTitleContains)
            ? "Any aircraft"
            : RequiredAircraftTitleContains;
}
