using System.Text.Json;
using Microsoft.Web.WebView2.WinForms;
using Velopack;
using Velopack.Exceptions;

namespace PCareer.Client;

public sealed class UpdateForm : Form
{
    private readonly WebView2 _web = new() { Dock = DockStyle.Fill };
    private readonly UpdateManager _updateManager;
    private UpdateInfo? _pendingUpdate;

    public UpdateForm(UpdateManager updateManager)
    {
        _updateManager = updateManager;
        Text = "Virtual Pilot Network";
        StartPosition = FormStartPosition.CenterScreen;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        ClientSize = new Size(480, 440);
        BackColor = Color.FromArgb(11, 14, 18);
        Controls.Add(_web);

        _web.WebMessageReceived += (_, e) => HandleWebMessage(e.WebMessageAsJson);
    }

    protected override async void OnShown(EventArgs e)
    {
        base.OnShown(e);
        try
        {
            await _web.EnsureCoreWebView2Async();
            _web.CoreWebView2.NavigateToString(UpdateHtmlContent.Template);
            // Small delay to let the HTML render before we start the check
            await Task.Delay(300);
            await CheckForUpdatesAsync();
        }
        catch (Exception ex)
        {
            SendToJs(new { show = "stateError", error = ex.Message });
        }
    }

    private void HandleWebMessage(string json)
    {
        using var doc = JsonDocument.Parse(json);
        if (!doc.RootElement.TryGetProperty("action", out var actionProp))
            return;

        switch (actionProp.GetString())
        {
            case "update":
                _ = DownloadAndApplyAsync();
                break;
            case "quit":
                Application.Exit();
                break;
            case "retry":
                _ = CheckForUpdatesAsync();
                break;
        }
    }

    private async Task CheckForUpdatesAsync()
    {
        SendToJs(new { show = "stateChecking" });
        try
        {
            var newVersion = await _updateManager.CheckForUpdatesAsync();
            if (newVersion is null)
            {
                SendToJs(new { show = "stateCurrent" });
                await Task.Delay(1500);
                DialogResult = DialogResult.OK;
                Close();
                return;
            }

            _pendingUpdate = newVersion;
            var currentVersion = _updateManager.CurrentVersion?.ToString() ?? "1.0.0";
            var latestVersion = newVersion.TargetFullRelease.Version.ToString();
            SendToJs(new
            {
                show = "stateUpdate",
                verCurrent = currentVersion,
                verNew = latestVersion,
            });
        }
        catch (NotInstalledException)
        {
            // Not installed via Velopack — updates don't apply. Proceed to login.
            DialogResult = DialogResult.OK;
            Close();
        }
        catch (Exception ex)
        {
            SendToJs(new { show = "stateError", error = ex.Message });
        }
    }

    private async Task DownloadAndApplyAsync()
    {
        if (_pendingUpdate is null) return;

        SendToJs(new { disableUpdate = true });
        try
        {
            await _updateManager.DownloadUpdatesAsync(_pendingUpdate, progress =>
            {
                SendToJs(new { progress });
            });
            _updateManager.ApplyUpdatesAndRestart(_pendingUpdate);
        }
        catch (Exception ex)
        {
            SendToJs(new { show = "stateError", error = ex.Message });
        }
    }

    private void SendToJs(object state)
    {
        if (_web.CoreWebView2 is null) return;
        _web.CoreWebView2.PostWebMessageAsJson(JsonSerializer.Serialize(state));
    }
}
