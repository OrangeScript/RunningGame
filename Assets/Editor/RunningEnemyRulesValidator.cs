#if UNITY_EDITOR
using System;
using UnityEngine;

/// <summary>Rules whose regressions would turn a forward shooter into auto aim,
/// unblockable attacks, delayed burst teleportation, or overlapping road art.</summary>
public static class RunningEnemyRulesValidator
{
    public static void ValidateCommandLine()
    {
        foreach (Vector3 target in new[] { new Vector3(-8, 0, -20), new Vector3(8, 3, -8) })
            Require(RunnerEnemyAttackPattern.LockDirection(EnemyArchetype.Chaser, Vector3.zero, target) == Vector3.back,
                "Ordinary sentries must fire straight down the road.");
        foreach (EnemyArchetype kind in Enum.GetValues(typeof(EnemyArchetype)))
        {
            Vector3 locked = RunnerEnemyAttackPattern.LockDirection(kind, Vector3.zero, new Vector3(100, 0, -5));
            Require(locked.z < 0 && Mathf.Abs(Vector3.SignedAngle(Vector3.back, locked, Vector3.up)) <= 28.01f,
                "Special shot can turn behind the enemy or across roads.");
            int count = RunnerEnemyAttackPattern.ProjectileCount(kind);
            for (int i = 0; i < count; i++)
                Require(RunnerEnemyAttackPattern.ShotDirection(kind, locked, i).z < 0, "A shot targets players who have already passed.");
            Require(RunnerEnemyAttackPattern.ProjectileSpeed(kind, 100) <= 13, "High difficulty creates unreadably fast lane bullets.");
        }
        Require(!RunnerEnemyAttackPattern.CanEngage(-1) && !RunnerEnemyAttackPattern.CanEngage(40) &&
            RunnerEnemyAttackPattern.CanEngage(20), "Enemy activation ignores approach direction or visibility budget.");
        var shot = new RunnerEnemyShot { Origin = new Vector3(2, .9f, 20), Direction = Vector3.back, Speed = 10, Born = 10 };
        Require(RunnerEnemyProjectile.PositionAt(shot, 9) == shot.Origin, "Pending burst round moved before its launch.");
        Require(RunnerEnemyProjectile.PositionAt(shot, 11) == new Vector3(2, .9f, 10), "Replica trajectory disagrees with server-time travel.");
        var moving = new RunnerObstaclePlan(ProceduralObstacleKind.MovingBlock, new Vector3(3, 1, 10),
            new Vector3(2, 2, 1), 0, 2, 0, 1, 8);
        Require(RunnerDefenderPlacement.OverlapsObstacle(new Vector3(5, 0, 10), moving), "Defender spawns in the moving cover's future path.");
        Require(!RunnerDefenderPlacement.OverlapsObstacle(new Vector3(-5, 0, 10), moving), "Reserved shoulder became unavailable.");
        var spinner = new RunnerObstaclePlan(ProceduralObstacleKind.Spinner, new Vector3(0, 1, 10),
            new Vector3(6, .4f, .4f), 0, 0, 45, 1, 8);
        Require(RunnerDefenderPlacement.OverlapsObstacle(new Vector3(0, 0, 12.5f), spinner), "Rotating arm's Z envelope was ignored.");
        Debug.Log("RUNNING_ENEMY_RULES_PASS straight-fire limited-special-aim approach-only bounded-speed delayed-burst obstacle-envelopes");
    }
    private static void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
}
#endif
