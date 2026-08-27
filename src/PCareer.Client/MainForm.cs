using System.Text.Json;
using Microsoft.Web.WebView2.WinForms;
using PCareer.Client.Models;
using PCareer.Client.Services;

namespace PCareer.Client;

public sealed class MainForm : Form
{
    private const int SimConnectMessageId = 0x0402;

    private readonly ISimulatorConnection _simulator = SimulatorConnectionFactory.Create();
    private readonly PCareerApiClient _serverClient;
    private readonly DesktopSession _session;
    private readonly FlightSessionController _flight = new();
    private readonly System.Windows.Forms.Timer _retryTimer = new() { Interval = 2000 };

    private readonly WebView2 _web = new() { Dock = DockStyle.Fill };

    private TelemetrySnapshot? _latestTelemetry;
    private ContractAssignment? _contract;
    private int _activationInProgress;
    private int _cancellationInProgress;

    public MainForm(PCareerApiClient serverClient, DesktopSession session)
    {
        _serverClient = serverClient;
        _session = session;
        Text = "Virtual Pilot Network";
        Icon = BrandAssets.ApplicationIcon;
        StartPosition = FormStartPosition.CenterScreen;
        MinimumSize = new Size(520, 700);
        Size = new Size(700, 730);
        BackColor = Palette.AppBackground;

        Controls.Add(_web);

        _web.WebMessageReceived += (_, e) => HandleWebMessage(e.WebMessageAsJson);

        _startButton.Click += StartFlightClicked;
        _finishButton.Click += FinishFlightClicked;
        _retryTimer.Tick += (_, _) => TryConnect();
        _simulator.ConnectionChanged += (_, _) => UpdateConnectionState();
        _simulator.TelemetryReceived += (_, telemetry) => ReceiveTelemetry(telemetry);
        _simulator.AircraftIdentityReceived += (_, snapshot) =>
            BeginInvoke(() => _ = UploadAircraftSnapshotAsync(snapshot));
        _serverClient.TelemetryStatusChanged += ServerTelemetryStatusChanged;
        FormClosed += (_, _) => _simulator.Dispose();

        _finishButton.Enabled = false;
        _startButton.Enabled = false;
    }

    // Hidden controls kept for logic compatibility
    private readonly Label _userLabel = new();
    private readonly Label _connectionLabel = new();
    private readonly Label _contractLabel = new();
    private readonly Label _aircraftLabel = new();
    private readonly Label _positionLabel = new();
    private readonly Label _altitudeLabel = new();
    private readonly Label _speedLabel = new();
    private readonly Label _verticalSpeedLabel = new();
    private readonly Label _headingLabel = new();
    private readonly Label _groundLabel = new();
    private readonly Label _attitudeLabel = new();
    private readonly Label _systemsLabel = new();
    private readonly Label _telemetryServerLabel = new();
    private readonly Label _readinessLabel = new();
    private readonly Label _flightStatusLabel = new();
    private readonly Button _startButton = new() { Enabled = false };
    private readonly Button _finishButton = new() { Enabled = false };
    private readonly Button _transmitButton = new() { Enabled = false };

    protected override async void OnShown(EventArgs e)
    {
        base.OnShown(e);
        try
        {
            var environment = await WebViewRuntime.CreateEnvironmentAsync();
            await _web.EnsureCoreWebView2Async(environment);
            _web.CoreWebView2.NavigateToString(BrandAssets.AddLogoToHtml(HtmlContent.Template));
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "WebView2 initialization error",
                MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        TryConnect();
        _retryTimer.Start();
        _ = LoadActiveContractAsync();
    }

    protected override void WndProc(ref Message message)
    {
        if (message.Msg == SimConnectMessageId)
        {
            _simulator.ReceiveMessage();
        }
        base.WndProc(ref message);
    }

    // ── JS ↔ C# bridge ──────────────────────────────────────────────────

    private void HandleWebMessage(string json)
    {
        using var doc = JsonDocument.Parse(json);
        if (!doc.RootElement.TryGetProperty("action", out var actionProp))
            return;

        var action = actionProp.GetString();
        switch (action)
        {
            case "startFlight":
                StartFlightClicked(this, EventArgs.Empty);
                break;
            case "finishFlight":
                FinishFlightClicked(this, EventArgs.Empty);
                break;
            case "refreshContract":
                _ = LoadActiveContractAsync();
                break;
            case "loadPayload":
                LoadPayloadClicked();
                break;
            case "loadFuel":
                LoadFuelClicked();
                break;
#if DEBUG
            case "transmitAircraft":
                TransmitAircraftClicked(this, EventArgs.Empty);
                break;
#endif
        }
    }

    private void SendStateToJS()
    {
        if (_web.CoreWebView2 is null) return;

        var canApplyLoads = CanApplyLoads();

        var state = new
        {
            user = $"{_session.User.DisplayName}",

            connText = _simulator.StatusMessage,
            connDot = _simulator.IsConnected ? "ok" : "warn",

            contract = _contract is null
                ? "None | Accept a contract on the Virtual Pilot Network website, then refresh."
                : $"{_contract.FlightDesignator}  |  {_contract.RouteDisplay}  |  {_contract.RequiredAircraftDisplay}",

            aircraft = _latestTelemetry is null
                ? "--"
                : string.IsNullOrWhiteSpace(_latestTelemetry.AircraftAtcModel)
                    ? _latestTelemetry.AircraftTitle
                    : $"{_latestTelemetry.AircraftTitle}", //  »  ATC {_latestTelemetry.AircraftAtcType} {_latestTelemetry.AircraftAtcModel}",

            stateText = FlightStatusText(),
            stateDot = _flight.Phase switch
            {
                FlightPhase.Ready => "idle",
                FlightPhase.Loading => "warn",
                FlightPhase.Started => "info",
                FlightPhase.Airborne => "info",
                FlightPhase.Landed => "ok",
                FlightPhase.Finished => "ok",
                FlightPhase.Cancelled => "warn",
                _ => "idle",
            },

            readyText = _readinessLabel.Text,
            readyDot = _readinessLabel.ForeColor == Palette.StatusOk ? "ok" : "warn",

            startEnabled = _startButton.Enabled,
            finishEnabled = _finishButton.Enabled,
            loadPayloadEnabled = canApplyLoads,
            loadFuelEnabled = canApplyLoads && _contract?.RequiredFuelKg is not null,
            payloadButtonText = _contract is null
                ? "Load payload"
                : $"Load payload  ·  {_contract.RequiredPayloadKg:0} kg",
            fuelButtonText = _contract?.RequiredFuelKg is double fuel
                ? $"Load fuel  ·  {fuel:0} kg"
                : "Load fuel  ·  no target",
#if DEBUG
            developmentMode = true,
            transmitAircraftEnabled = _transmitButton.Enabled,
            position = _positionLabel.Text,
            altitude = _altitudeLabel.Text,
            speed = _speedLabel.Text,
            verticalSpeed = _verticalSpeedLabel.Text,
            heading = _headingLabel.Text,
            ground = _groundLabel.Text,
            attitude = _attitudeLabel.Text,
            systems = _systemsLabel.Text,
            telemetryServer = _telemetryServerLabel.Text,
#else
            developmentMode = false,
#endif
        };

        _web.CoreWebView2.PostWebMessageAsJson(JsonSerializer.Serialize(state));
    }

    // ── Simulator connection ─────────────────────────────────────────────

    private void TryConnect()
    {
        if (!_simulator.IsConnected)
        {
            _simulator.TryConnect(Handle, SimConnectMessageId);
        }
    }

    private void UpdateConnectionState()
    {
        _connectionLabel.Text = _simulator.StatusMessage;
        _transmitButton.Enabled = _simulator.IsConnected;
        UpdateReadiness();
        SendStateToJS();
    }

    // ── Contract ─────────────────────────────────────────────────────────

    private async Task LoadActiveContractAsync()
    {
        _contractLabel.Text = "Loading active contract...";
        try
        {
            _contract = await _serverClient.GetActiveContractAsync();
            _contractLabel.Text = _contract is null
                ? "None | Accept a contract on the Virtual Pilot Network website, then refresh."
                : $"{_contract.FlightDesignator}  |  {_contract.RouteDisplay}  |  {_contract.RequiredAircraftDisplay}";
        }
        catch (Exception exception)
        {
            _contract = null;
            _contractLabel.Text = $"Could not load contract: {exception.Message}";
        }
        finally
        {
            UpdateReadiness();
            SendStateToJS();
        }
    }

    // ── Telemetry ────────────────────────────────────────────────────────

    private void ServerTelemetryStatusChanged(object? sender, string statusMessage)
    {
        if (IsDisposed) return;
        BeginInvoke(() =>
        {
            _telemetryServerLabel.Text = statusMessage;
            SendStateToJS();
        });
    }

    private void ReceiveTelemetry(TelemetrySnapshot telemetry)
    {
        _latestTelemetry = telemetry;
        var cancellationReason = _flight.Observe(telemetry);
        if (cancellationReason is not null)
        {
            _ = CancelActiveFlightAsync(cancellationReason);
        }
        else if (_flight.Phase is FlightPhase.Loading
            && _contract is not null
            && _flight.LoadsMatch(_contract, telemetry))
        {
            _ = ActivateLoadedFlightAsync(_contract, telemetry);
        }
        if (_flight.FlightId is Guid flightId
            && _flight.Phase is FlightPhase.Started or FlightPhase.Airborne or FlightPhase.Landed)
        {
            _serverClient.QueueTelemetry(flightId, telemetry);
        }

        _aircraftLabel.Text = string.IsNullOrWhiteSpace(telemetry.AircraftAtcModel)
            ? telemetry.AircraftTitle
            : $"{telemetry.AircraftTitle}  »  ATC {telemetry.AircraftAtcType} {telemetry.AircraftAtcModel}";

#if DEBUG
        _positionLabel.Text = $"{telemetry.LatitudeDegrees:0.00000}, {telemetry.LongitudeDegrees:0.00000}";
        _altitudeLabel.Text = $"{telemetry.AltitudeFeet:0} ft MSL  ·  {telemetry.AltitudeAglFeet:0} ft AGL";
        _speedLabel.Text = $"{telemetry.IndicatedAirspeedKnots:0} KIAS  ·  {telemetry.GroundSpeedKnots:0} kt ground";
        _verticalSpeedLabel.Text = $"{telemetry.VerticalSpeedFeetPerMinute:+0;-0;0} ft/min";
        _headingLabel.Text = $"{telemetry.HeadingTrueDegrees:000}° true";
        _groundLabel.Text = telemetry.OnGround ? "On ground" : "Airborne";
        _attitudeLabel.Text = $"Pitch {telemetry.PitchDegrees:+0.0;-0.0;0.0}°  ·  Bank {telemetry.BankDegrees:+0.0;-0.0;0.0}°";
        _systemsLabel.Text =
            $"Fuel {telemetry.FuelTotalKg:0} kg  ·  Payload {telemetry.PayloadWeightKg:0} kg  ·  "
            + $"Weight {telemetry.TotalWeightPounds:0} lb  ·  Engines {telemetry.EngineCount}  ·  "
            + $"Gear {telemetry.GearPositionPercent:0}%  ·  Parking brake {(telemetry.ParkingBrakeSet ? "ON" : "off")}  ·  "
            + $"Sim {telemetry.SimulationRate:0.##}×  ·  Slew {(telemetry.SlewActive ? "ON" : "off")}";
#endif

        _flightStatusLabel.Text = FlightStatusText();
        _finishButton.Enabled = _flight.CanFinish;
        UpdateReadiness();
        SendStateToJS();
    }

    private void UpdateReadiness()
    {
        if (_contract is null)
        {
            _readinessLabel.Text = "Accept a contract on the website before starting a flight.";
            _readinessLabel.ForeColor = Palette.StatusWarn;
            _startButton.Enabled = false;
            return;
        }

        var readiness = _flight.EvaluateReadiness(
            _simulator.IsConnected,
            _contract,
            _latestTelemetry);
        var ready = readiness == "Ready to begin loading.";
        _readinessLabel.Text = readiness;
        _readinessLabel.ForeColor = ready ? Palette.StatusOk : Palette.StatusWarn;
        _startButton.Enabled = ready;
    }

    // ── Actions ──────────────────────────────────────────────────────────

    private bool CanApplyLoads() =>
        _contract is not null
        && _simulator.IsConnected
        && _latestTelemetry is { OnGround: true }
        && _flight.Phase is FlightPhase.Ready or FlightPhase.Loading;

    private void LoadPayloadClicked()
    {
        if (!CanApplyLoads() || _contract is null)
            return;

        try
        {
            _simulator.SetPayloadKilograms(_contract.RequiredPayloadKg);
        }
        catch (Exception exception)
        {
            MessageBox.Show(
                this,
                exception.Message,
                "Could not load payload",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
        }
    }

    private void LoadFuelClicked()
    {
        if (!CanApplyLoads() || _contract?.RequiredFuelKg is not double fuelKilograms)
            return;

        try
        {
            _simulator.SetFuelKilograms(fuelKilograms);
        }
        catch (Exception exception)
        {
            MessageBox.Show(
                this,
                exception.Message,
                "Could not load fuel",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
        }
    }

    private async void StartFlightClicked(object? sender, EventArgs eventArgs)
    {
        if (_latestTelemetry is null || _contract is null) return;

        try
        {
            _startButton.Enabled = false;
            _flight.BeginLoading();
            _flightStatusLabel.Text = FlightStatusText();
            UpdateReadiness();
            SendStateToJS();
            if (_flight.LoadsMatch(_contract, _latestTelemetry))
            {
                await ActivateLoadedFlightAsync(_contract, _latestTelemetry);
            }
        }
        catch (Exception exception)
        {
            MessageBox.Show(this, exception.Message, "Could not start flight",
                MessageBoxButtons.OK, MessageBoxIcon.Error);
            UpdateReadiness();
            SendStateToJS();
        }
    }

    private async Task ActivateLoadedFlightAsync(
        ContractAssignment contract,
        TelemetrySnapshot telemetry)
    {
        if (Interlocked.CompareExchange(ref _activationInProgress, 1, 0) != 0)
        {
            return;
        }
        try
        {
            var flightId = await _serverClient.StartFlightAsync(contract, telemetry);
            _flight.Start(flightId, telemetry);
            _flightStatusLabel.Text = FlightStatusText();
            UpdateReadiness();
            SendStateToJS();
        }
        catch (Exception exception)
        {
            _flight.AbortLoading();
            MessageBox.Show(this, exception.Message, "Could not activate flight",
                MessageBoxButtons.OK, MessageBoxIcon.Error);
            UpdateReadiness();
            SendStateToJS();
        }
        finally
        {
            Interlocked.Exchange(ref _activationInProgress, 0);
        }
    }

    private async Task CancelActiveFlightAsync(string reason)
    {
        if (Interlocked.CompareExchange(ref _cancellationInProgress, 1, 0) != 0)
        {
            return;
        }
        try
        {
            if (_flight.FlightId is Guid flightId)
            {
                await _serverClient.CancelFlightAsync(flightId, reason);
            }
        }
        catch (Exception exception)
        {
            reason += $"\n\nThe server could not be notified: {exception.Message}";
        }
        finally
        {
            MessageBox.Show(
                this,
                $"This flight has been cancelled.\n\n{reason}",
                "FLIGHT CANCELLED",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
            _flight.ResetCancelledFlight();
            _contract = null;
            _finishButton.Enabled = false;
            Interlocked.Exchange(ref _cancellationInProgress, 0);
            await LoadActiveContractAsync();
        }
    }

    private async void FinishFlightClicked(object? sender, EventArgs eventArgs)
    {
        if (_latestTelemetry is null || _flight.FlightId is not Guid flightId) return;

        try
        {
            _finishButton.Enabled = false;
            SendStateToJS();
            await _serverClient.FinishFlightAsync(flightId, _latestTelemetry);
            _flight.Finish();
            ResetAfterCompletedFlight();
            MessageBox.Show(
                this,
                "The server confirmed the flight and completed the active contract.",
                "Flight complete",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
            await LoadActiveContractAsync();
        }
        catch (Exception exception)
        {
            MessageBox.Show(this, exception.Message, "Could not finish flight",
                MessageBoxButtons.OK, MessageBoxIcon.Error);
            _finishButton.Enabled = _flight.CanFinish;
            SendStateToJS();
        }
    }

    private void TransmitAircraftClicked(object? sender, EventArgs eventArgs)
    {
        try
        {
            _transmitButton.Enabled = false;
            SendStateToJS();
            _simulator.RequestAircraftIdentity();
        }
        catch (Exception exception)
        {
            MessageBox.Show(
                this,
                exception.Message,
                "Could not read the simulator aircraft",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
            _transmitButton.Enabled = _simulator.IsConnected;
            SendStateToJS();
        }
    }

    private async Task UploadAircraftSnapshotAsync(AircraftSnapshot snapshot)
    {
        _transmitButton.Enabled = false;
        try
        {
            var result = await _serverClient.TransmitAircraftAsync(snapshot);
            MessageBox.Show(
                this,
                result.Created
                    ? $"{result.ModelDisplayName} ({result.IcaoTypeDesignator}) was added to the aircraft catalog."
                    : $"{result.ModelDisplayName} ({result.IcaoTypeDesignator}) is already in the aircraft catalog.",
                "Aircraft transmitted",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
        }
        catch (Exception exception)
        {
            MessageBox.Show(
                this,
                exception.Message,
                "Could not transmit aircraft",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
        }
        finally
        {
            _transmitButton.Enabled = _simulator.IsConnected;
            SendStateToJS();
        }
    }

    private void ResetAfterCompletedFlight()
    {
        _flight.ResetForNextFlight();
        _contract = null;
        _contractLabel.Text = "None | Accept a contract on the Virtual Pilot Network website, then refresh.";
        _telemetryServerLabel.Text = "Waiting for an active flight.";
        _flightStatusLabel.Text = FlightStatusText();
        _finishButton.Enabled = false;
        UpdateReadiness();
        SendStateToJS();
    }

    private string FlightStatusText()
    {
        var elapsed = _flight.StartedAt is DateTimeOffset startedAt
            ? $"  »  {DateTimeOffset.UtcNow - startedAt:hh\\:mm\\:ss}"
            : string.Empty;
        return _flight.Phase switch
        {
            FlightPhase.Ready => "Ready",
            FlightPhase.Loading => "Loading fuel and payload — flight not active",
            FlightPhase.Started => $"Started — waiting for takeoff{elapsed}",
            FlightPhase.Airborne => $"Airborne{elapsed}",
            FlightPhase.Landed => $"Landed — ready to finish{elapsed}",
            FlightPhase.Finished => $"Finished{elapsed}",
            FlightPhase.Cancelled => "Cancelled",
            _ => _flight.Phase.ToString(),
        };
    }
}
