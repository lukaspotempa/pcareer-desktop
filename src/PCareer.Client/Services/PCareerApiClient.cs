using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using PCareer.Client.Models;

namespace PCareer.Client.Services;

public sealed class PCareerApiClient : IFlightServerClient, IDisposable
{
    private static readonly TimeSpan TelemetryInterval = TimeSpan.FromSeconds(5);
    private readonly HttpClient _http;
    private readonly JsonSerializerOptions _json = new(JsonSerializerDefaults.Web)
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
    };
    private readonly SemaphoreSlim _refreshLock = new(1, 1);
    private DesktopSession? _session;
    private DateTimeOffset _lastTelemetryQueuedAt = DateTimeOffset.MinValue;
    private int _telemetryUploadInProgress;

    public PCareerApiClient(Uri serverBaseUri)
    {
        _http = new HttpClient
        {
            BaseAddress = serverBaseUri,
            Timeout = TimeSpan.FromSeconds(20),
        };
    }

    public event EventHandler<string>? TelemetryStatusChanged;

    public DesktopSession? Session => _session;

    public async Task<DesktopLoginRequest> BeginDiscordLoginAsync(
        CancellationToken cancellationToken = default)
    {
        using var response = await _http.PostAsync(
            "api/auth/desktop/start",
            content: null,
            cancellationToken);
        var body = await ReadRequiredAsync<LoginStartDto>(response, cancellationToken);
        return new DesktopLoginRequest(
            body.RequestId,
            body.PollToken,
            new Uri(body.AuthorizationUrl),
            body.ExpiresAt,
            Math.Max(1, body.PollIntervalSeconds));
    }

    public async Task<DesktopSession?> PollDiscordLoginAsync(
        DesktopLoginRequest login,
        CancellationToken cancellationToken = default)
    {
        using var response = await _http.PostAsJsonAsync(
            "api/auth/desktop/poll",
            new { request_id = login.RequestId, poll_token = login.PollToken },
            _json,
            cancellationToken);
        if (response.StatusCode == HttpStatusCode.Accepted)
        {
            return null;
        }

        var body = await ReadRequiredAsync<SessionDto>(response, cancellationToken);
        _session = ToSession(body);
        return _session;
    }

    public async Task<ContractAssignment?> GetActiveContractAsync(
        CancellationToken cancellationToken = default)
    {
        using var response = await SendAuthenticatedAsync(
            () => new HttpRequestMessage(HttpMethod.Get, "api/contracts"),
            cancellationToken);
        var contracts = await ReadRequiredAsync<List<ContractDto>>(response, cancellationToken);
        var active = contracts.FirstOrDefault(contract => contract.Status == "active");
        if (active is null)
        {
            return null;
        }

        return new ContractAssignment(
            active.ContractId,
            active.StartAirport.Name,
            active.EndAirport.Name,
            active.Aircraft,
            active.StartAirport.Latitude,
            active.StartAirport.Longitude,
            2)
        {
            DepartureCode = active.StartAirport.Icao,
            ArrivalCode = active.EndAirport.Icao,
            AircraftIcao = active.AircraftIcao,
        };
    }

    public async Task<Guid> StartFlightAsync(
        ContractAssignment contract,
        TelemetrySnapshot initialTelemetry,
        CancellationToken cancellationToken = default)
    {
        using var response = await SendAuthenticatedAsync(
            () => JsonRequest(
                HttpMethod.Post,
                "api/desktop/flights/start",
                new { contract_id = contract.ContractId, telemetry = initialTelemetry }),
            cancellationToken);
        var flight = await ReadRequiredAsync<FlightDto>(response, cancellationToken);
        _lastTelemetryQueuedAt = DateTimeOffset.UtcNow;
        TelemetryStatusChanged?.Invoke(this, "Initial telemetry accepted by server.");
        return Guid.Parse(flight.FlightId);
    }

    public void QueueTelemetry(Guid flightId, TelemetrySnapshot telemetry)
    {
        var now = DateTimeOffset.UtcNow;
        if (now - _lastTelemetryQueuedAt < TelemetryInterval
            || Interlocked.CompareExchange(ref _telemetryUploadInProgress, 1, 0) != 0)
        {
            return;
        }

        _lastTelemetryQueuedAt = now;
        _ = UploadTelemetryAsync(flightId, telemetry);
    }

    public async Task FinishFlightAsync(
        Guid flightId,
        TelemetrySnapshot finalTelemetry,
        CancellationToken cancellationToken = default)
    {
        using var response = await SendAuthenticatedAsync(
            () => JsonRequest(
                HttpMethod.Post,
                $"api/desktop/flights/{flightId}/finish",
                finalTelemetry),
            cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);
        TelemetryStatusChanged?.Invoke(this, "Flight completed and confirmed by server.");
    }

    public async Task LogoutAsync(CancellationToken cancellationToken = default)
    {
        if (_session is null)
        {
            return;
        }

        try
        {
            using var response = await _http.PostAsJsonAsync(
                "api/auth/desktop/logout",
                new { refresh_token = _session.RefreshToken },
                _json,
                cancellationToken);
        }
        finally
        {
            _session = null;
        }
    }

    private async Task UploadTelemetryAsync(Guid flightId, TelemetrySnapshot telemetry)
    {
        try
        {
            using var response = await SendAuthenticatedAsync(
                () => JsonRequest(
                    HttpMethod.Post,
                    $"api/desktop/flights/{flightId}/telemetry",
                    telemetry),
                CancellationToken.None);
            await EnsureSuccessAsync(response, CancellationToken.None);
            TelemetryStatusChanged?.Invoke(
                this,
                $"Telemetry sent {DateTimeOffset.Now:HH:mm:ss} · next ping in 5 seconds.");
        }
        catch (Exception exception)
        {
            TelemetryStatusChanged?.Invoke(this, $"Telemetry upload failed: {exception.Message}");
        }
        finally
        {
            Interlocked.Exchange(ref _telemetryUploadInProgress, 0);
        }
    }

    private async Task<HttpResponseMessage> SendAuthenticatedAsync(
        Func<HttpRequestMessage> requestFactory,
        CancellationToken cancellationToken)
    {
        await EnsureFreshAccessTokenAsync(cancellationToken);
        var response = await SendOnceAsync(requestFactory, cancellationToken);
        if (response.StatusCode != HttpStatusCode.Unauthorized)
        {
            return response;
        }

        response.Dispose();
        await RefreshAsync(cancellationToken, force: true);
        return await SendOnceAsync(requestFactory, cancellationToken);
    }

    private async Task<HttpResponseMessage> SendOnceAsync(
        Func<HttpRequestMessage> requestFactory,
        CancellationToken cancellationToken)
    {
        if (_session is null)
        {
            throw new InvalidOperationException("Discord login is required.");
        }

        var request = requestFactory();
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _session.AccessToken);
        return await _http.SendAsync(request, cancellationToken);
    }

    private async Task EnsureFreshAccessTokenAsync(CancellationToken cancellationToken)
    {
        if (_session is null)
        {
            throw new InvalidOperationException("Discord login is required.");
        }
        if (_session.AccessExpiresAt > DateTimeOffset.UtcNow.AddMinutes(1))
        {
            return;
        }
        await RefreshAsync(cancellationToken, force: false);
    }

    private async Task RefreshAsync(CancellationToken cancellationToken, bool force)
    {
        await _refreshLock.WaitAsync(cancellationToken);
        try
        {
            if (_session is null)
            {
                throw new InvalidOperationException("Discord login is required.");
            }
            if (!force && _session.AccessExpiresAt > DateTimeOffset.UtcNow.AddMinutes(1))
            {
                return;
            }

            using var response = await _http.PostAsJsonAsync(
                "api/auth/desktop/refresh",
                new { refresh_token = _session.RefreshToken },
                _json,
                cancellationToken);
            var body = await ReadRequiredAsync<SessionDto>(response, cancellationToken);
            _session = ToSession(body);
        }
        finally
        {
            _refreshLock.Release();
        }
    }

    private HttpRequestMessage JsonRequest(HttpMethod method, string path, object value) =>
        new(method, path) { Content = JsonContent.Create(value, options: _json) };

    private async Task<T> ReadRequiredAsync<T>(
        HttpResponseMessage response,
        CancellationToken cancellationToken)
    {
        await EnsureSuccessAsync(response, cancellationToken);
        return await response.Content.ReadFromJsonAsync<T>(_json, cancellationToken)
            ?? throw new InvalidOperationException("The server returned an empty response.");
    }

    private static async Task EnsureSuccessAsync(
        HttpResponseMessage response,
        CancellationToken cancellationToken)
    {
        if (response.IsSuccessStatusCode)
        {
            return;
        }

        string detail;
        try
        {
            var error = await response.Content.ReadFromJsonAsync<ApiError>(cancellationToken);
            detail = error?.Detail ?? response.ReasonPhrase ?? "Server request failed";
        }
        catch
        {
            detail = response.ReasonPhrase ?? "Server request failed";
        }
        throw new HttpRequestException(detail, null, response.StatusCode);
    }

    private static DesktopSession ToSession(SessionDto body) => new()
    {
        AccessToken = body.AccessToken,
        AccessExpiresAt = body.AccessExpiresAt,
        RefreshToken = body.RefreshToken,
        RefreshExpiresAt = body.RefreshExpiresAt,
        User = new AuthenticatedUser(
            body.User.Id,
            body.User.DiscordId,
            body.User.Username,
            body.User.DisplayName,
            body.User.AvatarUrl),
    };

    public void Dispose()
    {
        _refreshLock.Dispose();
        _http.Dispose();
    }

    private sealed record LoginStartDto(
        string RequestId,
        string PollToken,
        string AuthorizationUrl,
        DateTimeOffset ExpiresAt,
        int PollIntervalSeconds);

    private sealed record SessionDto(
        string AccessToken,
        DateTimeOffset AccessExpiresAt,
        string RefreshToken,
        DateTimeOffset RefreshExpiresAt,
        UserDto User);

    private sealed record UserDto(
        int Id,
        string DiscordId,
        string Username,
        string DisplayName,
        string? AvatarUrl);

    private sealed record AirportDto(
        string Icao,
        string Name,
        double Latitude,
        double Longitude);

    private sealed record ContractDto(
        string ContractId,
        string Status,
        string Aircraft,
        string AircraftIcao,
        AirportDto StartAirport,
        AirportDto EndAirport);

    private sealed record FlightDto(string FlightId);

    private sealed record ApiError(string Detail);
}
