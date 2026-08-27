namespace PCareer.Client.Services;

internal static class SimulatorAircraftIdentity
{
    public static string DecodeAtcModel(string? value) =>
        DecodeResourceKey(value, "AC_MODEL", "ATC_MODEL");

    public static string DecodeAtcType(string? value) =>
        DecodeResourceKey(value, "ATC_NAME");

    private static string DecodeResourceKey(string? value, params string[] markers)
    {
        var candidate = (value ?? string.Empty).Trim().TrimEnd('\0');
        if (!candidate.Contains("ATCCOM", StringComparison.OrdinalIgnoreCase))
        {
            return candidate;
        }

        foreach (var marker in markers)
        {
            var markerIndex = candidate.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
            if (markerIndex < 0)
            {
                continue;
            }

            var decoded = candidate[(markerIndex + marker.Length)..]
                .Trim(' ', '.', '_', '-', ':');
            var resourceSuffix = decoded.IndexOf(".0.", StringComparison.OrdinalIgnoreCase);
            if (resourceSuffix >= 0)
            {
                decoded = decoded[..resourceSuffix];
            }
            else
            {
                var textSuffix = decoded.IndexOf(".text", StringComparison.OrdinalIgnoreCase);
                if (textSuffix >= 0)
                {
                    decoded = decoded[..textSuffix];
                }
            }

            if (!string.IsNullOrWhiteSpace(decoded))
            {
                return decoded.Trim(' ', '.', '_', '-', ':');
            }
        }

        return candidate;
    }
}
