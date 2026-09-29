using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "ExplosionEffect", menuName = "Roguelike/Effects/Explosion")]
public class ExplosionDefinition : ItemEffectDefiniton
{

    [SerializeField] private float radius = 3f;
    
    [SerializeField] private float damageMultiplier = 0.5f;

    public override ItemRuntime CreateRuntime()
    {
        return new ExplosionRuntime(radius,damageMultiplier);
    }
}
