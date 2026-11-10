using System.Collections.Generic;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

public struct RunnerEnemyShot
{
    public uint Id;
    public Vector3 Origin, Direction;
    public float Speed, Radius, Lifetime, LaneCenter;
    public int Damage, LaneId;
    public double Born;
}

/// <summary>
/// Scene-local shot transport. It survives the firing enemy's death, so an
/// already fired bullet and its impact do not depend on a despawned RPC target.
/// </summary>
public sealed class RunnerEnemyProjectileSystem : MonoBehaviour
{
    private const string SpawnMessage = "Running.EnemyShots.Spawn.v1";
    private const string ImpactMessage = "Running.EnemyShots.Impact.v1";
    private const int BatchSize = 16;
    private struct Impact { public uint Id; public Vector3 Position; public double Time; }
    private static RunnerEnemyProjectileSystem instance;
    public static RunnerEnemyProjectileSystem Instance => instance;
    private NetworkManager manager;
    private uint sequence;
    private readonly List<RunnerEnemyShot> pendingSpawns = new List<RunnerEnemyShot>();
    private readonly List<Impact> pendingImpacts = new List<Impact>();
    private readonly Dictionary<uint, RunnerEnemyProjectile> bullets = new Dictionary<uint, RunnerEnemyProjectile>();
    public int ActiveProjectileCount => bullets.Count;
    public int ServerShotCount { get; private set; }
    public int ClientSpawnCount { get; private set; }

    public static RunnerEnemyProjectileSystem Ensure()
    {
        var network = NetworkManager.Singleton;
        if (network == null || !network.IsListening) return null;
        if (instance != null && instance.manager == network) return instance;
        var root = new GameObject("RunnerEnemyProjectiles_Runtime");
        instance = root.AddComponent<RunnerEnemyProjectileSystem>();
        return instance;
    }

    private void Awake()
    {
        manager = NetworkManager.Singleton;
        if (manager == null || manager.CustomMessagingManager == null) return;
        manager.CustomMessagingManager.RegisterNamedMessageHandler(SpawnMessage, ReceiveSpawns);
        manager.CustomMessagingManager.RegisterNamedMessageHandler(ImpactMessage, ReceiveImpacts);
    }

    public void SpawnServer(Enemy source, Vector3 origin, Vector3 direction, float speed, int damage,
        float radius, float lifetime, int laneId, float laneCenter, double launchTime)
    {
        if (manager == null || !manager.IsServer || !manager.IsListening) return;
        direction.y = 0;
        if (!ArenaProtocol.IsFinite(origin) || !ArenaProtocol.IsFinite(direction) || direction.sqrMagnitude < .001f ||
            !ArenaProtocol.IsFinite(speed) || !ArenaProtocol.IsFinite(radius) || !ArenaProtocol.IsFinite(lifetime) ||
            double.IsNaN(launchTime) || double.IsInfinity(launchTime)) return;
        var shot = new RunnerEnemyShot
        {
            Id = ++sequence, Origin = origin, Direction = direction.normalized,
            Speed = Mathf.Clamp(speed, 1, 20), Radius = Mathf.Clamp(radius, .08f, .4f),
            Lifetime = Mathf.Clamp(lifetime, .2f, 8), Damage = Mathf.Clamp(damage, 1, 30),
            LaneId = laneId, LaneCenter = laneCenter, Born = launchTime
        };
        bullets[shot.Id] = RunnerEnemyProjectile.Spawn(this, shot, true, source);
        pendingSpawns.Add(shot); ServerShotCount++;
    }

    public void ImpactServer(uint id, Vector3 position, double time)
    {
        if (manager == null || !manager.IsServer) return;
        if (bullets.TryGetValue(id, out var bullet)) bullet.StopAt(position, time);
        pendingImpacts.Add(new Impact { Id = id, Position = position, Time = time });
    }

    public void Forget(uint id) { bullets.Remove(id); }
    public void ClearServer()
    {
        if (manager == null || !manager.IsServer) return;
        double now = manager.ServerTime.Time;
        foreach (var pair in bullets)
            if (pair.Value != null) ImpactServer(pair.Key, pair.Value.transform.position, now);
    }

    private void LateUpdate()
    {
        if (manager == null || !manager.IsServer || !manager.IsListening) return;
        // Always preserve spawn -> impact ordering, including a burst cancelled
        // before its scheduled launch because the gunner was killed/frozen.
        for (int offset = 0; offset < pendingSpawns.Count; offset += BatchSize)
        {
            using (var writer = new FastBufferWriter(1100, Allocator.Temp))
            {
                int count = Mathf.Min(BatchSize, pendingSpawns.Count - offset); writer.WriteValueSafe(count);
                for (int i = offset; i < offset + count; i++) WriteShot(writer, pendingSpawns[i]);
                manager.CustomMessagingManager.SendNamedMessageToAll(SpawnMessage, writer, NetworkDelivery.ReliableSequenced);
            }
        }
        pendingSpawns.Clear();
        for (int offset = 0; offset < pendingImpacts.Count; offset += BatchSize)
        {
            using (var writer = new FastBufferWriter(512, Allocator.Temp))
            {
                int count = Mathf.Min(BatchSize, pendingImpacts.Count - offset); writer.WriteValueSafe(count);
                for (int i = offset; i < offset + count; i++)
                {
                    var impact = pendingImpacts[i];
                    writer.WriteValueSafe(impact.Id); writer.WriteValueSafe(impact.Position); writer.WriteValueSafe(impact.Time);
                }
                manager.CustomMessagingManager.SendNamedMessageToAll(ImpactMessage, writer, NetworkDelivery.ReliableSequenced);
            }
        }
        pendingImpacts.Clear();
    }

    private static void WriteShot(FastBufferWriter writer, RunnerEnemyShot shot)
    {
        writer.WriteValueSafe(shot.Id); writer.WriteValueSafe(shot.Origin); writer.WriteValueSafe(shot.Direction);
        writer.WriteValueSafe(shot.Speed); writer.WriteValueSafe(shot.Radius); writer.WriteValueSafe(shot.Lifetime);
        writer.WriteValueSafe(shot.Damage); writer.WriteValueSafe(shot.LaneId);
        writer.WriteValueSafe(shot.LaneCenter); writer.WriteValueSafe(shot.Born);
    }
    private static RunnerEnemyShot ReadShot(ref FastBufferReader reader)
    {
        var shot = new RunnerEnemyShot();
        reader.ReadValueSafe(out shot.Id); reader.ReadValueSafe(out shot.Origin); reader.ReadValueSafe(out shot.Direction);
        reader.ReadValueSafe(out shot.Speed); reader.ReadValueSafe(out shot.Radius); reader.ReadValueSafe(out shot.Lifetime);
        reader.ReadValueSafe(out shot.Damage); reader.ReadValueSafe(out shot.LaneId);
        reader.ReadValueSafe(out shot.LaneCenter); reader.ReadValueSafe(out shot.Born);
        return shot;
    }
    private void ReceiveSpawns(ulong sender, FastBufferReader reader)
    {
        if (manager.IsServer || sender != NetworkManager.ServerClientId) return;
        reader.ReadValueSafe(out int count); if (count < 0 || count > BatchSize) return;
        for (int i = 0; i < count; i++)
        {
            var shot = ReadShot(ref reader);
            if (bullets.ContainsKey(shot.Id)) continue;
            bullets[shot.Id] = RunnerEnemyProjectile.Spawn(this, shot, false, null); ClientSpawnCount++;
        }
    }
    private void ReceiveImpacts(ulong sender, FastBufferReader reader)
    {
        if (manager.IsServer || sender != NetworkManager.ServerClientId) return;
        reader.ReadValueSafe(out int count); if (count < 0 || count > BatchSize) return;
        for (int i = 0; i < count; i++)
        {
            reader.ReadValueSafe(out uint id); reader.ReadValueSafe(out Vector3 position); reader.ReadValueSafe(out double time);
            if (bullets.TryGetValue(id, out var bullet)) bullet.StopAt(position, time);
        }
    }
    private void OnDestroy()
    {
        if (manager != null && manager.CustomMessagingManager != null)
        {
            manager.CustomMessagingManager.UnregisterNamedMessageHandler(SpawnMessage);
            manager.CustomMessagingManager.UnregisterNamedMessageHandler(ImpactMessage);
        }
        if (instance == this) instance = null;
    }
}
