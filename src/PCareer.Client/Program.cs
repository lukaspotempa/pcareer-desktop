using PCareer.Client.Services;

namespace PCareer.Client;

internal static class Program
{
    [STAThread]
    private static int Main(string[] args)
    {
        ApplicationConfiguration.Initialize();

        if (!SimConnectAssemblyBootstrap.TryLoad(out var loadError))
        {
            if (args.Contains("--startup-check", StringComparer.OrdinalIgnoreCase))
            {
                return 1;
            }

            MessageBox.Show(
                loadError,
                "PCareer SimConnect startup error",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
            return 1;
        }

        if (args.Contains("--startup-check", StringComparer.OrdinalIgnoreCase))
        {
            using var simulator = SimulatorConnectionFactory.Create();
            simulator.TryConnect(IntPtr.Zero, 0x0402);
            return simulator.StatusMessage.StartsWith(
                "SimConnect error:",
                StringComparison.OrdinalIgnoreCase)
                ? 2
                : 0;
        }

        var serverUrl = Environment.GetEnvironmentVariable("PCAREER_SERVER_URL")
            ?? "https://career.virtual-pilot.com/";
        if (!Uri.TryCreate(serverUrl, UriKind.Absolute, out var serverUri))
        {
            MessageBox.Show(
                $"PCAREER_SERVER_URL is invalid: {serverUrl}",
                "PCareer configuration error",
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
}
