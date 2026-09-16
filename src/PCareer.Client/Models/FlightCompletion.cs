namespace PCareer.Client.Models;

public sealed record FlightCompletion(
    Guid FlightId,
    string Callsign,
    string OriginCode,
    string OriginName,
    string DestinationCode,
    string DestinationName,
    string Aircraft,
    string Registration,
    TimeSpan Duration,
    double DistanceNauticalMiles,
    double? LandingRateFeetPerMinute,
    double? LandingGForce,
    double? LandingQualityScore,
    double LandingPenaltyPercent,
    long GrossRevenueCents,
    long LandingPenaltyCents,
    long RevenueCents);
