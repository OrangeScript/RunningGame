#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using Running.Game.RunMap;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Fast, transport-independent regression coverage for the authoritative parts
/// of multiplayer map voting. Runtime lane isolation is covered by the main
/// Play Mode smoke and the project validator checks its required components.
/// </summary>
public static class RunningMultiplayerLogicValidator
{
    [MenuItem("Running/Validate Multiplayer Logic")]
    public static void ValidateFromMenu()
    {
        ValidateOrThrow();
        Debug.Log("Running multiplayer logic validation passed.");
    }

    public static void ValidateCommandLine()
    {
        ValidateOrThrow();
        Debug.Log("RUNNING_MULTIPLAYER_LOGIC_OK");
    }

    private static void ValidateOrThrow()
    {
        ValidatePublicChoicesAndVoteChanges();
        ValidateDisconnectMembership();
        ValidateTimeoutAndDeterministicTie();
        ValidateRejectedVotes();
    }

    private static void ValidatePublicChoicesAndVoteChanges()
    {
        var resolver = new RunMapVoteResolver(1907);
        RunMapVoteResult resolved = null;
        resolver.Resolved += result => resolved = result;
        resolver.BeginRound(0, new[] { 10, 20 }, new ulong[] { 1, 2, 3 }, 100d, 15d);

        Require(resolver.CurrentSnapshot.PlayerVotes.Count == 3,
            "Every eligible player's choice must be publicly represented.");
        Require(resolver.TrySubmitVote(1, 0, 10, 101d, out _),
            "First vote should be accepted.");
        Require(resolver.TrySubmitVote(1, 0, 20, 102d, out _),
            "A player must be able to change their vote before resolution.");

        Require(resolver.CurrentSnapshot.TryGetPlayerVote(1, out RunMapPlayerVoteSnapshot vote) &&
                vote.HasVoted && vote.NodeId == 20,
            "The public snapshot did not expose the changed vote.");
        Require(GetTally(resolver.CurrentSnapshot, 10) == 0 &&
                GetTally(resolver.CurrentSnapshot, 20) == 1,
            "Changing a vote must move, not duplicate, its tally.");

        Require(resolver.TrySubmitVote(2, 0, 10, 103d, out _),
            "Second player's vote should be accepted.");
        Require(resolver.RemovePlayer(3),
            "Disconnecting an eligible player should update membership.");
        Require(resolved != null && resolved.HasWinner &&
                resolved.Reason == RunMapVoteResolutionReason.AllPlayersVoted,
            "Removing the final non-voter should resolve votes from remaining players.");
        Require(resolved.TiedNodeIds.Count == 2,
            "One-to-one votes should preserve both tied finalists.");
    }

    private static void ValidateDisconnectMembership()
    {
        var resolver = new RunMapVoteResolver(77);
        RunMapVoteResult resolved = null;
        resolver.Resolved += result => resolved = result;
        resolver.BeginRound(4, new[] { 3, 8 }, new ulong[] { 10, 11, 12 }, 0d, 30d);
        resolver.TrySubmitVote(10, 4, 3, 1d, out _);
        resolver.TrySubmitVote(11, 4, 8, 1d, out _);

        Require(resolver.RemovePlayer(10), "A voting disconnect should be removable.");
        Require(resolved == null && resolver.IsActive,
            "A round must remain open while a connected eligible player has not voted.");
        Require(!resolver.CurrentSnapshot.TryGetPlayerVote(10, out _),
            "Disconnected players and their old votes must disappear from the snapshot.");
        Require(GetTally(resolver.CurrentSnapshot, 3) == 0,
            "A disconnected player's vote must be removed from totals.");

        Require(resolver.RemovePlayer(12), "The remaining non-voter should be removable.");
        Require(resolved != null && resolved.WinnerNodeId == 8,
            "The remaining connected player's selection should win.");
    }

    private static void ValidateTimeoutAndDeterministicTie()
    {
        int firstWinner = ResolveEmptyTimeout(4455);
        int secondWinner = ResolveEmptyTimeout(4455);
        Require(firstWinner == secondWinner,
            "The same seed/round/candidates must produce the same tie winner.");
    }

    private static int ResolveEmptyTimeout(int seed)
    {
        var resolver = new RunMapVoteResolver(seed);
        RunMapVoteResult resolved = null;
        resolver.Resolved += result => resolved = result;
        resolver.BeginRound(2, new[] { 5, 7, 9 }, new ulong[] { 1, 2 }, 50d, 5d);
        Require(resolver.Tick(55d), "The server deadline should resolve the round.");
        Require(resolved != null && resolved.HasWinner &&
                resolved.Reason == RunMapVoteResolutionReason.Timeout,
            "A no-vote timeout must still select one reachable branch.");
        Require(resolved.TiedNodeIds.Count == 3,
            "All zero-vote candidates should be represented in the tie.");
        return resolved.WinnerNodeId;
    }

    private static void ValidateRejectedVotes()
    {
        var resolver = new RunMapVoteResolver(1);
        resolver.BeginRound(9, new[] { 100, 200 }, new ulong[] { 4, 5 }, 10d, 10d);

        Require(!resolver.TrySubmitVote(99, 9, 100, 11d, out RunMapVoteRejectReason ineligible) &&
                ineligible == RunMapVoteRejectReason.PlayerNotEligible,
            "An ineligible sender must be rejected.");
        Require(!resolver.TrySubmitVote(4, 8, 100, 11d, out RunMapVoteRejectReason stale) &&
                stale == RunMapVoteRejectReason.WrongRound,
            "A stale round ID must be rejected.");
        Require(!resolver.TrySubmitVote(4, 9, 999, 11d, out RunMapVoteRejectReason unreachable) &&
                unreachable == RunMapVoteRejectReason.NodeNotReachable,
            "A non-candidate node must be rejected.");
        Require(!resolver.TrySubmitVote(4, 9, 100, 20d, out RunMapVoteRejectReason expired) &&
                expired == RunMapVoteRejectReason.RoundExpired,
            "A vote at or after the deadline must be rejected after timeout resolution.");
    }

    private static int GetTally(RunMapVoteSnapshot snapshot, int nodeId)
    {
        foreach (RunMapCandidateVoteSnapshot candidate in snapshot.Candidates)
        {
            if (candidate.NodeId == nodeId)
                return candidate.VoteCount;
        }

        throw new InvalidOperationException($"Candidate {nodeId} was absent from the snapshot.");
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }
}
#endif
