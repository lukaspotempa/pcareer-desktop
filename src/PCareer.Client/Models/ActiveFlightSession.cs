namespace PCareer.Client.Models;

public sealed record ActiveFlightSession(
    Guid FlightId,
    string ContractId,
    DateTimeOffset StartedAt,
    bool HasAirborneTelemetry);
