using System.Runtime.Loader;

namespace PCareer.Client;

internal static class SimConnectAssemblyBootstrap
{
    private const string ManagedAssemblyFileName =
        "Microsoft.FlightSimulator.SimConnect.dll";

    public static bool TryLoad(out string error)
    {
#if SIMCONNECT_AVAILABLE
        var assemblyPath = Path.Combine(AppContext.BaseDirectory, ManagedAssemblyFileName);
        if (!File.Exists(assemblyPath))
        {
            error =
                $"The managed SimConnect assembly is missing from the application folder:{Environment.NewLine}{assemblyPath}{Environment.NewLine}{Environment.NewLine}"
                + "Rebuild PCareer with -RequireSimConnect.";
            return false;
        }

        try
        {
            AssemblyLoadContext.Default.LoadFromAssemblyPath(assemblyPath);
        }
        catch (Exception exception)
        {
            error =
                $"The managed SimConnect assembly could not be loaded:{Environment.NewLine}{assemblyPath}{Environment.NewLine}{Environment.NewLine}"
                + exception.Message;
            return false;
        }
#endif

        error = string.Empty;
        return true;
    }
}

