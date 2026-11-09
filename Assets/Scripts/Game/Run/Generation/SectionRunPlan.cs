using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Unity.Netcode;
using UnityEngine;

namespace Running.Game.Run.Generation
{
    public sealed class PlannedSection
    {
        public PlannedSection(
            int sequenceIndex,
            int contentSeed,
            string definitionId,
            global::SectionType type,
            global::LevelSection prefab)
        {
            SequenceIndex = sequenceIndex;
            ContentSeed = contentSeed;
            DefinitionId = definitionId ?? string.Empty;
            Type = type;
            Prefab = prefab;
        }

        public int SequenceIndex { get; }
        public int ContentSeed { get; }
        public string DefinitionId { get; }
        public global::SectionType Type { get; }
        public global::LevelSection Prefab { get; }
    }

    public sealed class LaneRunPlan
    {
        private readonly ReadOnlyCollection<PlannedSection> sections;

        public LaneRunPlan(
            int laneIndex,
            int seed,
            Vector3 worldOrigin,
            Quaternion worldRotation,
            IList<PlannedSection> sections)
        {
            LaneIndex = laneIndex;
            Seed = seed;
            WorldOrigin = worldOrigin;
            WorldRotation = worldRotation;
            this.sections = new List<PlannedSection>(
                sections ?? Array.Empty<PlannedSection>()).AsReadOnly();
        }

        public int LaneIndex { get; }
        public int Seed { get; }
        public Vector3 WorldOrigin { get; }
        public Quaternion WorldRotation { get; }
        public IReadOnlyList<PlannedSection> Sections => sections;
    }

    /// <summary>
    /// Immutable output of generation. Reusing the same seed and catalog creates
    /// the same lane and section sequence; the same instance can also be replayed.
    /// </summary>
    public sealed class SectionRunPlan
    {
        private readonly ReadOnlyCollection<LaneRunPlan> lanes;

        public SectionRunPlan(
            int seed,
            Vector3 baseOrigin,
            Quaternion baseRotation,
            float laneSpacing,
            IList<LaneRunPlan> lanes)
        {
            Seed = seed;
            BaseOrigin = baseOrigin;
            BaseRotation = baseRotation;
            LaneSpacing = laneSpacing;
            this.lanes = new List<LaneRunPlan>(
                lanes ?? Array.Empty<LaneRunPlan>()).AsReadOnly();
        }

        public int Seed { get; }
        public Vector3 BaseOrigin { get; }
        public Quaternion BaseRotation { get; }
        public float LaneSpacing { get; }
        public int LaneCount => lanes.Count;
        public IReadOnlyList<LaneRunPlan> Lanes => lanes;

        public bool TryGetLane(int laneIndex, out LaneRunPlan lane)
        {
            if (laneIndex >= 0 && laneIndex < lanes.Count)
            {
                lane = lanes[laneIndex];
                return true;
            }

            lane = null;
            return false;
        }
    }

    /// <summary>
    /// Atomic network header for a generated plan. Clients can rebuild the
    /// read-only plan from this header and their shared section catalog.
    /// </summary>
    public struct RunPlanHeader : INetworkSerializable, IEquatable<RunPlanHeader>
    {
        public int Seed;
        public byte LaneCount;
        public uint Revision;

        public RunPlanHeader(int seed, int laneCount, uint revision)
        {
            Seed = seed;
            LaneCount = (byte)Mathf.Clamp(laneCount, 0, 4);
            Revision = revision;
        }

        public bool HasPlan => LaneCount > 0;

        public void NetworkSerialize<T>(BufferSerializer<T> serializer)
            where T : IReaderWriter
        {
            serializer.SerializeValue(ref Seed);
            serializer.SerializeValue(ref LaneCount);
            serializer.SerializeValue(ref Revision);
        }

        public bool Equals(RunPlanHeader other)
        {
            return Seed == other.Seed &&
                   LaneCount == other.LaneCount &&
                   Revision == other.Revision;
        }

        public override bool Equals(object obj)
        {
            return obj is RunPlanHeader other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = Seed;
                hash = (hash * 397) ^ LaneCount;
                hash = (hash * 397) ^ (int)Revision;
                return hash;
            }
        }
    }
}
