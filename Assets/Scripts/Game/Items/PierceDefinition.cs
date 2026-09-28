using System.Collections;
using System.Collections.Generic;
using UnityEngine;


[CreateAssetMenu(
    fileName = "PierceEffect",
    menuName = "Roguelike/Effects/Pierce"
)]
public class PierceDefinition : ItemEffectDefiniton
{
    // Start is called before the first frame update
    [SerializeField] private int extraPierces = 2;
    [SerializeField] private float pierceRange = 8f;

    public override ItemRuntime CreateRuntime()
    {
        return new PierceRuntime(extraPierces,pierceRange);
    }
}
