#if UNITY_EDITOR
using System;
using UnityEngine;

/// <summary>Forward-only weapon invariants; the full loop smoke additionally
/// exercises actual collider hits and server-authoritative enemy health.</summary>
public static class RunnerWeaponsValidator
{
    public static void Validate()
    {
        Vector3 forward=Vector3.forward;
        for(int i=0;i<120;i++)
            forward=RunnerProjectileSteering.Steer(forward,Vector3.right*10,0,12,1/60f);
        Require(forward==Vector3.forward,"Default projectiles steer toward a target.");
        Require(!RunnerProjectileSteering.CanAcquire(Vector3.zero,new Vector3(1,0,-5),2,2,24,12),"Homing can acquire a target behind it.");
        Require(!RunnerProjectileSteering.CanAcquire(Vector3.zero,new Vector3(0,0,10),2,3,24,12),"Homing can acquire another lane.");
        Require(!RunnerProjectileSteering.CanAcquire(Vector3.zero,new Vector3(4,0,10),2,2,24,12),"Homing turns toward a target outside the 12-degree cone.");
        Require(RunnerProjectileSteering.CanAcquire(Vector3.zero,new Vector3(1.5f,0,10),2,2,24,12),"Mild homing cannot acquire a nearby forward target.");
        Vector3 guided=Vector3.forward;
        for(int i=0;i<120;i++)
        {
            Vector3 before=guided;
            guided=RunnerProjectileSteering.Steer(guided,new Vector3(8,0,10),28,12,1/60f);
            Require(Vector3.Angle(Vector3.forward,guided)<=12.001f&&guided.z>0,"Homing leaves its forward cone.");
            Require(Vector3.Angle(before,guided)<=28/60f+.02f,"Homing exceeds its turn-rate budget.");
        }
        var spec=new ProjectileSpec{homingTurnRate=28,homingMaxAngle=12,homingAcquireRange=24,obstacleDamageMultiplier=2};
        var clone=spec.Clone();
        Require(clone.homingTurnRate==28&&clone.homingAcquireRange==24&&clone.obstacleDamageMultiplier==2,"Projectile clone loses build modifiers.");
        var visual=RunnerShotVisual.From(new Vector3(0,1,4),2,spec,36).ToSpec();
        Require(visual.damage==0&&visual.homingTurnRate==28&&visual.homingMaxAngle==12,"Cosmetic shot recipe changes steering or contains damage.");
        Debug.Log("RUNNER_WEAPONS_VALIDATION_PASS straight=world+Z homing=bounded+forward+same-lane clone=preserved cosmetic=no-damage");
    }
    private static void Require(bool valid,string message){if(!valid)throw new InvalidOperationException(message);}
}
#endif
