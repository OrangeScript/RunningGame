/// <summary>Mode-specific survival and movement modifiers. Forward pace stays
/// fixed; movement perks improve dodging rather than shortening the road.</summary>
public sealed class RunnerBuildStats
{
    public float StrafeSpeed = 1;
    public float DamageReduction;
    public int MaxHealthBonus;
    public float Regeneration;
    public int HealthOnKill;
    public float PickupRadius = 1.1f;
    public float ExtraInvulnerability;
}
