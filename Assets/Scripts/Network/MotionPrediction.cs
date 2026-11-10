using System.Collections.Generic;
using UnityEngine;

/// <summary>Fixed-step inputs shared by both pawn modes. Sequence acknowledgements
/// remove latency from the owner without allowing a client to author its position.</summary>
public sealed class MotionPrediction
{
    public const float Step = 1f / 60f;
    private readonly List<ArenaInputFrame> pending = new List<ArenaInputFrame>(128);
    private readonly Queue<ArenaInputFrame> recent = new Queue<ArenaInputFrame>(4);
    public IReadOnlyList<ArenaInputFrame> Pending => pending;
    public void Record(ArenaInputFrame frame)
    {
        if (pending.Count >= 256) pending.RemoveAt(0);
        pending.Add(frame);
        recent.Enqueue(frame);
        while (recent.Count > 4) recent.Dequeue();
    }
    public ArenaInputFrame[] RedundantBatch() => recent.ToArray();
    public void Acknowledge(ushort sequence)
    {
        pending.RemoveAll(frame => !ArenaProtocol.IsSequenceNewer(frame.Sequence,sequence));
    }
    public void Clear() { pending.Clear(); recent.Clear(); }
}

/// <summary>Bounds how far incoming commands can advance authoritative time.</summary>
public sealed class MovementInputBudget
{
    private double lastTime;
    private float tokens = 6;
    public void Reset(double now){lastTime=now;tokens=6;}
    public bool Consume(double now)
    {
        if (lastTime == 0) lastTime = now;
        tokens = Mathf.Min(18,tokens+(float)System.Math.Max(0,now-lastTime)*60);
        lastTime = now;
        if (tokens < 1) return false;
        tokens -= 1;
        return true;
    }
}
