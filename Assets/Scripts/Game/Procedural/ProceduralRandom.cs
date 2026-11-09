using System;
using UnityEngine;

/// <summary>
/// Small deterministic PRNG used for gameplay plans. It never touches
/// UnityEngine.Random, so a seed can be replayed without perturbing VFX.
/// </summary>
public struct ProceduralRandom
{
    private uint state;

    public ProceduralRandom(int seed)
    {
        state = unchecked((uint)seed);
        if (state == 0)
            state = 0x9E3779B9u;
    }

    public uint NextUInt()
    {
        uint x = state;
        x ^= x << 13;
        x ^= x >> 17;
        x ^= x << 5;
        state = x == 0 ? 0xA341316Cu : x;
        return state;
    }

    public int Range(int minimumInclusive, int maximumExclusive)
    {
        if (maximumExclusive <= minimumInclusive)
            return minimumInclusive;

        uint span = (uint)(maximumExclusive - minimumInclusive);
        return minimumInclusive + (int)(NextUInt() % span);
    }

    public float Range(float minimumInclusive, float maximumInclusive)
    {
        if (maximumInclusive <= minimumInclusive)
            return minimumInclusive;

        float unit = (NextUInt() & 0x00FFFFFFu) / 16777215f;
        return Mathf.Lerp(minimumInclusive, maximumInclusive, unit);
    }

    public bool Chance(float probability)
    {
        return Range(0f, 1f) < Mathf.Clamp01(probability);
    }

    public static int Derive(int seed, int saltA, int saltB = 0)
    {
        unchecked
        {
            uint hash = 2166136261u;
            hash = (hash ^ (uint)seed) * 16777619u;
            hash = (hash ^ (uint)saltA) * 16777619u;
            hash = (hash ^ (uint)saltB) * 16777619u;
            return (int)(hash & 0x7FFFFFFF);
        }
    }
}
