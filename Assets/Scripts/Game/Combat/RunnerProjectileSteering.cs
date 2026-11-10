using UnityEngine;

/// <summary>Forward-only steering shared by the simulation and validators.
/// No target behind the projectile can be acquired, even with homing upgrades.</summary>
public static class RunnerProjectileSteering
{
    public static bool CanAcquire(Vector3 origin, Vector3 target, int laneId, int targetLaneId,
        float range, float maxAngle)
    {
        if (laneId >= 0 && targetLaneId != laneId) return false;
        Vector3 offset = target - origin;
        offset.y = 0;
        if (offset.z <= .05f || offset.sqrMagnitude > range * range) return false;
        return Vector3.Angle(Vector3.forward, offset) <= Mathf.Clamp(maxAngle, 0, 45);
    }

    public static Vector3 Steer(Vector3 current, Vector3 desired, float turnRate, float maxAngle, float dt)
    {
        current.y = desired.y = 0;
        if (current.sqrMagnitude < .0001f) current = Vector3.forward;
        if (turnRate <= 0 || desired.sqrMagnitude < .0001f) return current.normalized;
        Vector3 next = Vector3.RotateTowards(current.normalized, desired.normalized,
            Mathf.Max(0, turnRate) * Mathf.Deg2Rad * Mathf.Max(0, dt), 0);
        float angle = Mathf.Atan2(next.x, next.z) * Mathf.Rad2Deg;
        angle = Mathf.Clamp(angle, -Mathf.Clamp(maxAngle, 0, 45), Mathf.Clamp(maxAngle, 0, 45));
        return Quaternion.AngleAxis(angle, Vector3.up) * Vector3.forward;
    }
}
