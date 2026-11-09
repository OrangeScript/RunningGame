using System;
using UnityEngine;

namespace Running.Game.RunMap
{
    /// <summary>
    /// Serializable generation inputs. A snapshot is deterministic only when both
    /// its seed and these values are identical.
    /// </summary>
    [Serializable]
    public sealed class RunMapGenerationSettings
    {
        private const int NonBossNodeTypeCount = 5;

        [SerializeField, Min(1)]
        private int regularLayerCount = 7;

        [SerializeField, Min(1)]
        private int minNodesPerLayer = 3;

        [SerializeField, Min(1)]
        private int maxNodesPerLayer = 4;

        [SerializeField, Min(1)]
        private int maxOutgoingConnections = 3;

        [SerializeField, Range(0f, 1f)]
        private float extraConnectionChance = 0.35f;

        [Header("Node Weights")]
        [SerializeField, Min(0)] private int combatWeight = 45;
        [SerializeField, Min(0)] private int eliteWeight = 12;
        [SerializeField, Min(0)] private int eventWeight = 18;
        [SerializeField, Min(0)] private int rewardWeight = 15;
        [SerializeField, Min(0)] private int treasureWeight = 10;

        public int RegularLayerCount => regularLayerCount;
        public int MinNodesPerLayer => minNodesPerLayer;
        public int MaxNodesPerLayer => maxNodesPerLayer;
        public int MaxOutgoingConnections => maxOutgoingConnections;
        public float ExtraConnectionChance => extraConnectionChance;
        public int CombatWeight => combatWeight;
        public int EliteWeight => eliteWeight;
        public int EventWeight => eventWeight;
        public int RewardWeight => rewardWeight;
        public int TreasureWeight => treasureWeight;

        public RunMapGenerationSettings()
        {
        }

        public RunMapGenerationSettings(
            int regularLayerCount,
            int minNodesPerLayer,
            int maxNodesPerLayer,
            int maxOutgoingConnections,
            float extraConnectionChance,
            int combatWeight = 45,
            int eliteWeight = 12,
            int eventWeight = 18,
            int rewardWeight = 15,
            int treasureWeight = 10)
        {
            this.regularLayerCount = regularLayerCount;
            this.minNodesPerLayer = minNodesPerLayer;
            this.maxNodesPerLayer = maxNodesPerLayer;
            this.maxOutgoingConnections = maxOutgoingConnections;
            this.extraConnectionChance = extraConnectionChance;
            this.combatWeight = combatWeight;
            this.eliteWeight = eliteWeight;
            this.eventWeight = eventWeight;
            this.rewardWeight = rewardWeight;
            this.treasureWeight = treasureWeight;

            Validate();
        }

        public RunMapGenerationSettings Clone()
        {
            return new RunMapGenerationSettings(
                regularLayerCount,
                minNodesPerLayer,
                maxNodesPerLayer,
                maxOutgoingConnections,
                extraConnectionChance,
                combatWeight,
                eliteWeight,
                eventWeight,
                rewardWeight,
                treasureWeight);
        }

        /// <summary>Throws a descriptive exception for an impossible configuration.</summary>
        public void Validate()
        {
            if (regularLayerCount < 1)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(regularLayerCount),
                    "At least one regular layer is required before the boss layer.");
            }

            if (minNodesPerLayer < 1)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(minNodesPerLayer),
                    "Every regular layer must contain at least one node.");
            }

            if (maxNodesPerLayer < minNodesPerLayer)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(maxNodesPerLayer),
                    "MaxNodesPerLayer cannot be smaller than MinNodesPerLayer.");
            }

            if (maxNodesPerLayer == int.MaxValue ||
                (long)regularLayerCount * maxNodesPerLayer >= int.MaxValue)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(maxNodesPerLayer),
                    "The configured map is too large for stable Int32 node IDs.");
            }

            if (maxOutgoingConnections < 1)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(maxOutgoingConnections),
                    "Every non-boss node needs at least one outgoing connection.");
            }

            if ((long)minNodesPerLayer * maxOutgoingConnections < maxNodesPerLayer)
            {
                throw new ArgumentException(
                    "MaxOutgoingConnections is too small to guarantee that a minimum-size " +
                    "layer can reach every node in a maximum-size next layer.");
            }

            if (extraConnectionChance < 0f || extraConnectionChance > 1f)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(extraConnectionChance),
                    "ExtraConnectionChance must be between 0 and 1.");
            }

            if (combatWeight < 0 || eliteWeight < 0 || eventWeight < 0 ||
                rewardWeight < 0 || treasureWeight < 0)
            {
                throw new ArgumentOutOfRangeException(
                    "nodeWeights",
                    "Node weights cannot be negative.");
            }

            long totalWeight =
                (long)combatWeight + eliteWeight + eventWeight + rewardWeight + treasureWeight;

            if (totalWeight <= 0 || totalWeight > int.MaxValue)
            {
                throw new ArgumentOutOfRangeException(
                    "nodeWeights",
                    "At least one positive node weight is required and the sum must fit in Int32.");
            }

            // Generation guarantees that all supported non-boss types occur at least once.
            // Reject settings for which that promise would be mathematically impossible.
            long minimumRegularNodeCount = (long)regularLayerCount * minNodesPerLayer;
            if (minimumRegularNodeCount < NonBossNodeTypeCount)
            {
                throw new ArgumentException(
                    $"The minimum map size must contain at least {NonBossNodeTypeCount} " +
                    "regular nodes so every supported non-boss node type can appear.");
            }
        }
    }
}
