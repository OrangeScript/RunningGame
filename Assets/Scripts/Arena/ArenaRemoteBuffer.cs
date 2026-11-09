using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

/// <summary>
/// Bounded interpolation of the single authoritative snapshot stream.
/// Only remote pawns play back; the local owner uses input prediction.
/// </summary>
[DisallowMultipleComponent]
public class ArenaRemoteBuffer : MonoBehaviour
{
    [Header("Playback")]
    [SerializeField] private Transform presentationTarget;
    [SerializeField, Min(0f)] private float interpolationDelay = 0.05f;
    [SerializeField, Range(2, 64)] private int capacity = 24;
    [SerializeField, Min(0f)] private float teleportDistance = 6f;
    [SerializeField] private bool applyPosition = true;
    [SerializeField] private bool applyRotation = true;

    private readonly List<ArenaStateSnapshot> snapshots = new List<ArenaStateSnapshot>(24);
    private bool playbackEnabled;

    public int Count => snapshots.Count;
    public bool PlaybackEnabled => playbackEnabled;

    private void Awake()
    {
        if (presentationTarget == null)
        {
            presentationTarget = transform;
        }
    }

    private void Update()
    {
        if (!playbackEnabled || snapshots.Count == 0)
        {
            return;
        }

        double synchronizedServerTime = GetSynchronizedServerTime();
        Render(synchronizedServerTime - interpolationDelay);
    }

    public void SetPlaybackEnabled(bool enabledForRemote)
    {
        playbackEnabled = enabledForRemote;
        if (!enabledForRemote)
        {
            Clear();
        }
    }

    public void SetPresentationTarget(Transform target)
    {
        presentationTarget = target != null ? target : transform;
        Clear();
    }

    public void Push(ArenaStateSnapshot snapshot)
    {
        if (!playbackEnabled ||
            !ArenaProtocol.IsFinite(snapshot.Position) ||
            !ArenaProtocol.IsFinite(snapshot.Forward) ||
            !ArenaProtocol.IsFinite(snapshot.Velocity) ||
            double.IsNaN(snapshot.ServerTime) ||
            double.IsInfinity(snapshot.ServerTime))
        {
            return;
        }

        if (snapshots.Count > 0)
        {
            ArenaStateSnapshot newest = snapshots[snapshots.Count - 1];
            if (snapshot.ServerTime <= newest.ServerTime)
            {
                return;
            }

            float teleportThreshold = Mathf.Max(0f, teleportDistance);
            if (teleportThreshold > 0f &&
                (snapshot.Position - newest.Position).sqrMagnitude >
                teleportThreshold * teleportThreshold)
            {
                snapshots.Clear();
                Apply(snapshot);
            }
        }

        int safeCapacity = Mathf.Clamp(capacity, 2, 64);
        if (snapshots.Count >= safeCapacity)
        {
            snapshots.RemoveAt(0);
        }

        snapshots.Add(snapshot);
    }

    public void Clear()
    {
        snapshots.Clear();
    }

    public void SnapTo(ArenaStateSnapshot snapshot)
    {
        snapshots.Clear();
        snapshots.Add(snapshot);
        Apply(snapshot);
    }

    private void Render(double playbackTime)
    {
        while (snapshots.Count >= 2 && snapshots[1].ServerTime <= playbackTime)
        {
            snapshots.RemoveAt(0);
        }

        if (snapshots.Count == 1 || playbackTime <= snapshots[0].ServerTime)
        {
            Apply(snapshots[0]);
            return;
        }

        ArenaStateSnapshot from = snapshots[0];
        ArenaStateSnapshot to = snapshots[1];
        double duration = to.ServerTime - from.ServerTime;
        float t = duration > double.Epsilon
            ? Mathf.Clamp01((float)((playbackTime - from.ServerTime) / duration))
            : 1f;

        ArenaStateSnapshot interpolated = new ArenaStateSnapshot
        {
            ServerTime = playbackTime,
            Position = Vector3.LerpUnclamped(from.Position, to.Position, t),
            Forward = Vector3.Slerp(from.Forward, to.Forward, t),
            Velocity = Vector3.LerpUnclamped(from.Velocity, to.Velocity, t),
            LastProcessedInput = to.LastProcessedInput
        };

        Apply(interpolated);
    }

    private void Apply(ArenaStateSnapshot snapshot)
    {
        Transform target = presentationTarget != null ? presentationTarget : transform;
        if (applyPosition)
        {
            target.position = snapshot.Position;
        }

        if (applyRotation)
        {
            Vector3 forward = snapshot.Forward;
            forward.y = 0f;
            if (forward.sqrMagnitude > 0.0001f)
            {
                target.rotation = Quaternion.LookRotation(forward.normalized, Vector3.up);
            }
        }
    }

    private static double GetSynchronizedServerTime()
    {
        NetworkManager manager = NetworkManager.Singleton;
        if (manager != null && manager.IsListening)
        {
            return manager.ServerTime.Time;
        }

        return Time.unscaledTimeAsDouble;
    }

    private void OnValidate()
    {
        capacity = Mathf.Clamp(capacity, 2, 64);
        interpolationDelay = Mathf.Max(0f, interpolationDelay);
        teleportDistance = Mathf.Max(0f, teleportDistance);
    }
}
