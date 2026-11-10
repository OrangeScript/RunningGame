using UnityEngine;

/// <summary>Shared rules for the rendered warning and the actual shot.</summary>
public static class RunnerEnemyAttackPattern
{
    public const float ActivationRange = 32;
    public const float MinimumFiringDistance = 1.5f;
    public const float MaximumSniperAngle = 28;
    public static bool CanEngage(float forwardDistance)
        => forwardDistance >= MinimumFiringDistance && forwardDistance <= ActivationRange;
    public static int ProjectileCount(EnemyArchetype kind)
        => kind == EnemyArchetype.Strafer || kind == EnemyArchetype.Ranged ? 3 : 1;
    public static float ProjectileSpeed(EnemyArchetype kind, float difficulty)
        => Mathf.Min(13, (kind == EnemyArchetype.Charger ? 12 : kind == EnemyArchetype.Strafer ? 9 : 10) +
            Mathf.InverseLerp(1, 4, difficulty));
    public static Vector3 LockDirection(EnemyArchetype kind, Vector3 origin, Vector3 target)
    {
        if (kind != EnemyArchetype.Charger) return Vector3.back;
        Vector3 difference = target - origin; difference.y = 0;
        if (difference.sqrMagnitude < .001f) return Vector3.back;
        float angle = Mathf.Clamp(Vector3.SignedAngle(Vector3.back, difference, Vector3.up), -MaximumSniperAngle, MaximumSniperAngle);
        return Quaternion.AngleAxis(angle, Vector3.up) * Vector3.back;
    }
    public static Vector3 ShotDirection(EnemyArchetype kind, Vector3 locked, int index)
        => kind == EnemyArchetype.Strafer ? Quaternion.AngleAxis((index - 1) * 15, Vector3.up) * Vector3.back : locked;
}
