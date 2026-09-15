using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using PCareer.Client.Models;

namespace PCareer.Client.Services;

public sealed record PersistedDesktopSession(
    DesktopSession Session,
    DateTimeOffset PersistUntil);

public sealed class DesktopSessionStore
{
    private static readonly byte[] Entropy = Encoding.UTF8.GetBytes(
        "VirtualPilotNetwork.DesktopSession.v1");
    private static readonly TimeSpan MaximumPersistence = TimeSpan.FromDays(7);
    private readonly string _path;
    private readonly JsonSerializerOptions _json = new(JsonSerializerDefaults.Web);

    public DesktopSessionStore(string? path = null)
    {
        _path = path ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Virtual Pilot Network",
            "desktop-session.dat");
    }

    public PersistedDesktopSession? Load()
    {
        if (!File.Exists(_path))
        {
            return null;
        }

        try
        {
            var encrypted = File.ReadAllBytes(_path);
            var serialized = ProtectedData.Unprotect(
                encrypted,
                Entropy,
                DataProtectionScope.CurrentUser);
            var persisted = JsonSerializer.Deserialize<PersistedDesktopSession>(serialized, _json);
            if (persisted is null
                || string.IsNullOrWhiteSpace(persisted.Session.RefreshToken)
                || persisted.PersistUntil <= DateTimeOffset.UtcNow
                || persisted.Session.RefreshExpiresAt <= DateTimeOffset.UtcNow)
            {
                Clear();
                return null;
            }

            return persisted;
        }
        catch (CryptographicException)
        {
            Clear();
            return null;
        }
        catch (JsonException)
        {
            Clear();
            return null;
        }
        catch (IOException)
        {
            return null;
        }
        catch (UnauthorizedAccessException)
        {
            return null;
        }
    }

    public DateTimeOffset Save(
        DesktopSession session,
        DateTimeOffset? existingDeadline = null)
    {
        var persistUntil = existingDeadline ?? DateTimeOffset.UtcNow + MaximumPersistence;
        persistUntil = new[] { persistUntil, session.RefreshExpiresAt }.Min();
        var serialized = JsonSerializer.SerializeToUtf8Bytes(
            new PersistedDesktopSession(session, persistUntil),
            _json);
        var encrypted = ProtectedData.Protect(
            serialized,
            Entropy,
            DataProtectionScope.CurrentUser);

        var directory = Path.GetDirectoryName(_path)
            ?? throw new InvalidOperationException("The session storage path has no directory.");
        Directory.CreateDirectory(directory);
        var temporaryPath = $"{_path}.{Guid.NewGuid():N}.tmp";
        try
        {
            File.WriteAllBytes(temporaryPath, encrypted);
            File.Move(temporaryPath, _path, overwrite: true);
        }
        finally
        {
            if (File.Exists(temporaryPath))
            {
                File.Delete(temporaryPath);
            }
        }

        return persistUntil;
    }

    public void Clear()
    {
        try
        {
            File.Delete(_path);
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }
}
