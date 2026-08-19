namespace PCareer.Client.Models;

public sealed record AuthenticatedUser(
    int Id,
    string DiscordId,
    string Username,
    string DisplayName,
    string? AvatarUrl);

public sealed class DesktopSession
{
    public required string AccessToken { get; set; }

    public required DateTimeOffset AccessExpiresAt { get; set; }

    public required string RefreshToken { get; set; }

    public required DateTimeOffset RefreshExpiresAt { get; set; }

    public required AuthenticatedUser User { get; set; }
}

public sealed record DesktopLoginRequest(
    string RequestId,
    string PollToken,
    Uri AuthorizationUrl,
    DateTimeOffset ExpiresAt,
    int PollIntervalSeconds);
