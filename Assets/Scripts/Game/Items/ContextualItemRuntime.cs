using System.Collections.Generic;

/// <summary>
/// Keeps one item identity while delegating its behaviour to a mode-specific
/// runtime. This lets inventory snapshots survive Runner -> Boss -> FPS swaps.
/// </summary>
public sealed class ContextualItemRuntime : ItemRuntime
{
    private readonly ItemRuntime fallback;
    private readonly Dictionary<GameplayMode, ItemRuntime> runtimes;
    private readonly List<ItemRuntime> uniqueRuntimes = new List<ItemRuntime>();
    private ItemManager owner;

    public ContextualItemRuntime(
        ItemRuntime fallback,
        Dictionary<GameplayMode, ItemRuntime> runtimes)
    {
        this.fallback = fallback;
        this.runtimes = runtimes ?? new Dictionary<GameplayMode, ItemRuntime>();

        AddUnique(fallback);
        foreach (ItemRuntime runtime in this.runtimes.Values)
            AddUnique(runtime);
    }

    public override bool CanActivate => Current != null && Current.CanActivate;

    private ItemRuntime Current
    {
        get
        {
            if (owner != null && runtimes.TryGetValue(owner.CurrentMode, out ItemRuntime runtime))
                return runtime;

            return fallback;
        }
    }

    public override void OnAcquire(ItemManager itemOwner)
    {
        base.OnAcquire(itemOwner);
        owner = itemOwner;

        for (int i = 0; i < uniqueRuntimes.Count; i++)
            uniqueRuntimes[i].OnAcquire(itemOwner);
    }

    public override void OnRemove()
    {
        for (int i = 0; i < uniqueRuntimes.Count; i++)
            uniqueRuntimes[i].OnRemove();

        owner = null;
        base.OnRemove();
    }

    public override void OnModeChanged(GameplayMode previous, GameplayMode current)
    {
        for (int i = 0; i < uniqueRuntimes.Count; i++)
            uniqueRuntimes[i].OnModeChanged(previous, current);
    }

    public override void Tick(float deltaTime)
    {
        Current?.Tick(deltaTime);
    }

    public override void ModifyProjectile(ProjectileSpec spec)
    {
        Current?.ModifyProjectile(spec);
    }

    public override void ModifyAttack(AttackPlan plan)
    {
        Current?.ModifyAttack(plan);
    }

    public override bool TryActivate()
    {
        return Current != null && Current.TryActivate();
    }

    private void AddUnique(ItemRuntime runtime)
    {
        if (runtime != null && !uniqueRuntimes.Contains(runtime))
            uniqueRuntimes.Add(runtime);
    }
}
