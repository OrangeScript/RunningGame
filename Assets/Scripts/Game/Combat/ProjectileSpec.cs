using System.Collections;
using System.Collections.Generic;
using UnityEngine;


[System.Serializable]
public class ProjectileSpec
{

    public GameplayMode gameplayMode = GameplayMode.Runner;
    public float damage = 1f;
    public float speed = 10f;
    public float sizeMultiplier = 1f;
    public ElementType element = ElementType.None;
    public ElementType secondaryElement = ElementType.None;
    public void AddElement(ElementType value)
    {
        if(value==ElementType.None||value==element||value==secondaryElement) return;
        if(element==ElementType.None) element=value;
        else if(secondaryElement==ElementType.None) secondaryElement=value;
    }

    public int bounce = 0;
    public float bounceRange = 6f;
    public float explosionRadius = 0f;
    public float explosionDamageMultiplier = 1f;
    public int pierce = 0;
    public float pierceRange = 8f;
    // Zero turn rate is a genuinely straight projectile: no target scan.
    public float homingTurnRate;
    public float homingMaxAngle = 12f;
    public float homingAcquireRange = 18f;
    public float obstacleDamageMultiplier = 1f;


    public ProjectileSpec Clone()
    {
        return new ProjectileSpec{
            gameplayMode = gameplayMode,
            damage = damage,
            speed = speed,
            sizeMultiplier = sizeMultiplier,
            element = element,
            secondaryElement = secondaryElement,
            bounce = bounce,
            bounceRange = bounceRange,
            pierce = pierce,
            pierceRange = pierceRange,
            homingTurnRate = homingTurnRate,
            homingMaxAngle = homingMaxAngle,
            homingAcquireRange = homingAcquireRange,
            obstacleDamageMultiplier = obstacleDamageMultiplier,
            explosionRadius = explosionRadius,
            explosionDamageMultiplier = explosionDamageMultiplier
        };
    }

}

public enum ElementType
{
    None,
    Ice,
    Fire,
    Water,
    Lightning,
    Wind,
    Earth,
    Nature
}
