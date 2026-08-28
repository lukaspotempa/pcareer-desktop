using System.Net;

namespace PCareer.Client.Models;

public sealed class TelemetryRejectedEventArgs(
    HttpStatusCode statusCode,
    string message) : EventArgs
{
    public HttpStatusCode StatusCode { get; } = statusCode;

    public string Message { get; } = message;
}
