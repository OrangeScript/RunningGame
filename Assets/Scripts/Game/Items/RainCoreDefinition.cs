using System.Collections;
using System.Collections.Generic;
using UnityEngine;


[CreateAssetMenu(
    fileName = "RainCoreEffect",
    menuName = "Roguelike/Effects/Rain Core"
)]
public class RainCoreDefinition : ItemEffectDefiniton
{
    [SerializeField]
    private float interval = 3f;

    [SerializeField]
    private float radius = 12f;

    [SerializeField]
    private float auraAmount = 1f;

    [SerializeField]
    private float auraDuration = 4f;

    [SerializeField]
    private LayerMask enemyLayer;


    public override ItemRuntime CreateRuntime()
    {
        return new RainCoreRuntime(
            interval,
            radius,
            auraAmount,
            auraDuration,
            enemyLayer
        );
    }

}
