using System;
using System.Collections.Generic;

namespace Running.Game.RunMap
{
    /// <summary>
    /// Small SplitMix64 generator whose output is explicitly defined here instead
    /// of relying on System.Random implementation details across runtime versions.
    /// </summary>
    internal struct DeterministicRunMapRandom
    {
        private ulong state;

        public DeterministicRunMapRandom(int seed)
        {
            state = unchecked((ulong)(long)seed) ^ 0xD1B54A32D192ED03UL;
        }

        public ulong NextUInt64()
        {
            unchecked
            {
                state += 0x9E3779B97F4A7C15UL;
                ulong value = state;
                value = (value ^ (value >> 30)) * 0xBF58476D1CE4E5B9UL;
                value = (value ^ (value >> 27)) * 0x94D049BB133111EBUL;
                return value ^ (value >> 31);
            }
        }

        public int NextInt(int minInclusive, int maxExclusive)
        {
            if (maxExclusive <= minInclusive)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(maxExclusive),
                    "maxExclusive must be greater than minInclusive.");
            }

            uint range = checked((uint)(maxExclusive - minInclusive));
            // Rejection sampling prevents modulo bias while remaining deterministic.
            ulong limit = ulong.MaxValue - (ulong.MaxValue % range);
            ulong value;
            do
            {
                value = NextUInt64();
            }
            while (value >= limit);

            return minInclusive + (int)(value % range);
        }

        public bool NextChance(float probability)
        {
            if (probability <= 0f)
            {
                return false;
            }

            if (probability >= 1f)
            {
                return true;
            }

            // Use the upper 53 bits to construct a stable [0, 1) double.
            double sample = (NextUInt64() >> 11) * (1.0 / 9007199254740992.0);
            return sample < probability;
        }

        public void Shuffle<T>(IList<T> values)
        {
            if (values == null)
            {
                throw new ArgumentNullException(nameof(values));
            }

            for (int index = values.Count - 1; index > 0; index--)
            {
                int other = NextInt(0, index + 1);
                T temporary = values[index];
                values[index] = values[other];
                values[other] = temporary;
            }
        }
    }
}
