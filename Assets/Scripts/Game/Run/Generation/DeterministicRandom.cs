using System;

namespace Running.Game.Run.Generation
{
    /// <summary>
    /// Small, platform-stable PRNG used for replayable run generation.
    /// Do not replace this with UnityEngine.Random: its global state would make
    /// a plan depend on unrelated gameplay calls.
    /// </summary>
    public struct DeterministicRandom
    {
        private uint state;

        public DeterministicRandom(int seed)
        {
            state = Mix(unchecked((uint)seed));
            if (state == 0u)
            {
                state = 0x6D2B79F5u;
            }
        }

        public uint NextUInt()
        {
            // xorshift32. A non-zero state is guaranteed by the constructor.
            uint value = state;
            value ^= value << 13;
            value ^= value >> 17;
            value ^= value << 5;
            state = value;
            return value;
        }

        public int NextInt(int exclusiveMax)
        {
            if (exclusiveMax <= 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(exclusiveMax),
                    "The exclusive maximum must be positive.");
            }

            return (int)(NextUInt() % (uint)exclusiveMax);
        }

        public double NextUnitDouble()
        {
            return NextUInt() / ((double)uint.MaxValue + 1d);
        }

        public int NextSeed()
        {
            return unchecked((int)NextUInt());
        }

        public static int DeriveSeed(int rootSeed, int salt)
        {
            uint combined = unchecked((uint)rootSeed);
            combined ^= unchecked((uint)salt) + 0x9E3779B9u +
                        (combined << 6) + (combined >> 2);
            uint result = Mix(combined);
            return unchecked((int)(result == 0u ? 0x6D2B79F5u : result));
        }

        private static uint Mix(uint value)
        {
            value ^= value >> 16;
            value *= 0x7FEB352Du;
            value ^= value >> 15;
            value *= 0x846CA68Bu;
            value ^= value >> 16;
            return value;
        }
    }
}
