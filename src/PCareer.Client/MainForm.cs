using System.Drawing;
using PCareer.Client.Models;
using PCareer.Client.Services;

namespace PCareer.Client;

public sealed class MainForm : Form
{
    private const int SimConnectMessageId = 0x0402;

    private readonly ISimulatorConnection _simulator = SimulatorConnectionFactory.Create();
    private readonly IFlightServerClient _serverClient = new LocalFlightServerClient();
    private readonly FlightSessionController _flight = new();
    private readonly ContractAssignment _contract = ContractAssignment.DevelopmentFlight;
    private readonly System.Windows.Forms.Timer _retryTimer = new() { Interval = 2000 };

    private readonly Label _connectionLabel = ValueLabel("Checking simulator…");
    private readonly Label _aircraftLabel = ValueLabel("—");
    private readonly Label _positionLabel = ValueLabel("—");
    private readonly Label _altitudeLabel = ValueLabel("—");
    private readonly Label _speedLabel = ValueLabel("—");
    private readonly Label _headingLabel = ValueLabel("—");
    private readonly Label _groundLabel = ValueLabel("—");
    private readonly Label _readinessLabel = ValueLabel("Waiting for simulator telemetry.");
    private readonly Label _flightStatusLabel = ValueLabel("Ready");
    private readonly Button _startButton = ActionButton("Start flight");
    private readonly Button _finishButton = ActionButton("Finish flight");

    private TelemetrySnapshot? _latestTelemetry;

    public MainForm()
    {
        Text = "PCareer Flight Client";
        StartPosition = FormStartPosition.CenterScreen;
        MinimumSize = new Size(760, 560);
        Size = new Size(860, 640);
        BackColor = Color.FromArgb(17, 23, 22);
        ForeColor = Color.FromArgb(235, 241, 237);
        Font = new Font("Segoe UI", 10);

        Controls.Add(BuildContent());

        _startButton.Click += StartFlightClicked;
        _finishButton.Click += FinishFlightClicked;
        _retryTimer.Tick += (_, _) => TryConnect();
        _simulator.ConnectionChanged += (_, _) => UpdateConnectionState();
        _simulator.TelemetryReceived += (_, telemetry) => ReceiveTelemetry(telemetry);
        FormClosed += (_, _) => _simulator.Dispose();

        _finishButton.Enabled = false;
    }

    protected override void OnShown(EventArgs e)
    {
        base.OnShown(e);
        TryConnect();
        _retryTimer.Start();
    }

    protected override void WndProc(ref Message message)
    {
        if (message.Msg == SimConnectMessageId)
        {
            _simulator.ReceiveMessage();
        }

        base.WndProc(ref message);
    }

    private Control BuildContent()
    {
        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(28),
            ColumnCount = 1,
            RowCount = 5,
            AutoScroll = true,
        };
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        var title = new Label
        {
            Text = "PCareer Flight Client",
            AutoSize = true,
            Font = new Font(Font.FontFamily, 22, FontStyle.Bold),
            Margin = new Padding(0, 0, 0, 18),
        };
        root.Controls.Add(title);

        var connectionCard = Card();
        connectionCard.Controls.Add(Field("Simulator", _connectionLabel));
        root.Controls.Add(connectionCard);

        var contractCard = Card();
        contractCard.Controls.Add(
            Field(
                "Development contract",
                ValueLabel(
                    $"{_contract.ContractId}  •  {_contract.DepartureName} → {_contract.ArrivalName}  •  {_contract.RequiredAircraftDisplay}")));
        contractCard.Margin = new Padding(0, 12, 0, 0);
        root.Controls.Add(contractCard);

        var telemetryGrid = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            ColumnCount = 2,
            RowCount = 3,
            Margin = new Padding(0, 12, 0, 0),
        };
        telemetryGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        telemetryGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        telemetryGrid.Controls.Add(TelemetryCard("Aircraft", _aircraftLabel), 0, 0);
        telemetryGrid.Controls.Add(TelemetryCard("Position", _positionLabel), 1, 0);
        telemetryGrid.Controls.Add(TelemetryCard("Altitude", _altitudeLabel), 0, 1);
        telemetryGrid.Controls.Add(TelemetryCard("Airspeed", _speedLabel), 1, 1);
        telemetryGrid.Controls.Add(TelemetryCard("Heading", _headingLabel), 0, 2);
        telemetryGrid.Controls.Add(TelemetryCard("Ground state", _groundLabel), 1, 2);
        root.Controls.Add(telemetryGrid);

        var actionCard = Card();
        actionCard.Margin = new Padding(0, 12, 0, 0);
        var actionLayout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            AutoSize = true,
            ColumnCount = 2,
            RowCount = 3,
        };
        actionLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        actionLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        actionLayout.Controls.Add(Field("Readiness", _readinessLabel), 0, 0);
        actionLayout.SetColumnSpan(actionLayout.GetControlFromPosition(0, 0)!, 2);
        actionLayout.Controls.Add(Field("Flight state", _flightStatusLabel), 0, 1);
        actionLayout.SetColumnSpan(actionLayout.GetControlFromPosition(0, 1)!, 2);
        actionLayout.Controls.Add(_startButton, 0, 2);
        actionLayout.Controls.Add(_finishButton, 1, 2);
        actionCard.Controls.Add(actionLayout);
        root.Controls.Add(actionCard);

        return root;
    }

    private static Panel Card() => new()
    {
        Dock = DockStyle.Top,
        AutoSize = true,
        Padding = new Padding(18),
        BackColor = Color.FromArgb(25, 34, 32),
        Margin = new Padding(0),
    };

    private static Control Field(string label, Label value)
    {
        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            ColumnCount = 1,
            RowCount = 2,
        };
        layout.Controls.Add(new Label
        {
            Text = label.ToUpperInvariant(),
            ForeColor = Color.FromArgb(125, 163, 151),
            AutoSize = true,
            Font = new Font("Segoe UI", 8, FontStyle.Bold),
        });
        layout.Controls.Add(value);
        return layout;
    }

    private static Control TelemetryCard(string title, Label value)
    {
        var card = Card();
        card.Margin = new Padding(0, 0, 8, 8);
        card.Controls.Add(Field(title, value));
        return card;
    }

    private static Label ValueLabel(string text) => new()
    {
        Text = text,
        AutoSize = true,
        MaximumSize = new Size(720, 0),
        ForeColor = Color.FromArgb(235, 241, 237),
        Font = new Font("Segoe UI", 10),
        Margin = new Padding(0, 5, 0, 0),
    };

    private static Button ActionButton(string text) => new()
    {
        Text = text,
        Dock = DockStyle.Fill,
        AutoSize = true,
        Height = 40,
        Margin = new Padding(0, 14, 8, 0),
        FlatStyle = FlatStyle.Flat,
        BackColor = Color.FromArgb(52, 128, 96),
        ForeColor = Color.White,
    };

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
        _connectionLabel.ForeColor = _simulator.IsConnected
            ? Color.FromArgb(120, 226, 166)
            : Color.FromArgb(235, 173, 149);
        UpdateReadiness();
    }

    private void ReceiveTelemetry(TelemetrySnapshot telemetry)
    {
        _latestTelemetry = telemetry;
        _flight.Observe(telemetry);
        if (_flight.FlightId is Guid flightId
            && _flight.Phase is FlightPhase.Started or FlightPhase.Airborne or FlightPhase.Landed)
        {
            _serverClient.QueueTelemetry(flightId, telemetry);
        }

        _aircraftLabel.Text = telemetry.AircraftTitle;
        _positionLabel.Text = $"{telemetry.LatitudeDegrees:0.00000}, {telemetry.LongitudeDegrees:0.00000}";
        _altitudeLabel.Text = $"{telemetry.AltitudeFeet:0} ft MSL  •  {telemetry.AltitudeAglFeet:0} ft AGL";
        _speedLabel.Text = $"{telemetry.IndicatedAirspeedKnots:0} KIAS  •  {telemetry.GroundSpeedKnots:0} kt ground";
        _headingLabel.Text = $"{telemetry.HeadingTrueDegrees:000}° true";
        _groundLabel.Text = telemetry.OnGround ? "On ground" : "Airborne";
        _flightStatusLabel.Text = FlightStatusText();
        _finishButton.Enabled = _flight.CanFinish;
        UpdateReadiness();
    }

    private void UpdateReadiness()
    {
        var readiness = _flight.EvaluateReadiness(
            _simulator.IsConnected,
            _contract,
            _latestTelemetry);
        _readinessLabel.Text = readiness;
        _readinessLabel.ForeColor = readiness == "Ready to start flight."
            ? Color.FromArgb(120, 226, 166)
            : Color.FromArgb(235, 206, 149);
        _startButton.Enabled = readiness == "Ready to start flight.";
    }

    private async void StartFlightClicked(object? sender, EventArgs eventArgs)
    {
        if (_latestTelemetry is null)
        {
            return;
        }

        try
        {
            _startButton.Enabled = false;
            var flightId = await _serverClient.StartFlightAsync(_contract, _latestTelemetry);
            _flight.Start(flightId, _latestTelemetry);
            _flightStatusLabel.Text = FlightStatusText();
            UpdateReadiness();
        }
        catch (Exception exception)
        {
            MessageBox.Show(this, exception.Message, "Could not start flight", MessageBoxButtons.OK, MessageBoxIcon.Error);
            UpdateReadiness();
        }
    }

    private async void FinishFlightClicked(object? sender, EventArgs eventArgs)
    {
        if (_latestTelemetry is null || _flight.FlightId is not Guid flightId)
        {
            return;
        }

        try
        {
            _finishButton.Enabled = false;
            await _serverClient.FinishFlightAsync(flightId, _latestTelemetry);
            _flight.Finish();
            _flightStatusLabel.Text = FlightStatusText();
            MessageBox.Show(
                this,
                "The local development flight is complete. A future server will verify and settle it.",
                "Flight complete",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
        }
        catch (Exception exception)
        {
            MessageBox.Show(this, exception.Message, "Could not finish flight", MessageBoxButtons.OK, MessageBoxIcon.Error);
            _finishButton.Enabled = _flight.CanFinish;
        }
    }

    private string FlightStatusText()
    {
        var elapsed = _flight.StartedAt is DateTimeOffset startedAt
            ? $"  •  {DateTimeOffset.UtcNow - startedAt:hh\\:mm\\:ss}"
            : string.Empty;
        return _flight.Phase switch
        {
            FlightPhase.Ready => "Ready",
            FlightPhase.Started => $"Started — waiting for takeoff{elapsed}",
            FlightPhase.Airborne => $"Airborne{elapsed}",
            FlightPhase.Landed => $"Landed — ready to finish{elapsed}",
            FlightPhase.Finished => $"Finished{elapsed}",
            _ => _flight.Phase.ToString(),
        };
    }
}

