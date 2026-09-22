using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName ="HeavyBulletEffect",menuName ="Roguelike/Effects/Heavy Bullet")]
public class HeavyBulletDefinition : ItemEffectDefiniton
{

    [SerializeField]
    private float damageMultiplier = 3f;

    [SerializeField]
    private float speedMultiplier = 0.5f;

    [SerializeField]
    private float sizeMultiplier = 3f;
    public override ItemRuntime CreateRuntime()
    {
        return new HeavyBulletRuntime(damageMultiplier,speedMultiplier,sizeMultiplier);
    }
}
