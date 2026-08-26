namespace PCareer.Client.Models;

public sealed record AircraftSnapshot(
    DateTimeOffset ObservedAt,
    string AircraftTitle,
    string AircraftAtcType,
    string AircraftAtcModel);
