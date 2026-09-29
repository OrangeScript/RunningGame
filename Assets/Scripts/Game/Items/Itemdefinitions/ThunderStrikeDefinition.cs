using System.Collections;
using System.Collections.Generic;
using UnityEngine;


[CreateAssetMenu(
    fileName = "ThunderStrikeEffect",
    menuName = "Roguelike/Effects/Thunder Strike"
)]
public class ThunderStrikeDefinition : ItemEffectDefiniton
{
    // Start is called before the first frame update
[SerializeField]
    private float cooldown = 4f;

    [SerializeField]
    private float range = 15f;

    [SerializeField]
    private float damage = 10f;

    [SerializeField]
    private float auraAmount = 1f;

    [SerializeField]
    private LayerMask enemyLayer;


    public override ItemRuntime CreateRuntime()
    {
        return new ThunderStrikerRuntime(
            cooldown,
            range,
            damage,
            auraAmount,
            enemyLayer
        );
    }
}
