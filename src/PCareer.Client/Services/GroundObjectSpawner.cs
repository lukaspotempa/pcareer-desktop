using PCareer.Client.Models;

namespace PCareer.Client.Services;

public sealed class GroundObjectSpawner
{
    private const double MetersPerFoot = 0.3048d;
    private static readonly TimeSpan SpawnTimeout = TimeSpan.FromSeconds(20);
    private readonly ISimulatorConnection _simulator;
    private readonly Dictionary<string, Task<uint>> _pendingSpawns = new(StringComparer.Ordinal);
    private readonly Dictionary<string, SpawnedGroundObject> _spawned = new(StringComparer.Ordinal);

    public GroundObjectSpawner(ISimulatorConnection simulator) => _simulator = simulator;

    public bool IsEnabled => _simulator.SupportsObjectSpawning;

    public string? LastSpawnError { get; private set; }

    public IReadOnlyCollection<SpawnedGroundObject> Spawned => _spawned.Values;

    public async Task<bool> TrySpawnAsync(MissionGroundObject groundObject)
    {
        if (_spawned.ContainsKey(groundObject.Id))
        {
            return true;
        }
        if (!_simulator.SupportsObjectSpawning)
        {
            return false;
        }

        if (!_pendingSpawns.TryGetValue(groundObject.Id, out var spawnTask))
        {
            spawnTask = ResolveAndSpawnAsync(groundObject);
            _pendingSpawns[groundObject.Id] = spawnTask;
        }

        try
        {
            var simObjectId = await spawnTask.WaitAsync(SpawnTimeout);
            _pendingSpawns.Remove(groundObject.Id);
            _spawned[groundObject.Id] = new SpawnedGroundObject(
                groundObject.Id,
                groundObject.Title,
                simObjectId);
            if (groundObject.Freeze)
            {
                try
                {
                    _simulator.FreezeGroundObject(simObjectId);
                }
                catch
                {
                }
            }
            return true;
        }
        catch (TimeoutException)
        {
            LastSpawnError = $"Timed out waiting for the simulator to accept “{groundObject.Title}”.";
            return false;
        }
        catch (Exception exception)
        {
            LastSpawnError = $"Could not spawn “{groundObject.Title}”: {exception.Message}";
            _pendingSpawns.Remove(groundObject.Id);
            return false;
        }
    }

    private async Task<uint> ResolveAndSpawnAsync(MissionGroundObject groundObject)
    {
        var installedTitle = await _simulator.ResolveGroundObjectTitleAsync(
            groundObject.ObjectType,
            groundObject.Title);
        return await _simulator.SpawnGroundObjectAsync(
            installedTitle,
            groundObject.LiveryName,
            groundObject.Position.Lat,
            groundObject.Position.Lon,
            groundObject.Position.AltM / MetersPerFoot,
            groundObject.HeadingDegrees);
    }

    public async Task RemoveAsync(IEnumerable<string> objectIds)
    {
        foreach (var objectId in objectIds)
        {
            await RemoveAsync(objectId);
        }
    }

    public async Task RemoveAsync(string objectId)
    {
        if (_spawned.TryGetValue(objectId, out var spawned))
        {
            await RemoveSpawnedAsync(spawned);
            return;
        }

        if (_pendingSpawns.TryGetValue(objectId, out var pending))
        {
            try
            {
                var simObjectId = await pending.WaitAsync(SpawnTimeout);
                await TryRemoveSpawnedAsync(new SpawnedGroundObject(objectId, string.Empty, simObjectId));
            }
            catch (TimeoutException)
            {
                _ = RemoveWhenReadyAsync(pending);
            }
            catch
            {
                // A failed spawn has no simulator object to remove.
            }
            finally
            {
                _pendingSpawns.Remove(objectId);
            }
        }
    }

    public async Task ReleaseAllAsync()
    {
        foreach (var pendingId in _pendingSpawns.Keys.ToArray())
        {
            await RemoveAsync(pendingId);
        }
        foreach (var spawned in _spawned.Values.ToArray())
        {
            await RemoveSpawnedAsync(spawned);
        }
    }

    private async Task RemoveWhenReadyAsync(Task<uint> pending)
    {
        try
        {
            var objectId = await pending;
            await _simulator.RemoveGroundObjectAsync(objectId);
        }
        catch
        {
            // Disconnecting also destroys objects owned by this connection.
        }
    }

    private async Task RemoveSpawnedAsync(SpawnedGroundObject spawned)
    {
        _spawned.Remove(spawned.ObjectId);
        await TryRemoveSpawnedAsync(spawned);
    }

    private async Task TryRemoveSpawnedAsync(SpawnedGroundObject spawned)
    {
        try
        {
            await _simulator.RemoveGroundObjectAsync(spawned.SimObjectId);
        }
        catch
        {
        }
    }
}
