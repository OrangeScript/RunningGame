using System;
using System.Collections.Generic;

namespace Running.Game.RunMap
{
    /// <summary>
    /// Pure C# authoritative vote state machine. It owns no clock, Unity object or
    /// transport: callers provide server time and relay its immutable events.
    /// </summary>
    public sealed class RunMapVoteResolver
    {
        private readonly int tieBreakSeed;
        private readonly HashSet<int> candidateNodeIds = new HashSet<int>();
        private readonly HashSet<ulong> eligiblePlayerIds = new HashSet<ulong>();
        private readonly Dictionary<ulong, int> choicesByPlayer =
            new Dictionary<ulong, int>();

        private int roundId = -1;
        private bool hasRound;
        private bool isActive;
        private double openedAt;
        private double deadline;
        private RunMapVoteSnapshot currentSnapshot = RunMapVoteSnapshot.Empty;

        public event Action<RunMapVoteSnapshot> SnapshotChanged;
        public event Action<RunMapVoteResult> Resolved;

        public int RoundId => roundId;
        public bool IsActive => isActive;
        public RunMapVoteSnapshot CurrentSnapshot => currentSnapshot;

        public RunMapVoteResolver(int tieBreakSeed)
        {
            this.tieBreakSeed = tieBreakSeed;
        }

        public void BeginRound(
            int newRoundId,
            IEnumerable<int> candidateNodeIds,
            IEnumerable<ulong> eligiblePlayerIds,
            double serverNow,
            double durationSeconds)
        {
            ValidateFinite(serverNow, nameof(serverNow));
            ValidateFinite(durationSeconds, nameof(durationSeconds));

            if (durationSeconds <= 0d)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(durationSeconds),
                    "Vote duration must be positive.");
            }

            if (newRoundId < 0 || (hasRound && newRoundId <= roundId))
            {
                throw new ArgumentOutOfRangeException(
                    nameof(newRoundId),
                    "Round IDs must be non-negative and strictly increasing.");
            }

            if (candidateNodeIds == null)
            {
                throw new ArgumentNullException(nameof(candidateNodeIds));
            }

            if (eligiblePlayerIds == null)
            {
                throw new ArgumentNullException(nameof(eligiblePlayerIds));
            }

            this.candidateNodeIds.Clear();
            foreach (int nodeId in candidateNodeIds)
            {
                if (nodeId < 0)
                {
                    throw new ArgumentOutOfRangeException(
                        nameof(candidateNodeIds),
                        "Candidate node IDs cannot be negative.");
                }

                if (!this.candidateNodeIds.Add(nodeId))
                {
                    throw new ArgumentException(
                        $"Candidate node {nodeId} was supplied more than once.",
                        nameof(candidateNodeIds));
                }
            }

            if (this.candidateNodeIds.Count == 0)
            {
                throw new ArgumentException(
                    "At least one candidate node is required.",
                    nameof(candidateNodeIds));
            }

            this.eligiblePlayerIds.Clear();
            foreach (ulong playerId in eligiblePlayerIds)
            {
                if (!this.eligiblePlayerIds.Add(playerId))
                {
                    throw new ArgumentException(
                        $"Eligible player {playerId} was supplied more than once.",
                        nameof(eligiblePlayerIds));
                }
            }

            choicesByPlayer.Clear();
            roundId = newRoundId;
            hasRound = true;
            isActive = true;
            openedAt = serverNow;
            deadline = serverNow + durationSeconds;
            ValidateFinite(deadline, nameof(durationSeconds));

            PublishSnapshot();

            if (this.eligiblePlayerIds.Count == 0)
            {
                ResolveWithoutWinner(RunMapVoteResolutionReason.NoEligiblePlayers);
            }
        }

        public bool TrySubmitVote(
            ulong playerId,
            int submittedRoundId,
            int nodeId,
            double serverNow,
            out RunMapVoteRejectReason rejectReason)
        {
            ValidateFinite(serverNow, nameof(serverNow));

            if (!isActive)
            {
                rejectReason = RunMapVoteRejectReason.NoActiveRound;
                return false;
            }

            if (submittedRoundId != roundId)
            {
                rejectReason = RunMapVoteRejectReason.WrongRound;
                return false;
            }

            if (serverNow >= deadline)
            {
                ResolveWithWinner(RunMapVoteResolutionReason.Timeout);
                rejectReason = RunMapVoteRejectReason.RoundExpired;
                return false;
            }

            if (!eligiblePlayerIds.Contains(playerId))
            {
                rejectReason = RunMapVoteRejectReason.PlayerNotEligible;
                return false;
            }

            if (!candidateNodeIds.Contains(nodeId))
            {
                rejectReason = RunMapVoteRejectReason.NodeNotReachable;
                return false;
            }

            choicesByPlayer[playerId] = nodeId;
            rejectReason = RunMapVoteRejectReason.None;
            PublishSnapshot();

            if (choicesByPlayer.Count == eligiblePlayerIds.Count)
            {
                ResolveWithWinner(RunMapVoteResolutionReason.AllPlayersVoted);
            }

            return true;
        }

        /// <summary>
        /// Removes a disconnected player and their vote. If all remaining eligible
        /// players have already voted, the round resolves immediately.
        /// </summary>
        public bool RemovePlayer(ulong playerId)
        {
            if (!isActive || !eligiblePlayerIds.Remove(playerId))
            {
                return false;
            }

            choicesByPlayer.Remove(playerId);
            PublishSnapshot();

            if (eligiblePlayerIds.Count == 0)
            {
                ResolveWithoutWinner(RunMapVoteResolutionReason.NoEligiblePlayers);
            }
            else if (choicesByPlayer.Count == eligiblePlayerIds.Count)
            {
                ResolveWithWinner(RunMapVoteResolutionReason.AllPlayersVoted);
            }

            return true;
        }

        public bool Tick(double serverNow)
        {
            ValidateFinite(serverNow, nameof(serverNow));
            if (!isActive || serverNow < deadline)
            {
                return false;
            }

            ResolveWithWinner(RunMapVoteResolutionReason.Timeout);
            return true;
        }

        private void ResolveWithWinner(RunMapVoteResolutionReason reason)
        {
            if (!isActive)
            {
                return;
            }

            Dictionary<int, int> tallies = BuildTallies();
            int maximumVotes = -1;
            List<int> finalists = new List<int>();
            foreach (KeyValuePair<int, int> pair in tallies)
            {
                if (pair.Value > maximumVotes)
                {
                    maximumVotes = pair.Value;
                    finalists.Clear();
                    finalists.Add(pair.Key);
                }
                else if (pair.Value == maximumVotes)
                {
                    finalists.Add(pair.Key);
                }
            }

            finalists.Sort();
            int winner = SelectDeterministicTieWinner(finalists);
            isActive = false;
            PublishSnapshot();
            Resolved?.Invoke(new RunMapVoteResult(roundId, true, winner, reason, finalists));
        }

        private void ResolveWithoutWinner(RunMapVoteResolutionReason reason)
        {
            if (!isActive)
            {
                return;
            }

            isActive = false;
            PublishSnapshot();
            Resolved?.Invoke(new RunMapVoteResult(
                roundId,
                false,
                -1,
                reason,
                Array.Empty<int>()));
        }

        private int SelectDeterministicTieWinner(IReadOnlyList<int> finalists)
        {
            if (finalists == null || finalists.Count == 0)
            {
                throw new InvalidOperationException("A vote cannot resolve without candidates.");
            }

            int winner = finalists[0];
            ulong winnerScore = CalculateTieScore(winner);
            for (int index = 1; index < finalists.Count; index++)
            {
                int candidate = finalists[index];
                ulong score = CalculateTieScore(candidate);
                if (score < winnerScore || (score == winnerScore && candidate < winner))
                {
                    winner = candidate;
                    winnerScore = score;
                }
            }

            return winner;
        }

        private ulong CalculateTieScore(int nodeId)
        {
            // SplitMix-style avalanche over only stable integer inputs. The lowest
            // score wins; node ID is a final collision fallback.
            unchecked
            {
                ulong value = (ulong)(uint)tieBreakSeed;
                value ^= (ulong)(uint)roundId * 0x9E3779B185EBCA87UL;
                value ^= (ulong)(uint)nodeId * 0xC2B2AE3D27D4EB4FUL;
                value ^= value >> 30;
                value *= 0xBF58476D1CE4E5B9UL;
                value ^= value >> 27;
                value *= 0x94D049BB133111EBUL;
                return value ^ (value >> 31);
            }
        }

        private Dictionary<int, int> BuildTallies()
        {
            Dictionary<int, int> tallies = new Dictionary<int, int>();
            foreach (int nodeId in candidateNodeIds)
            {
                tallies.Add(nodeId, 0);
            }

            foreach (int nodeId in choicesByPlayer.Values)
            {
                tallies[nodeId]++;
            }

            return tallies;
        }

        private void PublishSnapshot()
        {
            Dictionary<int, int> tallies = BuildTallies();
            List<int> sortedCandidateIds = new List<int>(candidateNodeIds);
            sortedCandidateIds.Sort();
            List<RunMapCandidateVoteSnapshot> candidates =
                new List<RunMapCandidateVoteSnapshot>(sortedCandidateIds.Count);
            foreach (int nodeId in sortedCandidateIds)
            {
                candidates.Add(new RunMapCandidateVoteSnapshot(nodeId, tallies[nodeId]));
            }

            List<ulong> sortedPlayerIds = new List<ulong>(eligiblePlayerIds);
            sortedPlayerIds.Sort();
            List<RunMapPlayerVoteSnapshot> playerVotes =
                new List<RunMapPlayerVoteSnapshot>(sortedPlayerIds.Count);
            foreach (ulong playerId in sortedPlayerIds)
            {
                bool hasVoted = choicesByPlayer.TryGetValue(playerId, out int choice);
                playerVotes.Add(new RunMapPlayerVoteSnapshot(
                    playerId,
                    hasVoted,
                    hasVoted ? choice : -1));
            }

            currentSnapshot = new RunMapVoteSnapshot(
                roundId,
                hasRound,
                isActive,
                openedAt,
                deadline,
                candidates,
                playerVotes);
            SnapshotChanged?.Invoke(currentSnapshot);
        }

        private static void ValidateFinite(double value, string parameterName)
        {
            if (double.IsNaN(value) || double.IsInfinity(value))
            {
                throw new ArgumentOutOfRangeException(
                    parameterName,
                    "Value must be finite.");
            }
        }
    }
}
