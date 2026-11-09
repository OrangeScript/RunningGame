using System;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

namespace Running.Game.RunMap
{
    /// <summary>
    /// Thin NGO adapter around the pure map and vote domain objects.
    ///
    /// The server owns map generation, reachability, membership and timeout. Clients
    /// receive immutable snapshots through events and can only request a vote for a
    /// node in the active reachable set.
    /// </summary>
    public sealed class RunMapNetworkCoordinator : NetworkBehaviour
    {
        [Header("Generation")]
        [SerializeField] private bool initializeOnServerSpawn;
        [SerializeField] private int initialSeed = 1;
        [SerializeField] private RunMapGenerationSettings generationSettings =
            new RunMapGenerationSettings();

        [Header("Voting")]
        [SerializeField, Min(0.1f)] private float defaultVoteDurationSeconds = 20f;
        [SerializeField] private bool beginFirstVoteOnInitialize;

        private readonly RunMapGenerator generator = new RunMapGenerator();
        private readonly List<int> reachableNodeIds = new List<int>();

        private RunMapSnapshot mapSnapshot;
        private RunMapVoteResolver voteResolver;
        private RunMapVoteSnapshot remoteVoteSnapshot = RunMapVoteSnapshot.Empty;
        private int currentNodeId = -1;
        private int nextRoundId;

        public event Action<RunMapSnapshot> MapChanged;
        public event Action<RunMapVoteSnapshot> VoteChanged;
        public event Action<RunMapVoteResult> VoteResolved;
        public event Action<RunMapVoteRejection> VoteRejected;
        public event Action<RunMapNodeSnapshot> CurrentNodeChanged;
        public event Action<IReadOnlyList<int>> ReachableNodesChanged;
        public event Action NoFurtherNodes;

        /// <summary>A defensive copy safe for UI/save consumers.</summary>
        public RunMapSnapshot MapSnapshot => mapSnapshot?.DeepCopy();

        /// <summary>Immutable current vote view. It includes every player's visible choice.</summary>
        public RunMapVoteSnapshot VoteSnapshot =>
            IsServer && voteResolver != null
                ? voteResolver.CurrentSnapshot
                : remoteVoteSnapshot;

        public int CurrentNodeId => currentNodeId;

        public IReadOnlyList<int> ReachableNodeIds =>
            Array.AsReadOnly(reachableNodeIds.ToArray());

        public override void OnNetworkSpawn()
        {
            if (IsServer)
            {
                NetworkManager.OnClientConnectedCallback += HandleClientConnected;
                NetworkManager.OnClientDisconnectCallback += HandleClientDisconnected;

                if (initializeOnServerSpawn)
                {
                    InitializeRun(initialSeed, generationSettings);
                    if (beginFirstVoteOnInitialize)
                    {
                        BeginVoteForReachableNodes(defaultVoteDurationSeconds);
                    }
                }
            }
        }

        public override void OnNetworkDespawn()
        {
            if (NetworkManager != null && IsServer)
            {
                NetworkManager.OnClientConnectedCallback -= HandleClientConnected;
                NetworkManager.OnClientDisconnectCallback -= HandleClientDisconnected;
            }

            DetachVoteResolver();
        }

        private void Update()
        {
            if (IsServer && voteResolver != null && voteResolver.IsActive)
            {
                voteResolver.Tick(GetServerNow());
            }
        }

        /// <summary>
        /// Server-only run initialization. Existing map/vote state is replaced.
        /// Returns false when called without server authority.
        /// </summary>
        public bool InitializeRun(
            int seed,
            RunMapGenerationSettings settingsOverride = null)
        {
            if (!IsServer)
            {
                return false;
            }

            RunMapGenerationSettings settings = settingsOverride ?? generationSettings;
            if (settings == null)
            {
                throw new InvalidOperationException("Run-map generation settings are missing.");
            }

            RunMapSnapshot generated = generator.Generate(seed, settings);
            DetachVoteResolver();

            mapSnapshot = generated;
            voteResolver = new RunMapVoteResolver(seed);
            voteResolver.SnapshotChanged += HandleAuthoritativeVoteChanged;
            voteResolver.Resolved += HandleAuthoritativeVoteResolved;
            remoteVoteSnapshot = RunMapVoteSnapshot.Empty;
            currentNodeId = -1;
            nextRoundId = 0;
            ReplaceReachableNodes(mapSnapshot.GetEntryNodeIds());

            RaiseMapChangedLocally();
            RaiseReachableChangedLocally();
            SendMapSnapshotToClients();
            SendTraversalStateToClients();
            return true;
        }

        /// <summary>
        /// Starts a vote over the current reachable nodes. Call this after the current
        /// encounter is complete. Membership is frozen to currently connected clients.
        /// </summary>
        public bool BeginVoteForReachableNodes(float durationSeconds = -1f)
        {
            if (!IsServer || mapSnapshot == null || voteResolver == null ||
                voteResolver.IsActive)
            {
                return false;
            }

            if (reachableNodeIds.Count == 0)
            {
                NoFurtherNodes?.Invoke();
                NotifyNoFurtherNodesClientRpc();
                return false;
            }

            float duration = durationSeconds > 0f
                ? durationSeconds
                : defaultVoteDurationSeconds;
            if (float.IsNaN(duration) || float.IsInfinity(duration) || duration <= 0f)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(durationSeconds),
                    "Vote duration must be finite and positive.");
            }

            List<ulong> eligiblePlayers =
                new List<ulong>(NetworkManager.ConnectedClientsIds);
            eligiblePlayers.Sort();

            voteResolver.BeginRound(
                nextRoundId++,
                reachableNodeIds,
                eligiblePlayers,
                GetServerNow(),
                duration);
            return true;
        }

        /// <summary>
        /// Client/host UI entry point. Local validation gives immediate feedback;
        /// the server repeats every validation before accepting the request.
        /// </summary>
        public bool RequestVote(int nodeId)
        {
            if (!IsClient)
            {
                return false;
            }

            RunMapVoteSnapshot snapshot = VoteSnapshot;
            if (snapshot == null || !snapshot.IsActive || !snapshot.ContainsCandidate(nodeId))
            {
                return false;
            }

            SubmitVoteServerRpc(snapshot.RoundId, nodeId);
            return true;
        }

        [ServerRpc(RequireOwnership = false)]
        private void SubmitVoteServerRpc(
            int submittedRoundId,
            int nodeId,
            ServerRpcParams rpcParams = default)
        {
            ulong senderClientId = rpcParams.Receive.SenderClientId;
            RunMapVoteRejectReason rejectReason;

            if (voteResolver == null || !voteResolver.IsActive)
            {
                rejectReason = RunMapVoteRejectReason.NoActiveRound;
            }
            else if (submittedRoundId != voteResolver.RoundId)
            {
                rejectReason = RunMapVoteRejectReason.WrongRound;
            }
            else if (!reachableNodeIds.Contains(nodeId) ||
                     !voteResolver.CurrentSnapshot.ContainsCandidate(nodeId))
            {
                // Reachability is server-owned and deliberately checked independently
                // from the candidate list to prevent stale/tampered RPC acceptance.
                rejectReason = RunMapVoteRejectReason.NodeNotReachable;
            }
            else if (voteResolver.TrySubmitVote(
                         senderClientId,
                         submittedRoundId,
                         nodeId,
                         GetServerNow(),
                         out rejectReason))
            {
                return;
            }

            SendVoteRejection(senderClientId, submittedRoundId, nodeId, rejectReason);
        }

        private void HandleAuthoritativeVoteChanged(RunMapVoteSnapshot snapshot)
        {
            VoteChanged?.Invoke(snapshot);
            SendVoteSnapshotToClients(snapshot);
        }

        private void HandleAuthoritativeVoteResolved(RunMapVoteResult result)
        {
            if (result.HasWinner)
            {
                if (mapSnapshot == null ||
                    !reachableNodeIds.Contains(result.WinnerNodeId) ||
                    !mapSnapshot.TryGetNode(result.WinnerNodeId, out RunMapNodeSnapshot selectedNode))
                {
                    Debug.LogError(
                        $"Authoritative vote selected invalid node {result.WinnerNodeId}.",
                        this);
                    return;
                }

                currentNodeId = result.WinnerNodeId;
                ReplaceReachableNodes(mapSnapshot.GetOutgoingNodeIds(currentNodeId));
                CurrentNodeChanged?.Invoke(selectedNode.Clone());
                RaiseReachableChangedLocally();
                SendTraversalStateToClients();
            }

            VoteResolved?.Invoke(result);
            SendVoteResultToClients(result);
        }

        private void HandleClientConnected(ulong clientId)
        {
            if (!IsServer || mapSnapshot == null)
            {
                return;
            }

            ClientRpcParams target = CreateTarget(clientId);
            SendMapSnapshotToClients(target);
            SendTraversalStateToClients(target);

            RunMapVoteSnapshot snapshot = voteResolver?.CurrentSnapshot;
            if (snapshot != null && snapshot.HasRound)
            {
                SendVoteSnapshotToClients(snapshot, target);
            }
        }

        private void HandleClientDisconnected(ulong clientId)
        {
            if (IsServer && voteResolver != null)
            {
                // Resolver removes both eligibility and any previously visible vote.
                voteResolver.RemovePlayer(clientId);
            }
        }

        private void SendMapSnapshotToClients(ClientRpcParams clientRpcParams = default)
        {
            if (!IsServer || mapSnapshot == null)
            {
                return;
            }

            int nodeCount = mapSnapshot.Nodes.Count;
            int[] nodeIds = new int[nodeCount];
            int[] layers = new int[nodeCount];
            int[] columns = new int[nodeCount];
            byte[] types = new byte[nodeCount];
            int[] contentSeeds = new int[nodeCount];
            for (int index = 0; index < nodeCount; index++)
            {
                RunMapNodeSnapshot node = mapSnapshot.Nodes[index];
                nodeIds[index] = node.Id;
                layers[index] = node.Layer;
                columns[index] = node.Column;
                types[index] = (byte)node.Type;
                contentSeeds[index] = node.ContentSeed;
            }

            int connectionCount = mapSnapshot.Connections.Count;
            int[] connectionFrom = new int[connectionCount];
            int[] connectionTo = new int[connectionCount];
            for (int index = 0; index < connectionCount; index++)
            {
                RunMapConnectionSnapshot connection = mapSnapshot.Connections[index];
                connectionFrom[index] = connection.FromNodeId;
                connectionTo[index] = connection.ToNodeId;
            }

            SyncMapClientRpc(
                mapSnapshot.Seed,
                mapSnapshot.BossNodeId,
                nodeIds,
                layers,
                columns,
                types,
                contentSeeds,
                connectionFrom,
                connectionTo,
                clientRpcParams);
        }

        [ClientRpc]
        private void SyncMapClientRpc(
            int seed,
            int bossNodeId,
            int[] nodeIds,
            int[] layers,
            int[] columns,
            byte[] types,
            int[] contentSeeds,
            int[] connectionFrom,
            int[] connectionTo,
            ClientRpcParams clientRpcParams = default)
        {
            if (IsServer)
            {
                return; // The host already applied the authoritative object.
            }

            if (!HaveEqualLength(nodeIds, layers, columns, types, contentSeeds) ||
                connectionFrom == null || connectionTo == null ||
                connectionFrom.Length != connectionTo.Length)
            {
                Debug.LogError("Received malformed run-map arrays from the server.", this);
                return;
            }

            try
            {
                List<RunMapNodeSnapshot> nodes =
                    new List<RunMapNodeSnapshot>(nodeIds.Length);
                for (int index = 0; index < nodeIds.Length; index++)
                {
                    if (!Enum.IsDefined(typeof(RunMapNodeType), types[index]))
                    {
                        throw new InvalidOperationException("Unknown run-map node type received.");
                    }

                    nodes.Add(new RunMapNodeSnapshot(
                        nodeIds[index],
                        layers[index],
                        columns[index],
                        (RunMapNodeType)types[index],
                        contentSeeds[index]));
                }

                List<RunMapConnectionSnapshot> connections =
                    new List<RunMapConnectionSnapshot>(connectionFrom.Length);
                for (int index = 0; index < connectionFrom.Length; index++)
                {
                    connections.Add(new RunMapConnectionSnapshot(
                        connectionFrom[index],
                        connectionTo[index]));
                }

                mapSnapshot = new RunMapSnapshot(seed, bossNodeId, nodes, connections);
                RaiseMapChangedLocally();
            }
            catch (Exception exception)
            {
                Debug.LogError($"Rejected malformed run-map snapshot: {exception.Message}", this);
            }
        }

        private void SendTraversalStateToClients(ClientRpcParams clientRpcParams = default)
        {
            if (!IsServer)
            {
                return;
            }

            SyncTraversalClientRpc(
                currentNodeId,
                reachableNodeIds.ToArray(),
                clientRpcParams);
        }

        [ClientRpc]
        private void SyncTraversalClientRpc(
            int selectedNodeId,
            int[] nextReachableNodeIds,
            ClientRpcParams clientRpcParams = default)
        {
            if (IsServer)
            {
                return;
            }

            if (nextReachableNodeIds == null || mapSnapshot == null)
            {
                Debug.LogError("Traversal state arrived without a valid map snapshot.", this);
                return;
            }

            List<int> validatedReachable = new List<int>(nextReachableNodeIds.Length);
            HashSet<int> unique = new HashSet<int>();
            foreach (int nodeId in nextReachableNodeIds)
            {
                if (!mapSnapshot.TryGetNode(nodeId, out _) || !unique.Add(nodeId))
                {
                    Debug.LogError("Received invalid reachable-node state from server.", this);
                    return;
                }

                validatedReachable.Add(nodeId);
            }

            RunMapNodeSnapshot selectedNode = null;
            if (selectedNodeId >= 0 && !mapSnapshot.TryGetNode(selectedNodeId, out selectedNode))
            {
                Debug.LogError("Received an unknown selected node from server.", this);
                return;
            }

            bool selectedNodeChanged = currentNodeId != selectedNodeId;
            currentNodeId = selectedNodeId;
            ReplaceReachableNodes(validatedReachable);

            if (selectedNodeChanged && selectedNode != null)
            {
                CurrentNodeChanged?.Invoke(selectedNode.Clone());
            }

            RaiseReachableChangedLocally();
        }

        private void SendVoteSnapshotToClients(
            RunMapVoteSnapshot snapshot,
            ClientRpcParams clientRpcParams = default)
        {
            if (!IsServer || snapshot == null)
            {
                return;
            }

            int[] candidates = new int[snapshot.Candidates.Count];
            int[] tallies = new int[snapshot.Candidates.Count];
            for (int index = 0; index < snapshot.Candidates.Count; index++)
            {
                candidates[index] = snapshot.Candidates[index].NodeId;
                tallies[index] = snapshot.Candidates[index].VoteCount;
            }

            ulong[] playerIds = new ulong[snapshot.PlayerVotes.Count];
            int[] choices = new int[snapshot.PlayerVotes.Count];
            for (int index = 0; index < snapshot.PlayerVotes.Count; index++)
            {
                RunMapPlayerVoteSnapshot playerVote = snapshot.PlayerVotes[index];
                playerIds[index] = playerVote.PlayerId;
                choices[index] = playerVote.HasVoted ? playerVote.NodeId : -1;
            }

            SyncVoteClientRpc(
                snapshot.RoundId,
                snapshot.HasRound,
                snapshot.IsActive,
                GetServerNow(),
                snapshot.OpenedAt,
                snapshot.Deadline,
                candidates,
                tallies,
                playerIds,
                choices,
                clientRpcParams);
        }

        [ClientRpc]
        private void SyncVoteClientRpc(
            int syncedRoundId,
            bool hasRound,
            bool isActive,
            double serverNow,
            double serverOpenedAt,
            double serverDeadline,
            int[] candidates,
            int[] tallies,
            ulong[] playerIds,
            int[] choices,
            ClientRpcParams clientRpcParams = default)
        {
            if (IsServer)
            {
                return;
            }

            if (candidates == null || tallies == null || candidates.Length != tallies.Length ||
                playerIds == null || choices == null || playerIds.Length != choices.Length)
            {
                Debug.LogError("Received malformed vote arrays from server.", this);
                return;
            }

            try
            {
                HashSet<int> candidateSet = new HashSet<int>();
                List<RunMapCandidateVoteSnapshot> candidateSnapshots =
                    new List<RunMapCandidateVoteSnapshot>(candidates.Length);
                for (int index = 0; index < candidates.Length; index++)
                {
                    if (!candidateSet.Add(candidates[index]))
                    {
                        throw new InvalidOperationException("Duplicate candidate node received.");
                    }

                    candidateSnapshots.Add(new RunMapCandidateVoteSnapshot(
                        candidates[index],
                        tallies[index]));
                }

                HashSet<ulong> playerSet = new HashSet<ulong>();
                List<RunMapPlayerVoteSnapshot> playerSnapshots =
                    new List<RunMapPlayerVoteSnapshot>(playerIds.Length);
                for (int index = 0; index < playerIds.Length; index++)
                {
                    if (!playerSet.Add(playerIds[index]))
                    {
                        throw new InvalidOperationException("Duplicate eligible player received.");
                    }

                    bool hasVoted = choices[index] >= 0;
                    if (hasVoted && !candidateSet.Contains(choices[index]))
                    {
                        throw new InvalidOperationException("Player choice is not a candidate.");
                    }

                    playerSnapshots.Add(new RunMapPlayerVoteSnapshot(
                        playerIds[index],
                        hasVoted,
                        choices[index]));
                }

                double localNow = Time.realtimeSinceStartupAsDouble;
                double clockOffset = localNow - serverNow;
                remoteVoteSnapshot = new RunMapVoteSnapshot(
                    syncedRoundId,
                    hasRound,
                    isActive,
                    serverOpenedAt + clockOffset,
                    serverDeadline + clockOffset,
                    candidateSnapshots,
                    playerSnapshots);
                VoteChanged?.Invoke(remoteVoteSnapshot);
            }
            catch (Exception exception)
            {
                Debug.LogError($"Rejected malformed vote snapshot: {exception.Message}", this);
            }
        }

        private void SendVoteResultToClients(
            RunMapVoteResult result,
            ClientRpcParams clientRpcParams = default)
        {
            if (!IsServer || result == null)
            {
                return;
            }

            int[] tiedNodeIds = new int[result.TiedNodeIds.Count];
            for (int index = 0; index < tiedNodeIds.Length; index++)
            {
                tiedNodeIds[index] = result.TiedNodeIds[index];
            }

            SyncVoteResultClientRpc(
                result.RoundId,
                result.HasWinner,
                result.WinnerNodeId,
                (byte)result.Reason,
                tiedNodeIds,
                clientRpcParams);
        }

        [ClientRpc]
        private void SyncVoteResultClientRpc(
            int resultRoundId,
            bool hasWinner,
            int winnerNodeId,
            byte reasonValue,
            int[] tiedNodeIds,
            ClientRpcParams clientRpcParams = default)
        {
            if (IsServer)
            {
                return;
            }

            if (!Enum.IsDefined(typeof(RunMapVoteResolutionReason), reasonValue) ||
                tiedNodeIds == null)
            {
                Debug.LogError("Received malformed vote result from server.", this);
                return;
            }

            try
            {
                VoteResolved?.Invoke(new RunMapVoteResult(
                    resultRoundId,
                    hasWinner,
                    winnerNodeId,
                    (RunMapVoteResolutionReason)reasonValue,
                    tiedNodeIds));
            }
            catch (Exception exception)
            {
                Debug.LogError($"Rejected malformed vote result: {exception.Message}", this);
            }
        }

        private void SendVoteRejection(
            ulong clientId,
            int rejectedRoundId,
            int nodeId,
            RunMapVoteRejectReason reason)
        {
            NotifyVoteRejectedClientRpc(
                rejectedRoundId,
                nodeId,
                (byte)reason,
                CreateTarget(clientId));
        }

        [ClientRpc]
        private void NotifyVoteRejectedClientRpc(
            int rejectedRoundId,
            int nodeId,
            byte reasonValue,
            ClientRpcParams clientRpcParams = default)
        {
            if (!Enum.IsDefined(typeof(RunMapVoteRejectReason), reasonValue))
            {
                return;
            }

            VoteRejected?.Invoke(new RunMapVoteRejection(
                rejectedRoundId,
                nodeId,
                (RunMapVoteRejectReason)reasonValue));
        }

        [ClientRpc]
        private void NotifyNoFurtherNodesClientRpc(ClientRpcParams clientRpcParams = default)
        {
            if (!IsServer)
            {
                NoFurtherNodes?.Invoke();
            }
        }

        private void RaiseMapChangedLocally()
        {
            MapChanged?.Invoke(mapSnapshot?.DeepCopy());
        }

        private void RaiseReachableChangedLocally()
        {
            ReachableNodesChanged?.Invoke(Array.AsReadOnly(reachableNodeIds.ToArray()));
        }

        private void ReplaceReachableNodes(IEnumerable<int> nodeIds)
        {
            if (nodeIds == null)
            {
                throw new ArgumentNullException(nameof(nodeIds));
            }

            reachableNodeIds.Clear();
            HashSet<int> unique = new HashSet<int>();
            foreach (int nodeId in nodeIds)
            {
                if (!unique.Add(nodeId))
                {
                    throw new InvalidOperationException(
                        $"Reachable node {nodeId} appears more than once.");
                }

                reachableNodeIds.Add(nodeId);
            }

            reachableNodeIds.Sort();
        }

        private void DetachVoteResolver()
        {
            if (voteResolver == null)
            {
                return;
            }

            voteResolver.SnapshotChanged -= HandleAuthoritativeVoteChanged;
            voteResolver.Resolved -= HandleAuthoritativeVoteResolved;
            voteResolver = null;
        }

        private static bool HaveEqualLength(
            int[] first,
            int[] second,
            int[] third,
            byte[] fourth,
            int[] fifth)
        {
            return first != null && second != null && third != null &&
                   fourth != null && fifth != null &&
                   first.Length == second.Length &&
                   first.Length == third.Length &&
                   first.Length == fourth.Length &&
                   first.Length == fifth.Length;
        }

        private static ClientRpcParams CreateTarget(ulong clientId)
        {
            return new ClientRpcParams
            {
                Send = new ClientRpcSendParams
                {
                    TargetClientIds = new[] { clientId }
                }
            };
        }

        private static double GetServerNow()
        {
            return Time.realtimeSinceStartupAsDouble;
        }
    }
}
