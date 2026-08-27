using System.Diagnostics;
using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;

namespace PCareer.Client;

internal sealed record PortableUpdateManifest(
    string Version,
    string Url,
    string Sha256,
    long Size)
{
    public Version ParsedVersion => PortableUpdater.ParseVersion(Version);

    public Uri DownloadUri
    {
        get
        {
            if (!Uri.TryCreate(Url, UriKind.Absolute, out var uri) || uri.Scheme != Uri.UriSchemeHttps)
            {
                throw new InvalidDataException("The update download URL must use HTTPS.");
            }

            return uri;
        }
    }

    public string NormalizedSha256
    {
        get
        {
            var normalized = Sha256.Trim().ToUpperInvariant();
            if (normalized.Length != 64 || normalized.Any(character => !Uri.IsHexDigit(character)))
            {
                throw new InvalidDataException("The update manifest contains an invalid SHA-256 checksum.");
            }

            return normalized;
        }
    }

    public void Validate()
    {
        _ = ParsedVersion;
        _ = DownloadUri;
        _ = NormalizedSha256;
        if (Size <= 0 || Size > 1_073_741_824)
        {
            throw new InvalidDataException("The update manifest contains an invalid file size.");
        }
    }
}

internal sealed class PortableUpdateClient
{
    private static readonly HttpClient Http = new()
    {
        Timeout = TimeSpan.FromSeconds(30),
    };

    private readonly Uri _manifestUri;

    public PortableUpdateClient(Uri manifestUri)
    {
        if (manifestUri.Scheme != Uri.UriSchemeHttps)
        {
            throw new ArgumentException("The update manifest URL must use HTTPS.", nameof(manifestUri));
        }

        _manifestUri = manifestUri;
    }

    public Version CurrentVersion => PortableUpdater.CurrentVersion;

    public async Task<PortableUpdateManifest?> CheckForUpdateAsync(
        CancellationToken cancellationToken = default)
    {
        using var response = await Http.GetAsync(_manifestUri, cancellationToken);
        response.EnsureSuccessStatusCode();
        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        var manifest = await JsonSerializer.DeserializeAsync<PortableUpdateManifest>(
            stream,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true },
            cancellationToken)
            ?? throw new InvalidDataException("The update manifest is empty.");

        manifest.Validate();
        return manifest.ParsedVersion > CurrentVersion ? manifest : null;
    }

    public async Task<string> DownloadAsync(
        PortableUpdateManifest manifest,
        IProgress<int>? progress = null,
        CancellationToken cancellationToken = default)
    {
        manifest.Validate();
        var updateDirectory = PortableUpdater.UpdateDirectory;
        Directory.CreateDirectory(updateDirectory);
        var destination = Path.Combine(
            updateDirectory,
            $"VirtualPilotNetwork-{manifest.ParsedVersion}-{Guid.NewGuid():N}.exe");

        try
        {
            using var response = await Http.GetAsync(
                manifest.DownloadUri,
                HttpCompletionOption.ResponseHeadersRead,
                cancellationToken);
            response.EnsureSuccessStatusCode();
            if (response.Content.Headers.ContentLength is long contentLength
                && contentLength != manifest.Size)
            {
                throw new InvalidDataException(
                    $"The update size did not match the manifest ({contentLength} instead of {manifest.Size} bytes).");
            }

            long total = 0;
            await using (var source = await response.Content.ReadAsStreamAsync(cancellationToken))
            await using (var target = new FileStream(
                destination,
                FileMode.CreateNew,
                FileAccess.Write,
                FileShare.None,
                81920,
                FileOptions.Asynchronous | FileOptions.SequentialScan))
            {
                var buffer = new byte[81920];
                int read;
                while ((read = await source.ReadAsync(buffer, cancellationToken)) > 0)
                {
                    await target.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
                    total += read;
                    progress?.Report((int)Math.Min(100, total * 100 / manifest.Size));
                }

                await target.FlushAsync(cancellationToken);
            }

            if (total != manifest.Size)
            {
                throw new InvalidDataException(
                    $"The downloaded update was {total} bytes; {manifest.Size} bytes were expected.");
            }

            var actualHash = await PortableUpdater.CalculateSha256Async(destination, cancellationToken);
            if (!CryptographicOperations.FixedTimeEquals(
                    Convert.FromHexString(actualHash),
                    Convert.FromHexString(manifest.NormalizedSha256)))
            {
                throw new InvalidDataException("The downloaded update failed SHA-256 verification.");
            }

            progress?.Report(100);
            return destination;
        }
        catch
        {
            TryDelete(destination);
            throw;
        }
    }

    private static void TryDelete(string path)
    {
        try
        {
            File.Delete(path);
        }
        catch
        {
            // A later startup cleanup will retry.
        }
    }
}

internal static class PortableUpdater
{
    private const string ApplyArgument = "--portable-apply";

    public static string UpdateDirectory => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "VirtualPilotNetwork",
        "updates");

    public static Version CurrentVersion =>
        Assembly.GetEntryAssembly()?.GetName().Version ?? new Version(0, 0, 0);

    public static Version ParseVersion(string value)
    {
        var normalized = value.Trim().TrimStart('v', 'V');
        if (!Version.TryParse(normalized, out var version) || version.Major < 0)
        {
            throw new InvalidDataException($"The update version '{value}' is invalid.");
        }

        return version;
    }

    public static async Task<string> CalculateSha256Async(
        string path,
        CancellationToken cancellationToken = default)
    {
        await using var stream = new FileStream(
            path,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            81920,
            FileOptions.Asynchronous | FileOptions.SequentialScan);
        var hash = await SHA256.HashDataAsync(stream, cancellationToken);
        return Convert.ToHexString(hash);
    }

    public static void BeginApply(string downloadedExecutable, string expectedSha256)
    {
        var target = Environment.ProcessPath
            ?? throw new InvalidOperationException("The application executable path is unavailable.");
        var startInfo = new ProcessStartInfo(downloadedExecutable)
        {
            UseShellExecute = false,
            WorkingDirectory = Path.GetDirectoryName(downloadedExecutable),
        };
        startInfo.ArgumentList.Add(ApplyArgument);
        startInfo.ArgumentList.Add(Environment.ProcessId.ToString());
        startInfo.ArgumentList.Add(target);
        startInfo.ArgumentList.Add(expectedSha256);
        _ = Process.Start(startInfo)
            ?? throw new InvalidOperationException("The downloaded updater could not be started.");
    }

    public static bool TryApplyPendingUpdate(string[] args, out int exitCode)
    {
        exitCode = 0;
        if (args.Length == 0 || !args[0].Equals(ApplyArgument, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (args.Length != 4 || !int.TryParse(args[1], out var parentProcessId))
        {
            exitCode = 2;
            return true;
        }

        try
        {
            ApplyPendingUpdate(parentProcessId, args[2], args[3]);
        }
        catch (Exception exception)
        {
            MessageBox.Show(
                $"The update could not be installed. The existing application was not changed.\n\n{exception.Message}",
                "Virtual Pilot Network — update failed",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
            TryLaunch(args[2]);
            exitCode = 1;
        }

        return true;
    }

    public static void CleanupDownloads()
    {
        try
        {
            if (!Directory.Exists(UpdateDirectory)) return;
            foreach (var file in Directory.EnumerateFiles(UpdateDirectory, "*.exe"))
            {
                try
                {
                    File.Delete(file);
                }
                catch
                {
                    // The just-finished updater may still be shutting down.
                }
            }
        }
        catch
        {
            // Cleanup must never prevent the application from starting.
        }
    }

    private static void ApplyPendingUpdate(
        int parentProcessId,
        string targetPath,
        string expectedSha256)
    {
        var sourcePath = Path.GetFullPath(
            Environment.ProcessPath
            ?? throw new InvalidOperationException("The updater executable path is unavailable."));
        var target = Path.GetFullPath(targetPath);
        if (!target.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)
            || !File.Exists(target)
            || sourcePath.Equals(target, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("The update target is invalid.");
        }

        var actualHash = CalculateSha256Async(sourcePath).GetAwaiter().GetResult();
        var normalizedExpected = expectedSha256.Trim().ToUpperInvariant();
        if (normalizedExpected.Length != 64
            || !CryptographicOperations.FixedTimeEquals(
                Convert.FromHexString(actualHash),
                Convert.FromHexString(normalizedExpected)))
        {
            throw new InvalidDataException("The updater failed its final SHA-256 verification.");
        }

        WaitForProcess(parentProcessId);

        var targetDirectory = Path.GetDirectoryName(target)
            ?? throw new InvalidOperationException("The update target directory is invalid.");
        var stagedPath = Path.Combine(targetDirectory, $".{Path.GetFileName(target)}.{Guid.NewGuid():N}.update");
        try
        {
            File.Copy(sourcePath, stagedPath, overwrite: false);
            Exception? lastError = null;
            for (var attempt = 0; attempt < 40; attempt++)
            {
                try
                {
                    File.Move(stagedPath, target, overwrite: true);
                    TryLaunch(target);
                    return;
                }
                catch (IOException exception)
                {
                    lastError = exception;
                    Thread.Sleep(250);
                }
                catch (UnauthorizedAccessException exception)
                {
                    lastError = exception;
                    Thread.Sleep(250);
                }
            }

            throw new IOException("Windows did not release the previous executable in time.", lastError);
        }
        finally
        {
            try
            {
                File.Delete(stagedPath);
            }
            catch
            {
                // Best effort; this only remains after a failed update.
            }
        }
    }

    private static void WaitForProcess(int processId)
    {
        try
        {
            using var process = Process.GetProcessById(processId);
            if (!process.WaitForExit(30_000))
            {
                throw new TimeoutException("The running application did not close within 30 seconds.");
            }
        }
        catch (ArgumentException)
        {
            // The parent already exited.
        }
    }

    private static void TryLaunch(string executable)
    {
        Process.Start(new ProcessStartInfo(executable)
        {
            UseShellExecute = true,
            WorkingDirectory = Path.GetDirectoryName(executable),
        });
    }
}
