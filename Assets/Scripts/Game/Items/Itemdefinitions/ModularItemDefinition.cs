using UnityEngine;

public enum ItemActiveAbility { None,Bomb,Healing,Shield,ElementBurst }

/// <summary>Composable item data shared by Runner and Arena. No item-specific
/// scene searches are needed for ordinary weapon and survival modifiers.</summary>
[CreateAssetMenu(menuName="Roguelike/Effects/Modular Item")]
public sealed class ModularItemDefinition : ItemEffectDefiniton
{
    public float damageMultiplier=1,intervalMultiplier=1,speedMultiplier=1,sizeMultiplier=1;
    public int extraProjectiles,pierce,bounce;
    public ElementType element;
    public ElementType secondaryElement;
    public float movementMultiplier=1,damageReduction,regeneration,invulnerability,grazeMultiplier=1;
    public ItemActiveAbility activeAbility;
    public float cooldown=18;
    public override ItemRuntime CreateRuntime()=>new ModularItemRuntime(this);
}

public sealed class ModularItemRuntime : ItemRuntime
{
    private readonly ModularItemDefinition definition;
    private float cooldownRemaining;
    public ModularItemRuntime(ModularItemDefinition data)=>definition=data;
    public override bool CanActivate=>definition.activeAbility!=ItemActiveAbility.None;
    public override float CooldownDuration=>definition.cooldown;
    public override float CooldownRemaining=>cooldownRemaining;
    public override void Tick(float dt)=>cooldownRemaining=Mathf.Max(0,cooldownRemaining-dt);
    public override void ModifyAttack(AttackPlan plan)
    {
        plan.damageMultiplier*=definition.damageMultiplier;
        plan.attackInterval*=definition.intervalMultiplier;
        plan.projectileCount=Mathf.Clamp(plan.projectileCount+definition.extraProjectiles,1,12);
    }
    public override void ModifyProjectile(ProjectileSpec spec)
    {
        spec.speed*=definition.speedMultiplier;spec.sizeMultiplier*=definition.sizeMultiplier;
        spec.pierce+=definition.pierce;spec.bounce+=definition.bounce;
        spec.AddElement(definition.element);
        spec.AddElement(definition.secondaryElement);
    }
    public override void ModifyArenaStats(ArenaBuildStats stats)
    {
        stats.MoveSpeed*=definition.movementMultiplier;
        stats.DamageReduction+=definition.damageReduction;
        stats.Regeneration+=definition.regeneration;
        stats.ExtraInvulnerability+=definition.invulnerability;
        stats.GrazeMultiplier*=definition.grazeMultiplier;
    }
    public override bool TryActivate()
    {
        if(Owner==null||!CanActivate||cooldownRemaining>0)return false;
        if(Owner.CurrentMode==GameplayMode.BossArena)
        {
            var player=Owner.GetComponent<ArenaPlayerNetwork>();
            if(player==null||!player.IsArenaAlive)return false;
            if(definition.activeAbility==ItemActiveAbility.Healing)
            {
                foreach(var ally in ArenaPlayerNetwork.ActivePlayers)if(ally.IsArenaAlive)ally.HealServer(2);
            }
            else if(definition.activeAbility==ItemActiveAbility.Shield)player.GrantShieldServer(3,6);
            else
            {
                var boss=Object.FindFirstObjectByType<ArenaBossController>();
                boss?.ApplyArenaDamage(new ArenaDamageInfo(16,1,Owner.OwnerClientId,
                    boss.transform.position,Vector3.forward,definition.element));
                if(definition.activeAbility==ItemActiveAbility.Bomb)
                {
                    Object.FindFirstObjectByType<ArenaBootstrap>()?.ClearBossBullets();
                    boss?.PausePattern(.8f);
                }
            }
        }
        else
        {
            var runner=Owner.GetComponent<RunnerController>();
            if(definition.activeAbility==ItemActiveAbility.Healing||definition.activeAbility==ItemActiveAbility.Shield)
                Owner.GetComponent<CrowdManager>()?.HealHealth(20);
            else foreach(var enemy in Object.FindObjectsByType<Enemy>(FindObjectsSortMode.None))
            {
                if(enemy.IsDead||(runner!=null&&enemy.LaneId!=runner.LaneId))continue;
                enemy.ApplyElement(definition.element,1,3,12);enemy.TakeDamage(12,definition.element);
            }
        }
        cooldownRemaining=definition.cooldown;return true;
    }
}
