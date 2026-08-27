using PCareer.Client.Services;

namespace PCareer.Client;

internal static class Program
{
    [STAThread]
    private static int Main(string[] args)
    {
        if (PortableUpdater.TryApplyPendingUpdate(args, out var updateExitCode))
        {
            return updateExitCode;
        }

        PortableUpdater.CleanupDownloads();
        ApplicationConfiguration.Initialize();

        // --startup-check is a CI/test flag, skip everything else
        if (args.Contains("--startup-check", StringComparer.OrdinalIgnoreCase))
        {
            if (!SimConnectAssemblyBootstrap.TryLoad(out var loadError))
            {
                return 1;
            }

            using var simulator = SimulatorConnectionFactory.Create();
            simulator.TryConnect(IntPtr.Zero, 0x0402);
            return simulator.StatusMessage.StartsWith(
                "SimConnect error:",
                StringComparison.OrdinalIgnoreCase)
                ? 2
                : 0;
        }

        // --skip-update-check bypasses the portable update check when testing a release build.
        var skipUpdate = args.Contains("--skip-update-check", StringComparer.OrdinalIgnoreCase);

        if (!skipUpdate)
        {
            var exitCode = RunUpdateCheck();
            if (exitCode is not null)
                return exitCode.Value;
        }

        if (!SimConnectAssemblyBootstrap.TryLoad(out var simLoadError))
        {
            MessageBox.Show(
                simLoadError,
                "Virtual Pilot Network — SimConnect startup error",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
            return 1;
        }

        var serverUrl = Environment.GetEnvironmentVariable("PCAREER_SERVER_URL")
            ?? "https://career.virtual-pilot.com/";
        if (!Uri.TryCreate(serverUrl, UriKind.Absolute, out var serverUri))
        {
            MessageBox.Show(
                $"PCAREER_SERVER_URL is invalid: {serverUrl}",
                "Virtual Pilot Network — configuration error",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
            return 3;
        }

        using var api = new PCareerApiClient(serverUri);
        using var login = new LoginForm(api);
        if (login.ShowDialog() != DialogResult.OK || login.Session is null)
        {
            return 0;
        }

        Application.Run(new MainForm(api, login.Session));
        return 0;
    }

    /// <summary>
    /// Checks the portable release manifest published with the latest GitHub release.
    /// Returns null to continue, or an exit code to stop.
    /// </summary>
    private static int? RunUpdateCheck()
    {
#if !SINGLE_FILE_PUBLISH
        return null;
#else
        var manifestUrl = Environment.GetEnvironmentVariable("PCAREER_UPDATE_MANIFEST_URL")
            ?? "https://github.com/lukaspotempa/pcareer-desktop/releases/latest/download/VirtualPilotNetwork-update.json";

        try
        {
            if (!Uri.TryCreate(manifestUrl, UriKind.Absolute, out var manifestUri))
            {
                throw new InvalidOperationException("PCAREER_UPDATE_MANIFEST_URL is not a valid absolute URL.");
            }

            var client = new PortableUpdateClient(manifestUri);
            using var updateForm = new UpdateForm(client);
            var result = updateForm.ShowDialog();

            if (result == DialogResult.OK)
                return null; // up to date, continue to login

            // User clicked Quit or closed the window
            return 0;
        }
        catch (Exception ex)
        {
            var retry = MessageBox.Show(
                $"Could not check for updates:\n\n{ex.Message}\n\nContinue without updating?",
                "Virtual Pilot Network — update check failed",
                MessageBoxButtons.RetryCancel,
                MessageBoxIcon.Warning);
            return retry == DialogResult.Retry ? RunUpdateCheck() : null;
        }
#endif
    }
}
