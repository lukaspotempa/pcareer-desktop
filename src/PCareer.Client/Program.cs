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

        Application.Run(new MainForm());
        return 0;
    }
}
