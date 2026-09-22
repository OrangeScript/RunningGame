using System.Collections;
using System.Collections.Generic;
using System.Drawing;
using UnityEngine;


[System.Serializable]
public class ProjectileSpec
{

    public float damage = 1f;
    public float speed = 10f;
    public float sizeMultiplier = 1f;
    public ElementType element = ElementType.None;

    public ProjectileSpec Clone()
    {
        return new ProjectileSpec{
            damage = damage,
            speed = speed,
            sizeMultiplier = sizeMultiplier,
            element = element
        };
    }

}

public enum ElementType
{
    None,
    Ice,
    Fire,
    Lightning
}
