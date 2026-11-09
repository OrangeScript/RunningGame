using System;
using Unity.Netcode;
using UnityEngine;

[Flags]
public enum ArenaInputButtons : byte
{
    None = 0,
    Fire = 1 << 0,
    Focus = 1 << 1
}

/// <summary>
/// Compact owner-to-server input. Axes are quantized to signed bytes so the
/// server never has to trust arbitrary floating-point values from a client.
/// </summary>
public struct ArenaInputFrame : INetworkSerializable, IEquatable<ArenaInputFrame>
{
    public ushort Sequence;
    public sbyte MoveX;
    public sbyte MoveY;
    public sbyte AimX;
    public sbyte AimY;
    public ArenaInputButtons Buttons;

    public bool FireHeld => (Buttons & ArenaInputButtons.Fire) != 0;
    public bool FocusHeld => (Buttons & ArenaInputButtons.Focus) != 0;

    public void NetworkSerialize<T>(BufferSerializer<T> serializer)
        where T : IReaderWriter
    {
        serializer.SerializeValue(ref Sequence);
        serializer.SerializeValue(ref MoveX);
        serializer.SerializeValue(ref MoveY);
        serializer.SerializeValue(ref AimX);
        serializer.SerializeValue(ref AimY);

        byte rawButtons = (byte)Buttons;
        serializer.SerializeValue(ref rawButtons);
        if (serializer.IsReader)
        {
            Buttons = (ArenaInputButtons)rawButtons;
        }
    }

    public bool Equals(ArenaInputFrame other)
    {
        return Sequence == other.Sequence &&
               MoveX == other.MoveX &&
               MoveY == other.MoveY &&
               AimX == other.AimX &&
               AimY == other.AimY &&
               Buttons == other.Buttons;
    }
}

/// <summary>
/// The single authoritative pose stream: owners reconcile and replay pending
/// inputs; remote peers interpolate these snapshots.
/// </summary>
public struct ArenaStateSnapshot : INetworkSerializable, IEquatable<ArenaStateSnapshot>
{
    public double ServerTime;
    public Vector3 Position;
    public Vector3 Forward;
    public Vector3 Velocity;
    public ushort LastProcessedInput;

    public void NetworkSerialize<T>(BufferSerializer<T> serializer)
        where T : IReaderWriter
    {
        serializer.SerializeValue(ref ServerTime);
        serializer.SerializeValue(ref Position);
        serializer.SerializeValue(ref Forward);
        serializer.SerializeValue(ref Velocity);
        serializer.SerializeValue(ref LastProcessedInput);
    }

    public bool Equals(ArenaStateSnapshot other)
    {
        return ServerTime.Equals(other.ServerTime) &&
               Position.Equals(other.Position) &&
               Forward.Equals(other.Forward) &&
               Velocity.Equals(other.Velocity) &&
               LastProcessedInput == other.LastProcessedInput;
    }
}

public static class ArenaProtocol
{
    public const int AxisPrecision = 127;
    public const float MinimumAimMagnitude = 0.05f;

    private const ArenaInputButtons AllowedButtons = ArenaInputButtons.Fire | ArenaInputButtons.Focus;

    public static ArenaInputFrame Pack(
        ushort sequence,
        Vector2 move,
        Vector2 aim,
        bool fireHeld)
    {
        move = ClampToUnitCircle(move);
        aim = ClampToUnitCircle(aim);

        if (aim.sqrMagnitude < MinimumAimMagnitude * MinimumAimMagnitude)
        {
            aim = Vector2.up;
        }

        return new ArenaInputFrame
        {
            Sequence = sequence,
            MoveX = QuantizeAxis(move.x),
            MoveY = QuantizeAxis(move.y),
            AimX = QuantizeAxis(aim.x),
            AimY = QuantizeAxis(aim.y),
            Buttons = fireHeld ? ArenaInputButtons.Fire : ArenaInputButtons.None
        };
    }

    public static bool TryDecode(
        ArenaInputFrame frame,
        out Vector2 move,
        out Vector2 aim,
        out bool fireHeld)
    {
        move = ClampToUnitCircle(new Vector2(
            DequantizeAxis(frame.MoveX),
            DequantizeAxis(frame.MoveY)));

        aim = ClampToUnitCircle(new Vector2(
            DequantizeAxis(frame.AimX),
            DequantizeAxis(frame.AimY)));

        fireHeld = frame.FireHeld;

        if ((frame.Buttons & ~AllowedButtons) != 0)
        {
            return false;
        }

        if (!IsFinite(move) || !IsFinite(aim))
        {
            return false;
        }

        if (aim.sqrMagnitude < MinimumAimMagnitude * MinimumAimMagnitude)
        {
            aim = Vector2.up;
        }

        return true;
    }

    /// <summary>
    /// Wrap-safe comparison for monotonically increasing ushort sequences.
    /// A peer cannot advance more than half the ushort range in one step.
    /// </summary>
    public static bool IsSequenceNewer(ushort candidate, ushort previous)
    {
        return candidate != previous && (ushort)(candidate - previous) < 32768;
    }

    public static bool IsFinite(Vector2 value)
    {
        return IsFinite(value.x) && IsFinite(value.y);
    }

    public static bool IsFinite(Vector3 value)
    {
        return IsFinite(value.x) && IsFinite(value.y) && IsFinite(value.z);
    }

    public static bool IsFinite(float value)
    {
        return !float.IsNaN(value) && !float.IsInfinity(value);
    }

    public static Vector2 ClampToUnitCircle(Vector2 value)
    {
        if (!IsFinite(value))
        {
            return Vector2.zero;
        }

        return value.sqrMagnitude > 1f ? value.normalized : value;
    }

    private static sbyte QuantizeAxis(float value)
    {
        int quantized = Mathf.RoundToInt(Mathf.Clamp(value, -1f, 1f) * AxisPrecision);
        return (sbyte)Mathf.Clamp(quantized, -AxisPrecision, AxisPrecision);
    }

    private static float DequantizeAxis(sbyte value)
    {
        return Mathf.Clamp(value / (float)AxisPrecision, -1f, 1f);
    }
}
