using System.Text.Json;
using Microsoft.Web.WebView2.WinForms;

namespace PCareer.Client;

public sealed class UpdateForm : Form
{
    private readonly WebView2 _web = new() { Dock = DockStyle.Fill };
    private readonly PortableUpdateClient _updateClient;
    private PortableUpdateManifest? _pendingUpdate;

    internal UpdateForm(PortableUpdateClient updateClient)
    {
        _updateClient = updateClient;
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
            var environment = await WebViewRuntime.CreateEnvironmentAsync();
            await _web.EnsureCoreWebView2Async(environment);
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
                DialogResult = DialogResult.Cancel;
                Close();
                break;
            case "retry":
                _ = CheckForUpdatesAsync();
                break;
            case "continue":
                DialogResult = DialogResult.OK;
                Close();
                break;
        }
    }

    private async Task CheckForUpdatesAsync()
    {
        SendToJs(new { show = "stateChecking" });
        try
        {
            var newVersion = await _updateClient.CheckForUpdateAsync();
            if (newVersion is null)
            {
                SendToJs(new { show = "stateCurrent" });
                await Task.Delay(1500);
                DialogResult = DialogResult.OK;
                Close();
                return;
            }

            _pendingUpdate = newVersion;
            var currentVersion = _updateClient.CurrentVersion.ToString(3);
            var latestVersion = newVersion.ParsedVersion.ToString(3);
            SendToJs(new
            {
                show = "stateUpdate",
                verCurrent = currentVersion,
                verNew = latestVersion,
            });
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
            var progress = new Progress<int>(value => SendToJs(new { progress = value }));
            var executable = await _updateClient.DownloadAsync(_pendingUpdate, progress);
            PortableUpdater.BeginApply(executable, _pendingUpdate.NormalizedSha256);
            DialogResult = DialogResult.Abort;
            Close();
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
