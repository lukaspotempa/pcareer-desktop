using System.Diagnostics;
using System.Drawing.Drawing2D;
using PCareer.Client.Models;
using PCareer.Client.Services;

namespace PCareer.Client;

public sealed class LoginForm : Form
{
    private readonly PCareerApiClient _api;
    private readonly CancellationTokenSource _cancellation = new();
    private readonly Image _brandLogo;
    private readonly RoundedPanel _card;
    private readonly Label _statusLabel;
    private readonly RoundedButton _loginButton;

    public LoginForm(PCareerApiClient api)
    {
        _api = api;
        Text = "Virtual Pilot Network";
        Icon = BrandAssets.ApplicationIcon;
        StartPosition = FormStartPosition.CenterScreen;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        ClientSize = new Size(780, 380);
        AutoScaleMode = AutoScaleMode.Dpi;
        BackColor = Palette.AppBackground;
        ForeColor = Palette.PrimaryText;
        Font = new Font("Segoe UI", 10);
        DoubleBuffered = true;

        _brandLogo = LoadBrandLogo();

        _card = new RoundedPanel
        {
            Size = new Size(704, 350),
            BackColor = Color.FromArgb(14, 21, 28),
            CornerRadius = 16,
        };

        var brandPane = new Panel
        {
            Location = Point.Empty,
            Size = new Size(244, 350),
            BackColor = Color.FromArgb(14, 21, 28),
        };

        var logo = new PictureBox
        {
            Location = new Point(38, 38),
            Size = new Size(168, 142),
            Image = _brandLogo,
            SizeMode = PictureBoxSizeMode.Zoom,
            BackColor = Color.Transparent,
            AccessibleName = "Virtual Pilot Network logo",
        };

        var brandName = new Label
        {
            Location = new Point(24, 198),
            Size = new Size(196, 48),
            Text = "VIRTUAL PILOT\r\nNETWORK",
            TextAlign = ContentAlignment.MiddleCenter,
            Font = new Font("Segoe UI", 13, FontStyle.Bold),
            ForeColor = Palette.PrimaryText,
            BackColor = Color.Transparent,
        };
        var productName = new Label
        {
            Location = new Point(24, 256),
            Size = new Size(196, 19),
            Text = "CAREER COMPANION",
            TextAlign = ContentAlignment.MiddleCenter,
            Font = new Font("Segoe UI", 7.5f, FontStyle.Bold),
            ForeColor = Palette.AccentLight,
            BackColor = Color.Transparent,
        };
        var brandDescription = new Label
        {
            Location = new Point(28, 288),
            Size = new Size(188, 42),
            Text = "The companion app to Virtual Pilot Network.",
            TextAlign = ContentAlignment.TopCenter,
            Font = new Font("Segoe UI", 8.25f),
            ForeColor = Palette.DimText,
            BackColor = Color.Transparent,
        };

        brandPane.Controls.Add(logo);
        brandPane.Controls.Add(brandName);
        brandPane.Controls.Add(productName);
        brandPane.Controls.Add(brandDescription);

        var columnDivider = new Panel
        {
            Location = new Point(244, 0),
            Size = new Size(1, 350),
            BackColor = Palette.BorderHover,
        };

        var title = new Label
        {
            Location = new Point(280, 34),
            AutoSize = true,
            Text = "Welcome back",
            Font = new Font("Segoe UI", 20, FontStyle.Bold),
            ForeColor = Palette.PrimaryText,
            BackColor = Color.Transparent,
        };

        var subtitle = new Label
        {
            Location = new Point(280, 75),
            Size = new Size(392, 44),
            Text = "Sign in with Discord to access your active contracts and sync your flight progress.",
            Font = new Font("Segoe UI", 9.5f),
            ForeColor = Palette.MutedText,
            BackColor = Color.Transparent,
        };

        _loginButton = new RoundedButton
        {
            Location = new Point(314, 148),
            Size = new Size(324, 46),
            Text = "Login with Discord",
            FlatStyle = FlatStyle.Flat,
            BackColor = Palette.DiscordBlurple,
            HoverColor = Color.FromArgb(71, 82, 196),
            ForeColor = Color.White,
            Font = new Font("Segoe UI", 10, FontStyle.Bold),
            Cursor = Cursors.Hand,
            CornerRadius = 9,
            TabIndex = 0,
            AccessibleName = "Open Discord to sign in",
            AccessibleDescription = "Opens Discord authentication in your default browser.",
        };
        _loginButton.FlatAppearance.BorderSize = 0;
        _loginButton.Click += LoginClicked;

        _statusLabel = new Label
        {
            Location = new Point(314, 205),
            Size = new Size(324, 26),
            Text = "",
            TextAlign = ContentAlignment.TopCenter,
            Font = new Font("Segoe UI", 8f),
            ForeColor = Palette.DimText,
            BackColor = Color.Transparent,
        };

        var securityDivider = new Panel
        {
            Location = new Point(280, 256),
            Size = new Size(392, 1),
            BackColor = Palette.Border,
        };
        var securityHeading = new Label
        {
            Location = new Point(280, 276),
            AutoSize = true,
            Text = "SECURE DISCORD AUTHENTICATION",
            Font = new Font("Segoe UI", 7.5f, FontStyle.Bold),
            ForeColor = Palette.DimText,
            BackColor = Color.Transparent,
        };
        var securityNote = new Label
        {
            Location = new Point(280, 296),
            Size = new Size(392, 34),
            Text = "Your password is never shared with Virtual Pilot Network.",
            Font = new Font("Segoe UI", 8.25f),
            ForeColor = Palette.MutedText,
            BackColor = Color.Transparent,
        };

        _card.Controls.Add(brandPane);
        _card.Controls.Add(columnDivider);
        _card.Controls.Add(title);
        _card.Controls.Add(subtitle);
        _card.Controls.Add(_loginButton);
        _card.Controls.Add(_statusLabel);
        _card.Controls.Add(securityDivider);
        _card.Controls.Add(securityHeading);
        _card.Controls.Add(securityNote);
        Controls.Add(_card);

        AcceptButton = _loginButton;
        Layout += (_, _) => CenterCard();
        FormClosed += (_, _) => _cancellation.Cancel();
    }

    public DesktopSession? Session { get; private set; }

    private void CenterCard()
    {
        _card.Location = new Point(
            (ClientSize.Width - _card.Width) / 2,
            (ClientSize.Height - _card.Height) / 2);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _brandLogo.Dispose();
            _cancellation.Dispose();
        }
        base.Dispose(disposing);
    }

    private static Image LoadBrandLogo()
    {
        const string resourceName = "PCareer.Client.Resources.BrandLogo.png";
        using var stream = typeof(LoginForm).Assembly.GetManifestResourceStream(resourceName)
            ?? throw new InvalidOperationException($"Embedded resource '{resourceName}' was not found.");
        using var source = Image.FromStream(stream);
        return new Bitmap(source);
    }

    private void ShowStatus(string message, bool isError = false)
    {
        _statusLabel.Text = message;
        _statusLabel.ForeColor = isError ? Palette.StatusError : Palette.DimText;
    }

    private async void LoginClicked(object? sender, EventArgs eventArgs)
    {
        _loginButton.Enabled = false;
        _loginButton.Text = "Opening Discord…";
        UseWaitCursor = true;
        try
        {
            ShowStatus("Preparing a secure Discord sign-in…");
            var login = await _api.BeginDiscordLoginAsync(_cancellation.Token);
            Process.Start(new ProcessStartInfo(login.AuthorizationUrl.AbsoluteUri)
            {
                UseShellExecute = true,
            });
            ShowStatus("Finish signing in in your browser. We'll update automatically.");

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
            throw new TimeoutException("Discord sign-in expired. Please try again.");
        }
        catch (OperationCanceledException) when (_cancellation.IsCancellationRequested)
        {
        }
        catch (Exception exception)
        {
            ShowStatus(exception.Message, isError: true);
            _loginButton.Enabled = true;
            _loginButton.Text = "Try Discord sign-in again  ↗";
        }
        finally
        {
            UseWaitCursor = false;
        }
    }

    private sealed class RoundedPanel : Panel
    {
        private int _cornerRadius = 8;

        public int CornerRadius
        {
            get => _cornerRadius;
            set
            {
                _cornerRadius = value;
                UpdateRegion();
                Invalidate();
            }
        }

        public Color BorderColor { get; set; } = Color.Transparent;

        protected override void OnSizeChanged(EventArgs eventArgs)
        {
            base.OnSizeChanged(eventArgs);
            UpdateRegion();
        }

        protected override void OnPaint(PaintEventArgs eventArgs)
        {
            base.OnPaint(eventArgs);
            if (BorderColor == Color.Transparent)
                return;

            eventArgs.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            using var path = CreateRoundedPath(new Rectangle(0, 0, Width - 1, Height - 1), CornerRadius);
            using var pen = new Pen(BorderColor);
            eventArgs.Graphics.DrawPath(pen, path);
        }

        private void UpdateRegion()
        {
            if (Width <= 0 || Height <= 0)
                return;

            using var path = CreateRoundedPath(ClientRectangle, CornerRadius);
            Region = new Region(path);
        }
    }

    private sealed class RoundedButton : Button
    {
        private int _cornerRadius = 8;
        private Color _normalColor;

        public int CornerRadius
        {
            get => _cornerRadius;
            set
            {
                _cornerRadius = value;
                UpdateRegion();
            }
        }

        public Color HoverColor { get; set; }

        protected override void OnCreateControl()
        {
            base.OnCreateControl();
            _normalColor = BackColor;
        }

        protected override void OnSizeChanged(EventArgs eventArgs)
        {
            base.OnSizeChanged(eventArgs);
            UpdateRegion();
        }

        protected override void OnMouseEnter(EventArgs eventArgs)
        {
            base.OnMouseEnter(eventArgs);
            if (Enabled)
                BackColor = HoverColor;
        }

        protected override void OnMouseLeave(EventArgs eventArgs)
        {
            base.OnMouseLeave(eventArgs);
            BackColor = _normalColor;
        }

        protected override void OnEnabledChanged(EventArgs eventArgs)
        {
            base.OnEnabledChanged(eventArgs);
            BackColor = Enabled ? _normalColor : Color.FromArgb(72, 80, 145);
        }

        private void UpdateRegion()
        {
            if (Width <= 0 || Height <= 0)
                return;

            using var path = CreateRoundedPath(ClientRectangle, CornerRadius);
            Region = new Region(path);
        }
    }

    private static GraphicsPath CreateRoundedPath(Rectangle bounds, int radius)
    {
        var diameter = Math.Max(1, radius * 2);
        var arc = new Rectangle(bounds.Location, new Size(diameter, diameter));
        var path = new GraphicsPath();
        path.AddArc(arc, 180, 90);
        arc.X = bounds.Right - diameter;
        path.AddArc(arc, 270, 90);
        arc.Y = bounds.Bottom - diameter;
        path.AddArc(arc, 0, 90);
        arc.X = bounds.Left;
        path.AddArc(arc, 90, 90);
        path.CloseFigure();
        return path;
    }
}
