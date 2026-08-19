using System.Diagnostics;
using PCareer.Client.Models;
using PCareer.Client.Services;

namespace PCareer.Client;

public sealed class LoginForm : Form
{
    private readonly PCareerApiClient _api;
    private readonly CancellationTokenSource _cancellation = new();
    private readonly Button _loginButton = new()
    {
        Text = "Continue with Discord",
        AutoSize = true,
        Height = 44,
        Padding = new Padding(18, 6, 18, 6),
        FlatStyle = FlatStyle.Flat,
        BackColor = Color.FromArgb(88, 101, 242),
        ForeColor = Color.White,
    };
    private readonly Label _statusLabel = new()
    {
        Text = "Sign in to connect this client to your PCareer account.",
        AutoSize = true,
        MaximumSize = new Size(440, 0),
        TextAlign = ContentAlignment.MiddleCenter,
        ForeColor = Color.FromArgb(156, 163, 175),
    };

    public LoginForm(PCareerApiClient api)
    {
        _api = api;
        Text = "PCareer · Discord login";
        StartPosition = FormStartPosition.CenterScreen;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        ClientSize = new Size(520, 330);
        BackColor = Color.FromArgb(17, 24, 39);
        ForeColor = Color.FromArgb(243, 244, 246);
        Font = new Font("Segoe UI", 10);

        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(36),
            ColumnCount = 1,
            RowCount = 5,
        };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        var mark = new Label
        {
            Text = "PC",
            AutoSize = false,
            Size = new Size(64, 64),
            TextAlign = ContentAlignment.MiddleCenter,
            Anchor = AnchorStyles.None,
            BackColor = Color.FromArgb(79, 70, 229),
            ForeColor = Color.White,
            Font = new Font("Segoe UI", 18, FontStyle.Bold),
        };
        var title = new Label
        {
            Text = "PCareer Flight Client",
            AutoSize = true,
            Anchor = AnchorStyles.None,
            Font = new Font("Segoe UI", 20, FontStyle.Bold),
            Margin = new Padding(0, 16, 0, 6),
        };
        _statusLabel.Anchor = AnchorStyles.None;
        _statusLabel.Margin = new Padding(0, 0, 0, 20);
        _loginButton.Anchor = AnchorStyles.None;
        _loginButton.Click += LoginClicked;

        layout.Controls.Add(mark, 0, 0);
        layout.Controls.Add(title, 0, 1);
        layout.Controls.Add(_statusLabel, 0, 2);
        layout.Controls.Add(_loginButton, 0, 3);
        Controls.Add(layout);
        FormClosed += (_, _) => _cancellation.Cancel();
    }

    public DesktopSession? Session { get; private set; }

    private async void LoginClicked(object? sender, EventArgs eventArgs)
    {
        _loginButton.Enabled = false;
        try
        {
            _statusLabel.Text = "Preparing secure Discord login…";
            var login = await _api.BeginDiscordLoginAsync(_cancellation.Token);
            Process.Start(new ProcessStartInfo(login.AuthorizationUrl.AbsoluteUri)
            {
                UseShellExecute = true,
            });
            _statusLabel.Text = "Complete authorization in your browser. This window will update automatically.";

            while (DateTimeOffset.UtcNow < login.ExpiresAt)
            {
                await Task.Delay(
                    TimeSpan.FromSeconds(login.PollIntervalSeconds),
                    _cancellation.Token);
                Session = await _api.PollDiscordLoginAsync(login, _cancellation.Token);
                if (Session is not null)
                {
                    DialogResult = DialogResult.OK;
                    Close();
                    return;
                }
            }
            throw new TimeoutException("Discord login expired. Please try again.");
        }
        catch (OperationCanceledException) when (_cancellation.IsCancellationRequested)
        {
        }
        catch (Exception exception)
        {
            _statusLabel.Text = exception.Message;
            _statusLabel.ForeColor = Color.FromArgb(248, 113, 113);
            _loginButton.Enabled = true;
        }
    }
}
