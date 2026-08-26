using System.Diagnostics;
using PCareer.Client.Models;
using PCareer.Client.Services;

namespace PCareer.Client;

public sealed class LoginForm : Form
{
    private readonly PCareerApiClient _api;
    private readonly CancellationTokenSource _cancellation = new();

    private readonly Label _statusLabel;
    private readonly Button _loginButton;

    public LoginForm(PCareerApiClient api)
    {
        _api = api;
        Text = "Virtual Pilot Network \u00b7 Discord login";
        StartPosition = FormStartPosition.CenterScreen;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        ClientSize = new Size(440, 400);
        BackColor = Color.FromArgb(11, 14, 18);
        ForeColor = Color.FromArgb(241, 245, 249);
        Font = new Font("Segoe UI", 10);

        var outerLayout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 3,
            BackColor = Color.Transparent,
            Padding = new Padding(0),
        };
        outerLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        outerLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        outerLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        outerLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        // Center row holds the card
        var centerPanel = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = Color.Transparent,
        };

        var card = new Panel
        {
            Size = new Size(360, 280),
            Anchor = AnchorStyles.None,
            BackColor = Color.FromArgb(20, 25, 32),
        };
        card.Paint += (_, e) =>
        {
            using var pen = new Pen(Color.FromArgb(20, 255, 255, 255)) { Width = 1 };
            e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            var rect = new Rectangle(0, 0, card.Width - 1, card.Height - 1);
            e.Graphics.DrawRectangle(pen, rect);
        };

        var cardInner = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(32, 28, 32, 24),
            ColumnCount = 1,
            RowCount = 4,
            BackColor = Color.Transparent,
        };
        cardInner.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        cardInner.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        cardInner.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        cardInner.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        cardInner.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        var title = new Label
        {
            Text = "Login",
            AutoSize = true,
            Font = new Font("Segoe UI", 22, FontStyle.Bold),
            ForeColor = Color.White,
            BackColor = Color.Transparent,
            Margin = new Padding(0, 0, 0, 6),
        };

        var subtitle = new Label
        {
            Text = "Continue with your Discord account to access your career dashboard.",
            AutoSize = true,
            MaximumSize = new Size(296, 0),
            Font = new Font("Segoe UI", 9.5f),
            ForeColor = Color.FromArgb(148, 163, 184),
            BackColor = Color.Transparent,
            Margin = new Padding(0, 0, 0, 22),
        };

        _loginButton = new Button
        {
            Text = "Continue with Discord",
            Width = 296,
            Height = 44,
            FlatStyle = FlatStyle.Flat,
            BackColor = Color.FromArgb(88, 101, 242),
            ForeColor = Color.White,
            Font = new Font("Segoe UI", 10, FontStyle.Bold),
            Cursor = Cursors.Hand,
            Margin = new Padding(0, 0, 0, 12),
        };
        _loginButton.FlatAppearance.BorderSize = 0;
        _loginButton.Click += LoginClicked;

        _statusLabel = new Label
        {
            Text = "",
            AutoSize = true,
            MaximumSize = new Size(296, 0),
            Font = new Font("Segoe UI", 9f),
            ForeColor = Color.FromArgb(148, 163, 184),
            BackColor = Color.Transparent,
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.BottomLeft,
        };

        cardInner.Controls.Add(title, 0, 0);
        cardInner.Controls.Add(subtitle, 0, 1);
        cardInner.Controls.Add(_loginButton, 0, 2);
        cardInner.Controls.Add(_statusLabel, 0, 3);
        card.Controls.Add(cardInner);
        centerPanel.Controls.Add(card);
        outerLayout.Controls.Add(centerPanel, 0, 1);

        // Bottom security note
        var divider = new Panel
        {
            Height = 1,
            Dock = DockStyle.Top,
            BackColor = Color.FromArgb(17, 255, 255, 255),
            Margin = new Padding(32, 0, 32, 10),
        };
        var securityNote = new Label
        {
            Text = "Authentication is handled directly by Discord. Your password is never shared with Virtual Pilot Network.",
            AutoSize = true,
            MaximumSize = new Size(376, 0),
            Font = new Font("Segoe UI", 8f),
            ForeColor = Color.FromArgb(100, 116, 139),
            BackColor = Color.Transparent,
            Dock = DockStyle.Top,
            Margin = new Padding(0, 0, 0, 8),
        };
        var bottomPanel = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 2,
            BackColor = Color.Transparent,
        };
        bottomPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        bottomPanel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        bottomPanel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        bottomPanel.Controls.Add(divider, 0, 0);
        bottomPanel.Controls.Add(securityNote, 0, 1);
        outerLayout.Controls.Add(bottomPanel, 0, 2);

        Controls.Add(outerLayout);
        FormClosed += (_, _) => _cancellation.Cancel();
    }

    public DesktopSession? Session { get; private set; }

    private async void LoginClicked(object? sender, EventArgs eventArgs)
    {
        _loginButton.Enabled = false;
        try
        {
            _statusLabel.ForeColor = Color.FromArgb(148, 163, 184);
            _statusLabel.Text = "Preparing secure Discord login...";
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
