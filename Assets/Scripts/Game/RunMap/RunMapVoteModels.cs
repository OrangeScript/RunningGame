using System;
using System.Collections.Generic;

namespace Running.Game.RunMap
{
    public enum RunMapVoteResolutionReason : byte
    {
        AllPlayersVoted = 0,
        Timeout = 1,
        NoEligiblePlayers = 2
    }

    public enum RunMapVoteRejectReason : byte
    {
        None = 0,
        NoActiveRound = 1,
        WrongRound = 2,
        PlayerNotEligible = 3,
        NodeNotReachable = 4,
        RoundExpired = 5
    }

    /// <summary>An immutable per-player choice suitable for UI display.</summary>
    public sealed class RunMapPlayerVoteSnapshot
    {
        public ulong PlayerId { get; }
        public bool HasVoted { get; }
        public int NodeId { get; }

        public RunMapPlayerVoteSnapshot(ulong playerId, bool hasVoted, int nodeId)
        {
            PlayerId = playerId;
            HasVoted = hasVoted;
            NodeId = nodeId;
        }
    }

    /// <summary>An immutable vote total for one candidate node.</summary>
    public sealed class RunMapCandidateVoteSnapshot
    {
        public int NodeId { get; }
        public int VoteCount { get; }

        public RunMapCandidateVoteSnapshot(int nodeId, int voteCount)
        {
            if (nodeId < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(nodeId));
            }

            if (voteCount < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(voteCount));
            }

            NodeId = nodeId;
            VoteCount = voteCount;
        }
    }

    /// <summary>
    /// Immutable read model emitted whenever membership, a choice or round state changes.
    /// UI code can subscribe without receiving a mutable resolver reference.
    /// </summary>
    public sealed class RunMapVoteSnapshot
    {
        private readonly RunMapCandidateVoteSnapshot[] candidates;
        private readonly RunMapPlayerVoteSnapshot[] playerVotes;

        public int RoundId { get; }
        public bool HasRound { get; }
        public bool IsActive { get; }
        public double OpenedAt { get; }
        public double Deadline { get; }
        public IReadOnlyList<RunMapCandidateVoteSnapshot> Candidates =>
            Array.AsReadOnly(candidates);
        public IReadOnlyList<RunMapPlayerVoteSnapshot> PlayerVotes =>
            Array.AsReadOnly(playerVotes);

        public static RunMapVoteSnapshot Empty { get; } =
            new RunMapVoteSnapshot(
                -1,
                false,
                false,
                0d,
                0d,
                Array.Empty<RunMapCandidateVoteSnapshot>(),
                Array.Empty<RunMapPlayerVoteSnapshot>());

        public RunMapVoteSnapshot(
            int roundId,
            bool hasRound,
            bool isActive,
            double openedAt,
            double deadline,
            IEnumerable<RunMapCandidateVoteSnapshot> candidates,
            IEnumerable<RunMapPlayerVoteSnapshot> playerVotes)
        {
            if (candidates == null)
            {
                throw new ArgumentNullException(nameof(candidates));
            }

            if (playerVotes == null)
            {
                throw new ArgumentNullException(nameof(playerVotes));
            }

            RoundId = roundId;
            HasRound = hasRound;
            IsActive = isActive;
            OpenedAt = openedAt;
            Deadline = deadline;
            this.candidates = CopyCandidates(candidates);
            this.playerVotes = CopyPlayerVotes(playerVotes);
        }

        public bool ContainsCandidate(int nodeId)
        {
            for (int index = 0; index < candidates.Length; index++)
            {
                if (candidates[index].NodeId == nodeId)
                {
                    return true;
                }
            }

            return false;
        }

        public bool TryGetPlayerVote(ulong playerId, out RunMapPlayerVoteSnapshot vote)
        {
            for (int index = 0; index < playerVotes.Length; index++)
            {
                if (playerVotes[index].PlayerId == playerId)
                {
                    vote = playerVotes[index];
                    return true;
                }
            }

            vote = null;
            return false;
        }

        public double GetRemainingSeconds(double now)
        {
            if (!IsActive)
            {
                return 0d;
            }

            return Math.Max(0d, Deadline - now);
        }

        private static RunMapCandidateVoteSnapshot[] CopyCandidates(
            IEnumerable<RunMapCandidateVoteSnapshot> source)
        {
            List<RunMapCandidateVoteSnapshot> result =
                new List<RunMapCandidateVoteSnapshot>();
            foreach (RunMapCandidateVoteSnapshot candidate in source)
            {
                if (candidate == null)
                {
                    throw new ArgumentException(
                        "Candidate snapshots cannot contain null.",
                        nameof(source));
                }

                result.Add(new RunMapCandidateVoteSnapshot(
                    candidate.NodeId,
                    candidate.VoteCount));
            }

            return result.ToArray();
        }

        private static RunMapPlayerVoteSnapshot[] CopyPlayerVotes(
            IEnumerable<RunMapPlayerVoteSnapshot> source)
        {
            List<RunMapPlayerVoteSnapshot> result = new List<RunMapPlayerVoteSnapshot>();
            foreach (RunMapPlayerVoteSnapshot playerVote in source)
            {
                if (playerVote == null)
                {
                    throw new ArgumentException(
                        "Player vote snapshots cannot contain null.",
                        nameof(source));
                }

                result.Add(new RunMapPlayerVoteSnapshot(
                    playerVote.PlayerId,
                    playerVote.HasVoted,
                    playerVote.NodeId));
            }

            return result.ToArray();
        }
    }

    /// <summary>Immutable authoritative result of a completed vote.</summary>
    public sealed class RunMapVoteResult
    {
        private readonly int[] tiedNodeIds;

        public int RoundId { get; }
        public bool HasWinner { get; }
        public int WinnerNodeId { get; }
        public RunMapVoteResolutionReason Reason { get; }
        public IReadOnlyList<int> TiedNodeIds => Array.AsReadOnly(tiedNodeIds);

        public RunMapVoteResult(
            int roundId,
            bool hasWinner,
            int winnerNodeId,
            RunMapVoteResolutionReason reason,
            IEnumerable<int> tiedNodeIds)
        {
            if (tiedNodeIds == null)
            {
                throw new ArgumentNullException(nameof(tiedNodeIds));
            }

            if (hasWinner && winnerNodeId < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(winnerNodeId));
            }

            RoundId = roundId;
            HasWinner = hasWinner;
            WinnerNodeId = winnerNodeId;
            Reason = reason;
            this.tiedNodeIds = new List<int>(tiedNodeIds).ToArray();
        }
    }

    /// <summary>Client-facing details when the server rejects a vote RPC.</summary>
    public sealed class RunMapVoteRejection
    {
        public int RoundId { get; }
        public int NodeId { get; }
        public RunMapVoteRejectReason Reason { get; }

        public RunMapVoteRejection(int roundId, int nodeId, RunMapVoteRejectReason reason)
        {
            RoundId = roundId;
            NodeId = nodeId;
            Reason = reason;
        }
    }
}
