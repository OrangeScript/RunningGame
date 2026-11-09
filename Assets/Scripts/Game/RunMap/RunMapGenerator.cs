using System;
using System.Collections.Generic;

namespace Running.Game.RunMap
{
    /// <summary>
    /// Generates a layered DAG. Every node has a path from an entry node and a path
    /// to the unique final boss. Identical seed/settings always produce identical data.
    /// </summary>
    public sealed class RunMapGenerator
    {
        private static readonly RunMapNodeType[] RequiredRegularTypes =
        {
            RunMapNodeType.Combat,
            RunMapNodeType.Elite,
            RunMapNodeType.Event,
            RunMapNodeType.Reward,
            RunMapNodeType.Treasure
        };

        public RunMapSnapshot Generate(int seed, RunMapGenerationSettings settings)
        {
            if (settings == null)
            {
                throw new ArgumentNullException(nameof(settings));
            }

            settings.Validate();
            RunMapGenerationSettings stableSettings = settings.Clone();
            DeterministicRunMapRandom random = new DeterministicRunMapRandom(seed);

            List<int[]> layers = BuildLayerNodeIds(stableSettings, ref random, out int bossNodeId);
            List<RunMapNodeType> regularTypes =
                BuildRegularNodeTypes(layers, stableSettings, ref random);
            List<RunMapNodeSnapshot> nodes =
                BuildNodes(layers, bossNodeId, regularTypes, ref random);
            List<RunMapConnectionSnapshot> connections =
                BuildConnections(layers, stableSettings, ref random);

            return new RunMapSnapshot(seed, bossNodeId, nodes, connections);
        }

        private static List<int[]> BuildLayerNodeIds(
            RunMapGenerationSettings settings,
            ref DeterministicRunMapRandom random,
            out int bossNodeId)
        {
            List<int[]> layers = new List<int[]>(settings.RegularLayerCount + 1);
            int nextNodeId = 0;

            for (int layer = 0; layer < settings.RegularLayerCount; layer++)
            {
                int count = random.NextInt(
                    settings.MinNodesPerLayer,
                    checked(settings.MaxNodesPerLayer + 1));
                int[] nodeIds = new int[count];
                for (int column = 0; column < count; column++)
                {
                    nodeIds[column] = nextNodeId++;
                }

                layers.Add(nodeIds);
            }

            bossNodeId = nextNodeId;
            layers.Add(new[] { bossNodeId });
            return layers;
        }

        private static List<RunMapNodeType> BuildRegularNodeTypes(
            IReadOnlyList<int[]> layers,
            RunMapGenerationSettings settings,
            ref DeterministicRunMapRandom random)
        {
            int regularNodeCount = 0;
            for (int layer = 0; layer < settings.RegularLayerCount; layer++)
            {
                regularNodeCount += layers[layer].Length;
            }

            List<RunMapNodeType> types = new List<RunMapNodeType>(regularNodeCount);
            types.AddRange(RequiredRegularTypes);
            while (types.Count < regularNodeCount)
            {
                types.Add(PickWeightedType(settings, ref random));
            }

            // Shuffling distributes the guaranteed type set without coupling types
            // to node ID order.
            random.Shuffle(types);
            return types;
        }

        private static List<RunMapNodeSnapshot> BuildNodes(
            IReadOnlyList<int[]> layers,
            int bossNodeId,
            IReadOnlyList<RunMapNodeType> regularTypes,
            ref DeterministicRunMapRandom random)
        {
            List<RunMapNodeSnapshot> nodes = new List<RunMapNodeSnapshot>();
            int typeIndex = 0;

            for (int layer = 0; layer < layers.Count; layer++)
            {
                int[] nodeIds = layers[layer];
                for (int column = 0; column < nodeIds.Length; column++)
                {
                    int nodeId = nodeIds[column];
                    RunMapNodeType type = nodeId == bossNodeId
                        ? RunMapNodeType.Boss
                        : regularTypes[typeIndex++];
                    int contentSeed = unchecked((int)(random.NextUInt64() & 0x7FFFFFFF));
                    nodes.Add(new RunMapNodeSnapshot(nodeId, layer, column, type, contentSeed));
                }
            }

            return nodes;
        }

        private static List<RunMapConnectionSnapshot> BuildConnections(
            IReadOnlyList<int[]> layers,
            RunMapGenerationSettings settings,
            ref DeterministicRunMapRandom random)
        {
            HashSet<ulong> keys = new HashSet<ulong>();
            Dictionary<int, int> outgoingCounts = new Dictionary<int, int>();

            for (int layer = 0; layer < layers.Count - 1; layer++)
            {
                List<int> fromNodes = new List<int>(layers[layer]);
                List<int> toNodes = new List<int>(layers[layer + 1]);
                // Preserve column order. Randomly shuffling both layers made
                // long crossing lines that obscured every route decision.

                // First cover every destination so every node is reachable.
                for (int index = 0; index < toNodes.Count; index++)
                {
                    int sourceIndex = toNodes.Count <= 1 ? fromNodes.Count / 2 :
                        (int)Math.Round(index * (fromNodes.Count - 1d) / (toNodes.Count - 1d));
                    int from = fromNodes[sourceIndex];
                    AddConnection(keys, outgoingCounts, from, toNodes[index]);
                }

                // Then cover every source so every node can progress toward the boss.
                for (int index = 0; index < fromNodes.Count; index++)
                {
                    int from = fromNodes[index];
                    if (!outgoingCounts.TryGetValue(from, out int count) || count == 0)
                    {
                        int destinationIndex = fromNodes.Count <= 1 ? toNodes.Count / 2 :
                            (int)Math.Round(index * (toNodes.Count - 1d) / (fromNodes.Count - 1d));
                        AddConnection(keys, outgoingCounts, from, toNodes[destinationIndex]);
                    }
                }

                // Finally add deterministic variety without violating the per-node cap.
                foreach (int from in fromNodes)
                {
                    List<int> candidates = new List<int>(toNodes);
                    random.Shuffle(candidates);
                    foreach (int to in candidates)
                    {
                        int count = outgoingCounts[from];
                        if (count >= settings.MaxOutgoingConnections)
                        {
                            break;
                        }

                        if (ContainsConnection(keys, from, to) ||
                            CrossesExistingConnection(keys, fromNodes, toNodes, from, to) ||
                            !random.NextChance(settings.ExtraConnectionChance))
                        {
                            continue;
                        }

                        AddConnection(keys, outgoingCounts, from, to);
                    }
                }
            }

            List<RunMapConnectionSnapshot> result = new List<RunMapConnectionSnapshot>(keys.Count);
            foreach (ulong key in keys)
            {
                result.Add(new RunMapConnectionSnapshot(
                    unchecked((int)(key >> 32)),
                    unchecked((int)(key & 0xFFFFFFFFUL))));
            }

            result.Sort((left, right) =>
            {
                int fromComparison = left.FromNodeId.CompareTo(right.FromNodeId);
                return fromComparison != 0
                    ? fromComparison
                    : left.ToNodeId.CompareTo(right.ToNodeId);
            });
            return result;
        }

        private static RunMapNodeType PickWeightedType(
            RunMapGenerationSettings settings,
            ref DeterministicRunMapRandom random)
        {
            int total = checked(
                settings.CombatWeight +
                settings.EliteWeight +
                settings.EventWeight +
                settings.RewardWeight +
                settings.TreasureWeight);
            int roll = random.NextInt(0, total);

            if ((roll -= settings.CombatWeight) < 0)
            {
                return RunMapNodeType.Combat;
            }

            if ((roll -= settings.EliteWeight) < 0)
            {
                return RunMapNodeType.Elite;
            }

            if ((roll -= settings.EventWeight) < 0)
            {
                return RunMapNodeType.Event;
            }

            if ((roll -= settings.RewardWeight) < 0)
            {
                return RunMapNodeType.Reward;
            }

            return RunMapNodeType.Treasure;
        }

        private static bool ContainsConnection(HashSet<ulong> keys, int fromNodeId, int toNodeId)
        {
            return keys.Contains(CreateConnectionKey(fromNodeId, toNodeId));
        }

        private static bool CrossesExistingConnection(
            HashSet<ulong> keys,List<int> sources,List<int> destinations,int from,int to)
        {
            foreach (int otherFrom in sources)
                foreach (int otherTo in destinations)
                    if (ContainsConnection(keys,otherFrom,otherTo) &&
                        ((otherFrom < from && otherTo > to) || (otherFrom > from && otherTo < to)))
                        return true;
            return false;
        }

        private static void AddConnection(
            HashSet<ulong> keys,
            Dictionary<int, int> outgoingCounts,
            int fromNodeId,
            int toNodeId)
        {
            if (!keys.Add(CreateConnectionKey(fromNodeId, toNodeId)))
            {
                return;
            }

            outgoingCounts.TryGetValue(fromNodeId, out int count);
            outgoingCounts[fromNodeId] = count + 1;
        }

        private static ulong CreateConnectionKey(int fromNodeId, int toNodeId)
        {
            return ((ulong)(uint)fromNodeId << 32) | (uint)toNodeId;
        }
    }
}
