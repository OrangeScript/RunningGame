/// <summary>Optional collision contract for road objects. Visual projectiles
/// read blocking state; only authoritative projectiles call HitByProjectile.</summary>
public interface IRunnerProjectileObstacle
{
    int LaneId { get; }
    bool BlocksProjectiles { get; }
    void HitByProjectile(float damage, ElementType element);
}
