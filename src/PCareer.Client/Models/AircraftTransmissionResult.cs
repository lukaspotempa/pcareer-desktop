namespace PCareer.Client.Models;

public sealed record AircraftTransmissionResult(
    string Status,
    string ModelCode,
    string ModelDisplayName,
    string IcaoTypeDesignator)
{
    public bool Created => Status == "created";
}
