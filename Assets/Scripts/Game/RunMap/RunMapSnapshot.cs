using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using UnityEngine;

namespace Running.Game.RunMap
{
    [Serializable]
    public sealed class RunMapNodeSnapshot
    {
        [SerializeField] private int id;
        [SerializeField] private int layer;
        [SerializeField] private int column;
        [SerializeField] private RunMapNodeType type;
        [SerializeField] private int contentSeed;

        public int Id => id;
        public int Layer => layer;
        public int Column => column;
        public RunMapNodeType Type => type;
        public int ContentSeed => contentSeed;

        public RunMapNodeSnapshot()
        {
        }

        public RunMapNodeSnapshot(
            int id,
            int layer,
            int column,
            RunMapNodeType type,
            int contentSeed)
        {
            this.id = id;
            this.layer = layer;
            this.column = column;
            this.type = type;
            this.contentSeed = contentSeed;
        }

        public RunMapNodeSnapshot Clone()
        {
            return new RunMapNodeSnapshot(id, layer, column, type, contentSeed);
        }
    }

    [Serializable]
    public sealed class RunMapConnectionSnapshot
    {
        [SerializeField] private int fromNodeId;
        [SerializeField] private int toNodeId;

        public int FromNodeId => fromNodeId;
        public int ToNodeId => toNodeId;

        public RunMapConnectionSnapshot()
        {
        }

        public RunMapConnectionSnapshot(int fromNodeId, int toNodeId)
        {
            this.fromNodeId = fromNodeId;
            this.toNodeId = toNodeId;
        }

        public RunMapConnectionSnapshot Clone()
        {
            return new RunMapConnectionSnapshot(fromNodeId, toNodeId);
        }
    }

    /// <summary>
    /// Serializable, self-contained save/network representation of a generated map.
    /// Public collections are exposed through read-only wrappers.
    /// </summary>
    [Serializable]
    public sealed class RunMapSnapshot
    {
        public const int CurrentFormatVersion = 1;

        [SerializeField] private int formatVersion = CurrentFormatVersion;
        [SerializeField] private int seed;
        [SerializeField] private int bossNodeId = -1;
        [SerializeField] private List<RunMapNodeSnapshot> nodes = new List<RunMapNodeSnapshot>();
        [SerializeField] private List<RunMapConnectionSnapshot> connections =
            new List<RunMapConnectionSnapshot>();

        [NonSerialized] private Dictionary<int, RunMapNodeSnapshot> nodeLookup;
        [NonSerialized] private Dictionary<int, int[]> outgoingLookup;
        [NonSerialized] private ReadOnlyCollection<RunMapNodeSnapshot> readOnlyNodes;
        [NonSerialized] private ReadOnlyCollection<RunMapConnectionSnapshot> readOnlyConnections;

        public int FormatVersion => formatVersion;
        public int Seed => seed;
        public int BossNodeId => bossNodeId;
        public IReadOnlyList<RunMapNodeSnapshot> Nodes =>
            readOnlyNodes ?? (readOnlyNodes = SafeNodes.AsReadOnly());
        public IReadOnlyList<RunMapConnectionSnapshot> Connections =>
            readOnlyConnections ?? (readOnlyConnections = SafeConnections.AsReadOnly());

        private List<RunMapNodeSnapshot> SafeNodes =>
            nodes ?? (nodes = new List<RunMapNodeSnapshot>());
        private List<RunMapConnectionSnapshot> SafeConnections =>
            connections ?? (connections = new List<RunMapConnectionSnapshot>());

        public RunMapSnapshot()
        {
        }

        public RunMapSnapshot(
            int seed,
            int bossNodeId,
            IEnumerable<RunMapNodeSnapshot> nodes,
            IEnumerable<RunMapConnectionSnapshot> connections)
        {
            if (nodes == null)
            {
                throw new ArgumentNullException(nameof(nodes));
            }

            if (connections == null)
            {
                throw new ArgumentNullException(nameof(connections));
            }

            formatVersion = CurrentFormatVersion;
            this.seed = seed;
            this.bossNodeId = bossNodeId;
            this.nodes = CloneNodes(nodes);
            this.connections = CloneConnections(connections);

            ValidateOrThrow();
        }

        public bool TryGetNode(int nodeId, out RunMapNodeSnapshot node)
        {
            EnsureCaches();
            return nodeLookup.TryGetValue(nodeId, out node);
        }

        public IReadOnlyList<int> GetEntryNodeIds()
        {
            EnsureCaches();
            List<int> result = new List<int>();
            foreach (RunMapNodeSnapshot node in SafeNodes)
            {
                if (node != null && node.Layer == 0)
                {
                    result.Add(node.Id);
                }
            }

            result.Sort();
            return Array.AsReadOnly(result.ToArray());
        }

        public IReadOnlyList<int> GetOutgoingNodeIds(int nodeId)
        {
            EnsureCaches();
            if (!nodeLookup.ContainsKey(nodeId))
            {
                throw new ArgumentOutOfRangeException(nameof(nodeId), "Unknown run-map node ID.");
            }

            return outgoingLookup.TryGetValue(nodeId, out int[] values)
                ? Array.AsReadOnly((int[])values.Clone())
                : Array.AsReadOnly(Array.Empty<int>());
        }

        public RunMapSnapshot DeepCopy()
        {
            return new RunMapSnapshot(seed, bossNodeId, SafeNodes, SafeConnections);
        }

        public void ValidateOrThrow()
        {
            if (!TryValidate(out string error))
            {
                throw new InvalidOperationException($"Invalid run-map snapshot: {error}");
            }
        }

        public bool TryValidate(out string error)
        {
            InvalidateCaches();

            if (formatVersion != CurrentFormatVersion)
            {
                error = $"Unsupported format version {formatVersion}.";
                return false;
            }

            if (SafeNodes.Count == 0)
            {
                error = "The map contains no nodes.";
                return false;
            }

            Dictionary<int, RunMapNodeSnapshot> byId = new Dictionary<int, RunMapNodeSnapshot>();
            Dictionary<int, List<int>> nodesByLayer = new Dictionary<int, List<int>>();
            int maximumLayer = -1;
            int bossCount = 0;

            foreach (RunMapNodeSnapshot node in SafeNodes)
            {
                if (node == null)
                {
                    error = "A node entry is null.";
                    return false;
                }

                if (node.Id < 0 || node.Layer < 0 || node.Column < 0)
                {
                    error = $"Node {node.Id} contains a negative ID, layer or column.";
                    return false;
                }

                if (!Enum.IsDefined(typeof(RunMapNodeType), node.Type))
                {
                    error = $"Node {node.Id} has an unknown type value.";
                    return false;
                }

                if (!byId.TryAdd(node.Id, node))
                {
                    error = $"Duplicate node ID {node.Id}.";
                    return false;
                }

                if (!nodesByLayer.TryGetValue(node.Layer, out List<int> layerNodes))
                {
                    layerNodes = new List<int>();
                    nodesByLayer.Add(node.Layer, layerNodes);
                }

                layerNodes.Add(node.Id);
                maximumLayer = Math.Max(maximumLayer, node.Layer);

                if (node.Type == RunMapNodeType.Boss)
                {
                    bossCount++;
                    if (node.Id != bossNodeId)
                    {
                        error = "BossNodeId does not identify the boss node.";
                        return false;
                    }
                }
            }

            if (bossCount != 1 || !byId.TryGetValue(bossNodeId, out RunMapNodeSnapshot boss))
            {
                error = "The map must contain exactly one valid boss node.";
                return false;
            }

            if (boss.Layer != maximumLayer || nodesByLayer[maximumLayer].Count != 1)
            {
                error = "The unique boss must be the only node in the final layer.";
                return false;
            }

            for (int layer = 0; layer <= maximumLayer; layer++)
            {
                if (!nodesByLayer.TryGetValue(layer, out List<int> layerNodes) || layerNodes.Count == 0)
                {
                    error = $"Layer {layer} is empty or missing.";
                    return false;
                }

                HashSet<int> columns = new HashSet<int>();
                foreach (int nodeId in layerNodes)
                {
                    if (!columns.Add(byId[nodeId].Column))
                    {
                        error = $"Layer {layer} contains duplicate column values.";
                        return false;
                    }
                }
            }

            Dictionary<int, List<int>> outgoing = new Dictionary<int, List<int>>();
            Dictionary<int, List<int>> incoming = new Dictionary<int, List<int>>();
            HashSet<ulong> connectionKeys = new HashSet<ulong>();

            foreach (RunMapConnectionSnapshot connection in SafeConnections)
            {
                if (connection == null)
                {
                    error = "A connection entry is null.";
                    return false;
                }

                if (!byId.TryGetValue(connection.FromNodeId, out RunMapNodeSnapshot from) ||
                    !byId.TryGetValue(connection.ToNodeId, out RunMapNodeSnapshot to))
                {
                    error = "A connection references an unknown node.";
                    return false;
                }

                if (to.Layer != from.Layer + 1)
                {
                    error = $"Connection {from.Id}->{to.Id} does not join adjacent layers.";
                    return false;
                }

                ulong key = ((ulong)(uint)from.Id << 32) | (uint)to.Id;
                if (!connectionKeys.Add(key))
                {
                    error = $"Duplicate connection {from.Id}->{to.Id}.";
                    return false;
                }

                AddLookupValue(outgoing, from.Id, to.Id);
                AddLookupValue(incoming, to.Id, from.Id);
            }

            foreach (RunMapNodeSnapshot node in SafeNodes)
            {
                if (node.Layer > 0 && !incoming.ContainsKey(node.Id))
                {
                    error = $"Node {node.Id} is not reachable from the previous layer.";
                    return false;
                }

                if (node.Id != bossNodeId && !outgoing.ContainsKey(node.Id))
                {
                    error = $"Node {node.Id} cannot progress toward the boss.";
                    return false;
                }

                if (node.Id == bossNodeId && outgoing.ContainsKey(node.Id))
                {
                    error = "The boss node cannot have outgoing connections.";
                    return false;
                }
            }

            // Because connections only join adjacent layers, the incoming/outgoing
            // checks above prove every node is reachable from layer zero and can
            // eventually reach the boss.
            error = null;
            return true;
        }

        private void EnsureCaches()
        {
            if (nodeLookup != null && outgoingLookup != null)
            {
                return;
            }

            if (!TryValidate(out string error))
            {
                throw new InvalidOperationException($"Invalid run-map snapshot: {error}");
            }

            nodeLookup = new Dictionary<int, RunMapNodeSnapshot>();
            foreach (RunMapNodeSnapshot node in SafeNodes)
            {
                nodeLookup.Add(node.Id, node);
            }

            Dictionary<int, List<int>> temporary = new Dictionary<int, List<int>>();
            foreach (RunMapConnectionSnapshot connection in SafeConnections)
            {
                AddLookupValue(temporary, connection.FromNodeId, connection.ToNodeId);
            }

            outgoingLookup = new Dictionary<int, int[]>();
            foreach (KeyValuePair<int, List<int>> pair in temporary)
            {
                pair.Value.Sort();
                outgoingLookup.Add(pair.Key, pair.Value.ToArray());
            }
        }

        private void InvalidateCaches()
        {
            nodeLookup = null;
            outgoingLookup = null;
            readOnlyNodes = null;
            readOnlyConnections = null;
        }

        private static void AddLookupValue(
            Dictionary<int, List<int>> lookup,
            int key,
            int value)
        {
            if (!lookup.TryGetValue(key, out List<int> values))
            {
                values = new List<int>();
                lookup.Add(key, values);
            }

            values.Add(value);
        }

        private static List<RunMapNodeSnapshot> CloneNodes(
            IEnumerable<RunMapNodeSnapshot> source)
        {
            List<RunMapNodeSnapshot> result = new List<RunMapNodeSnapshot>();
            foreach (RunMapNodeSnapshot node in source)
            {
                if (node == null)
                {
                    throw new ArgumentException("Node collection cannot contain null.", nameof(source));
                }

                result.Add(node.Clone());
            }

            return result;
        }

        private static List<RunMapConnectionSnapshot> CloneConnections(
            IEnumerable<RunMapConnectionSnapshot> source)
        {
            List<RunMapConnectionSnapshot> result = new List<RunMapConnectionSnapshot>();
            foreach (RunMapConnectionSnapshot connection in source)
            {
                if (connection == null)
                {
                    throw new ArgumentException(
                        "Connection collection cannot contain null.",
                        nameof(source));
                }

                result.Add(connection.Clone());
            }

            return result;
        }
    }
}
