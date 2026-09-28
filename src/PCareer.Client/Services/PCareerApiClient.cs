using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text.Json;
using PCareer.Client.Models;

namespace PCareer.Client.Services;

public sealed class PCareerApiClient : IFlightServerClient, IMissionClient, IDisposable
{
    private static readonly TimeSpan TelemetryInterval = TimeSpan.FromSeconds(5);
    private readonly HttpClient _http;
    private readonly JsonSerializerOptions _json = new(JsonSerializerDefaults.Web)
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
    };
    private readonly SemaphoreSlim _refreshLock = new(1, 1);
    private readonly DesktopSessionStore _sessionStore;
    private DesktopSession? _session;
    private DateTimeOffset? _sessionPersistenceDeadline;
    private DateTimeOffset _lastTelemetryQueuedAt = DateTimeOffset.MinValue;
    private string? _lastTelemetryRejection;
    private int _telemetryUploadInProgress;

    public PCareerApiClient(Uri serverBaseUri, DesktopSessionStore? sessionStore = null)
    {
        _sessionStore = sessionStore ?? new DesktopSessionStore();
        _http = new HttpClient
        {
            BaseAddress = serverBaseUri,
            Timeout = TimeSpan.FromSeconds(20),
        };
    }

    public event EventHandler<string>? TelemetryStatusChanged;

    public event EventHandler<TelemetryRejectedEventArgs>? TelemetryRejected;

    public DesktopSession? Session => _session;

    public async Task<DesktopSession?> RestoreSessionAsync(
        CancellationToken cancellationToken = default)
    {
        var persisted = _sessionStore.Load();
        if (persisted is null)
        {
            return null;
        }

        _session = persisted.Session;
        _sessionPersistenceDeadline = persisted.PersistUntil;
        try
        {
            await EnsureFreshAccessTokenAsync(cancellationToken);
            return _session;
        }
        catch (HttpRequestException exception)
            when (exception.StatusCode == HttpStatusCode.Unauthorized)
        {
            ClearSession();
            return null;
        }
        catch
        {
            _session = null;
            _sessionPersistenceDeadline = null;
            return null;
        }
    }

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
        PersistSession();
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
            AircraftSimulatorIdentities = (active.AircraftSimulatorIdentities ?? [])
                .Where(identity => identity.Simulator == "msfs_2024")
                .Select(identity => new AircraftSimulatorIdentity(
                    identity.Simulator,
                    identity.IdentityField,
                    identity.MatchMode,
                    identity.MatchValue))
                .ToArray(),
            AirlineIcao = active.AirlineIcao ?? "PCX",
            FlightNumber = active.FlightNumber,
            ContractType = active.ContractType,
            RequiredFuelKg = active.RequiredFuelKg,
            RequiredPayloadKg = active.Payloads?.Sum(payload => payload.WeightKg) ?? 0,
        };
    }

    public async Task<AircraftTransmissionResult> TransmitAircraftAsync(
        AircraftSnapshot snapshot,
        CancellationToken cancellationToken = default)
    {
        using var response = await SendAuthenticatedAsync(
            () => JsonRequest(HttpMethod.Post, "api/desktop/aircraft/transmit", snapshot),
            cancellationToken);
        var body = await ReadRequiredAsync<TransmissionDto>(response, cancellationToken);
        return new AircraftTransmissionResult(
            body.Status,
            body.ModelCode,
            body.ModelDisplayName,
            body.IcaoTypeDesignator);
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
                new
                {
                    contract_id = contract.ContractId,
                    telemetry = NormalizeTelemetryForServer(initialTelemetry),
                }),
            cancellationToken);
        var flight = await ReadRequiredAsync<FlightDto>(response, cancellationToken);
        _lastTelemetryQueuedAt = DateTimeOffset.UtcNow;
        TelemetryStatusChanged?.Invoke(this, "Initial telemetry accepted by server.");
        return Guid.Parse(flight.FlightId);
    }

    public async Task<ActiveFlightSession?> GetActiveFlightAsync(
        CancellationToken cancellationToken = default)
    {
        using var response = await SendAuthenticatedAsync(
            () => new HttpRequestMessage(HttpMethod.Get, "api/desktop/flights/active"),
            cancellationToken);
        if (response.StatusCode == HttpStatusCode.NoContent)
        {
            return null;
        }

        var flight = await ReadRequiredAsync<FlightDto>(response, cancellationToken);
        return new ActiveFlightSession(
            Guid.Parse(flight.FlightId),
            flight.ContractId,
            flight.StartedAt,
            flight.HasAirborneTelemetry,
            flight.SimRateIncreased);
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

    public async Task<FlightCompletion> FinishFlightAsync(
        Guid flightId,
        TelemetrySnapshot finalTelemetry,
        CancellationToken cancellationToken = default)
    {
        using var response = await SendAuthenticatedAsync(
            () => JsonRequest(
                HttpMethod.Post,
                $"api/desktop/flights/{flightId}/finish",
                NormalizeTelemetryForServer(finalTelemetry)),
            cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);
        using var summaryResponse = await SendAuthenticatedAsync(
            () => new HttpRequestMessage(HttpMethod.Get, "api/flights?limit=1"),
            cancellationToken);
        var records = await ReadRequiredAsync<List<FlightRecordDto>>(
            summaryResponse,
            cancellationToken);
        var record = records.SingleOrDefault(item => item.FlightId == flightId.ToString())
            ?? throw new InvalidOperationException("The completed flight summary is unavailable.");
        TelemetryStatusChanged?.Invoke(this, "Flight completed and confirmed by server.");
        return new FlightCompletion(
            flightId, record.Callsign, record.OriginIcao, record.OriginName,
            record.DestinationIcao, record.DestinationName, record.Aircraft,
            record.AircraftRegistration, TimeSpan.FromSeconds(record.RealDurationSeconds),
            record.DistanceNm, record.LandingRateFpm, record.LandingGForce,
            record.LandingQualityScore, record.LandingPenaltyPercent,
            record.GrossRevenueCents, record.LandingPenaltyCents, record.RevenueCents);
    }

    public async Task CancelFlightAsync(
        Guid flightId,
        string reason,
        CancellationToken cancellationToken = default)
    {
        using var response = await SendAuthenticatedAsync(
            () => JsonRequest(
                HttpMethod.Post,
                $"api/desktop/flights/{flightId}/cancel",
                new { reason }),
            cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);
        TelemetryStatusChanged?.Invoke(this, $"Flight cancelled: {reason}");
    }

    public async Task<MissionState?> GetActiveMissionAsync(CancellationToken cancellationToken = default)
    {
        using var response = await SendAuthenticatedAsync(
            () => new HttpRequestMessage(HttpMethod.Get, "api/missions/active"),
            cancellationToken);
        if (response.StatusCode == HttpStatusCode.NoContent)
        {
            return null;
        }

        var body = await ReadRequiredAsync<MissionStateDto>(response, cancellationToken);
        return body.ToModel();
    }

    public async Task<MissionState> AdvanceMissionAsync(
        string contractId,
        string phaseId,
        CancellationToken cancellationToken = default)
    {
        using var response = await SendAuthenticatedAsync(
            () => JsonRequest(
                HttpMethod.Post,
                "api/missions/advance",
                new { contract_id = contractId, phase_id = phaseId }),
            cancellationToken);
        var body = await ReadRequiredAsync<MissionStateDto>(response, cancellationToken);
        return body.ToModel();
    }

    public async Task<MissionState> RestartMissionAsync(
        string contractId,
        CancellationToken cancellationToken = default)
    {
        using var response = await SendAuthenticatedAsync(
            () => JsonRequest(
                HttpMethod.Post,
                "api/missions/restart",
                new { contract_id = contractId }),
            cancellationToken);
        var body = await ReadRequiredAsync<MissionStateDto>(response, cancellationToken);
        return body.ToModel();
    }

    public async Task<MissionState> EvaluateMissionTriggerAsync(
        string contractId,
        TelemetrySnapshot telemetry,
        CancellationToken cancellationToken = default)
    {
        using var response = await SendAuthenticatedAsync(
            () => JsonRequest(
                HttpMethod.Post,
                "api/missions/evaluate",
                new
                {
                    contract_id = contractId,
                    latitude_degrees = telemetry.LatitudeDegrees,
                    longitude_degrees = telemetry.LongitudeDegrees,
                    altitude_ft = telemetry.AltitudeFeet,
                    on_ground = telemetry.OnGround,
                }),
            cancellationToken);
        var body = await ReadRequiredAsync<MissionStateDto>(response, cancellationToken);
        return body.ToModel();
    }

    public async Task<MissionCompleteResult> CompleteMissionAsync(
        string contractId,
        string completionType,
        CancellationToken cancellationToken = default)
    {
        using var response = await SendAuthenticatedAsync(
            () => JsonRequest(
                HttpMethod.Post,
                "api/missions/complete",
                new { contract_id = contractId, completion_type = completionType }),
            cancellationToken);
        var body = await ReadRequiredAsync<MissionCompleteResultDto>(response, cancellationToken);
        return body.ToModel();
    }

    public async Task LogoutAsync(CancellationToken cancellationToken = default)
    {
        if (_session is null)
        {
            _sessionStore.Clear();
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
            ClearSession();
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
                    NormalizeTelemetryForServer(telemetry)),
                CancellationToken.None);
            await EnsureSuccessAsync(response, CancellationToken.None);
            _lastTelemetryRejection = null;
            TelemetryStatusChanged?.Invoke(
                this,
                $"Telemetry sent {DateTimeOffset.Now:HH:mm:ss} · next ping in 5 seconds.");
        }
        catch (Exception exception)
        {
            TelemetryStatusChanged?.Invoke(this, $"Telemetry upload failed: {exception.Message}");
            if (exception is HttpRequestException
                {
                    StatusCode: HttpStatusCode.Conflict or HttpStatusCode.NotFound
                } requestException)
            {
                var rejectionKey = $"{requestException.StatusCode}:{requestException.Message}";
                if (!string.Equals(
                    rejectionKey,
                    _lastTelemetryRejection,
                    StringComparison.Ordinal))
                {
                    _lastTelemetryRejection = rejectionKey;
                    TelemetryRejected?.Invoke(
                        this,
                        new TelemetryRejectedEventArgs(
                            requestException.StatusCode.Value,
                            requestException.Message));
                }
            }
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
            try
            {
                var body = await ReadRequiredAsync<SessionDto>(response, cancellationToken);
                _session = ToSession(body);
                PersistSession(_sessionPersistenceDeadline);
            }
            catch (HttpRequestException exception)
                when (exception.StatusCode == HttpStatusCode.Unauthorized)
            {
                ClearSession();
                throw;
            }
        }
        finally
        {
            _refreshLock.Release();
        }
    }

    private HttpRequestMessage JsonRequest(HttpMethod method, string path, object value) =>
        new(method, path) { Content = JsonContent.Create(value, options: _json) };

    internal static TelemetrySnapshot NormalizeTelemetryForServer(
        TelemetrySnapshot telemetry)
    {
        const double poundsPerKilogram = 2.20462262185d;
        var normalizedEmptyWeightPounds = telemetry.TotalWeightPounds
            - (telemetry.FuelTotalKg + telemetry.PayloadWeightKg) * poundsPerKilogram;
        return double.IsFinite(normalizedEmptyWeightPounds)
            && normalizedEmptyWeightPounds >= 0
            ? telemetry with { EmptyWeightPounds = normalizedEmptyWeightPounds }
            : telemetry;
    }

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

    private void ClearSession()
    {
        _session = null;
        _sessionPersistenceDeadline = null;
        _sessionStore.Clear();
    }

    private void PersistSession(DateTimeOffset? existingDeadline = null)
    {
        if (_session is null)
        {
            return;
        }

        try
        {
            _sessionPersistenceDeadline = _sessionStore.Save(
                _session,
                existingDeadline);
        }
        catch (IOException)
        {
            _sessionPersistenceDeadline = existingDeadline;
        }
        catch (UnauthorizedAccessException)
        {
            _sessionPersistenceDeadline = existingDeadline;
        }
        catch (CryptographicException)
        {
            _sessionPersistenceDeadline = existingDeadline;
        }
    }

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
        string ContractType,
        string? AirlineIcao,
        string FlightNumber,
        string Aircraft,
        string AircraftIcao,
        List<AircraftIdentityDto>? AircraftSimulatorIdentities,
        AirportDto StartAirport,
        AirportDto EndAirport,
        double? RequiredFuelKg,
        List<PayloadDto>? Payloads);

    private sealed record PayloadDto(double WeightKg);

    private sealed record AircraftIdentityDto(
        string Simulator,
        string IdentityField,
        string MatchMode,
        string MatchValue);

    private sealed record FlightDto(
        string FlightId,
        string ContractId,
        DateTimeOffset StartedAt,
        bool HasAirborneTelemetry = false,
        bool SimRateIncreased = false);

    private sealed record FlightRecordDto(
        string FlightId,
        string Callsign,
        string OriginIcao,
        string OriginName,
        string DestinationIcao,
        string DestinationName,
        string Aircraft,
        string AircraftRegistration,
        int RealDurationSeconds,
        double DistanceNm,
        double? LandingRateFpm,
        double? LandingGForce,
        double? LandingQualityScore,
        double LandingPenaltyPercent,
        long GrossRevenueCents,
        long LandingPenaltyCents,
        long RevenueCents);

    private sealed record TransmissionDto(
        string Status,
        string ModelCode,
        string ModelDisplayName,
        string IcaoTypeDesignator);

    private sealed record MissionStateDto(
        int Id,
        int ScriptId,
        MissionScriptDto Script,
        string Status,
        string CurrentPhaseId,
        Dictionary<string, JsonElement>? PhaseData,
        List<GroundObjectSpecDto> SpawnedObjects,
        DateTimeOffset StartedAt,
        DateTimeOffset? CompletedAt,
        string? ContractId)
    {
        public MissionState ToModel() => new(
            Id,
            ScriptId,
            Script.ToModel(),
            Status,
            CurrentPhaseId,
            PhaseData,
            (SpawnedObjects ?? [])
                .Select(spec => spec.ToModel())
                .ToArray(),
            StartedAt,
            CompletedAt,
            ContractId);
    }

    private sealed record MissionScriptDto(
        int Id,
        string Code,
        string Name,
        string Description,
        string AircraftIcao,
        AirfieldRefDto PickupAirport,
        AirfieldRefDto DeliveryAirport,
        int EstimatedDurationMin,
        List<GroundObjectSpecDto>? GroundObjects,
        List<MissionPhaseDto>? MissionSequence,
        List<MissionDialogLineDto> DialogLines,
        string? WeatherPreset,
        int BaseRewardCents,
        int CancellationFeeCents,
        bool IsActive)
    {
        public MissionScriptData ToModel() => new(
            Id,
            Code,
            Name,
            Description,
            AircraftIcao,
            PickupAirport.ToModel(),
            DeliveryAirport.ToModel(),
            EstimatedDurationMin,
            (GroundObjects ?? [])
                .Select(spec => spec.ToModel())
                .ToArray(),
            (MissionSequence ?? [])
                .Select(phase => phase.ToModel())
                .ToArray(),
            (DialogLines ?? []).Select(line => line.ToModel()).ToArray(),
            WeatherPreset,
            BaseRewardCents,
            CancellationFeeCents,
            IsActive);
    }

    private sealed record AirfieldRefDto(
        int Id,
        string Icao,
        string Name,
        double Latitude,
        double Longitude)
    {
        public MissionAirfield ToModel() => new(Id, Icao, Name, Latitude, Longitude);
    }

    private sealed record GroundObjectSpecDto(
        string Id,
        string ObjectType,
        string Title,
        GroundObjectPositionDto Position,
        double HeadingDegrees,
        bool Freeze,
        string? LiveryName)
    {
        public MissionGroundObject ToModel() =>
            new(Id, ObjectType, Title, Position.ToModel(), HeadingDegrees, Freeze, LiveryName);
    }

    private sealed record GroundObjectPositionDto(double Lat, double Lon, double AltM)
    {
        public MissionPosition ToModel() => new(Lat, Lon, AltM);
    }

    private sealed record MissionPhaseDto(
        string Id,
        string Label,
        string? Action,
        List<MissionWaypointDto>? Waypoints,
        MissionTriggerDto? Trigger,
        double? DurationSec,
        string? DialogId,
        MissionCameraDto? Camera,
        MissionTeleportDto? DebugTeleport,
        List<string> ObjectIds)
    {
        public MissionPhase ToModel() => new(
            Id,
            Label,
            Action,
            (Waypoints ?? [])
                .Select(waypoint => waypoint.ToModel())
                .ToArray(),
            Trigger?.ToModel(),
            DurationSec,
            DialogId,
            Camera?.ToModel(),
            DebugTeleport?.ToModel(),
            ObjectIds ?? []);
    }

    private sealed record MissionWaypointDto(double Lat, double Lon)
    {
        public MissionWaypoint ToModel() => new(Lat, Lon);
    }

    private sealed record MissionTriggerDto(
        string Type,
        double? Lat,
        double? Lon,
        double? RadiusM,
        double? MinAltitudeFt,
        bool? OnGround,
        double? ElapsedSec)
    {
        public MissionTrigger ToModel() =>
            new(Type, Lat, Lon, RadiusM, MinAltitudeFt, OnGround, ElapsedSec);
    }

    private sealed record MissionCameraDto(
        string? TargetObjectId,
        double OffsetMX,
        double OffsetMY,
        double OffsetMZ,
        double? FovDegrees,
        double TransitionSec)
    {
        public MissionCamera ToModel() =>
            new(TargetObjectId, OffsetMX, OffsetMY, OffsetMZ, FovDegrees, TransitionSec);
    }

    private sealed record MissionTeleportDto(
        double Lat,
        double Lon,
        double AltitudeFt,
        double HeadingDegrees,
        bool OnGround)
    {
        public MissionTeleport ToModel() =>
            new(Lat, Lon, AltitudeFt, HeadingDegrees, OnGround);
    }

    private sealed record MissionDialogLineDto(
        string Id,
        string Text,
        string? Speaker,
        string? AudioHint)
    {
        public MissionDialogLine ToModel() => new(Id, Text, Speaker, AudioHint);
    }

    private sealed record MissionCompleteResultDto(
        int MissionStateId,
        string Status,
        int RewardCents,
        string Message)
    {
        public MissionCompleteResult ToModel() =>
            new(MissionStateId, Status, RewardCents, Message);
    }

    private sealed record ApiError(string Detail);
}
