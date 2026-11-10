using UnityEngine;

/// <summary>Reject the whole motion envelope, not only an obstacle's current pose.</summary>
public static class RunnerDefenderPlacement
{
    public static bool OverlapsObstacle(Vector3 localPosition, RunnerObstaclePlan plan, float clearance = .95f)
    {
        Vector2 xBounds = plan.SweptXBounds;
        float halfZ = plan.Kind == ProceduralObstacleKind.Spinner ?
            Mathf.Sqrt(plan.Size.x * plan.Size.x + plan.Size.z * plan.Size.z) * .5f : plan.Size.z * .5f;
        return localPosition.x > xBounds.x - clearance && localPosition.x < xBounds.y + clearance &&
            Mathf.Abs(localPosition.z - plan.Position.z) < halfZ + clearance;
    }
}
