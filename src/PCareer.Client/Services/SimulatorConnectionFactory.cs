namespace PCareer.Client.Services;

public static class SimulatorConnectionFactory
{
    public static ISimulatorConnection Create()
    {
#if SIMCONNECT_AVAILABLE
        return new MsfsSimConnectService();
#else
        return new SimConnectUnavailableService();
#endif
    }
}

