#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>Checks generated movement envelopes and actual collider contracts,
/// rather than requiring a particular random list of obstacles.</summary>
public static class RunningObstacleValidator
{
    public static void ValidateCommandLine()
    {
        Validate();Debug.Log("RUNNING_OBSTACLE_VALIDATION_PASS seeds=400 deterministic escape-shoulder rows gate-clearance geometry hit-contract cycles");
    }

    public static void Validate()
    {
        var kinds=new HashSet<ProceduralObstacleKind>();
        for(int seed=1;seed<=400;seed++)
        {
            foreach(bool gate in new[]{false,true})
            {
                var first=RunnerObstaclePlanner.Generate(seed,1+seed%6,gate);
                var again=RunnerObstaclePlanner.Generate(seed,1+seed%6,gate);
                Require(first.Length==again.Length,"Seed changed obstacle count.");
                Require(first.Length<=(gate?2:4),"Obstacle budget overcrowded a section.");
                float previous=-10;
                for(int i=0;i<first.Length;i++)
                {
                    var item=first[i];kinds.Add(item.Kind);
                    Require(item.Equals(again[i]),"Seed did not preserve exact motion/cycle parameters.");
                    var bounds=item.SweptXBounds;
                    Require(bounds.x>=-7.25f&&bounds.y<=7.25f,"Obstacle sweep escaped its lane.");
                    Require(RunnerObstaclePlanner.EscapeSide(seed)<0?bounds.x>=-3.75f:bounds.y<=3.75f,"Moving obstacle invaded the promised escape shoulder.");
                    Require(item.Position.z-previous>=3.2f,"Consecutive hazards leave too little reaction space.");
                    Require(item.Position.z<=(gate?8.5f:17),"Obstacle overlaps a gate or section exit.");
                    previous=item.Position.z;
                }
            }
        }
        Require(kinds.Count==7,"Seeds cannot produce every obstacle mechanic.");
        var timed=new RunnerObstaclePlan(ProceduralObstacleKind.PulseLaser,Vector3.zero,Vector3.one,0,0,0,0,8,4,.8f,1);
        Require(timed.CycleAt(.3)==RunnerHazardCycle.Warning&&timed.CycleAt(1)==RunnerHazardCycle.Active&&
            timed.CycleAt(3)==RunnerHazardCycle.Safe&&timed.CycleAt(4.3)==RunnerHazardCycle.Warning,"Cycle order lacks a warning or shared clock wrap.");

        var root=new GameObject("ObstacleGeometryValidation");
        try
        {
            for(int i=0;i<7;i++)
            {
                var plan=new RunnerObstaclePlan((ProceduralObstacleKind)i,new Vector3(1,1,5),new Vector3(2,2,1),0,1,60,4,8);
                var obstacle=RunnerObstacleRuntime.Create(root.transform,plan,2,false,0,77,i);
                var collider=obstacle.GetComponent<Collider>();
                Require(collider!=null&&collider.isTrigger,"Road hazard can physically stop a forward runner.");
                Require(obstacle.LaneId==2&&RunnerObstacleDamageUtility.Find(collider)==obstacle,"Projectile obstacle interface lost its lane or target.");
                bool energy=plan.Kind==ProceduralObstacleKind.PulseLaser||plan.Kind==ProceduralObstacleKind.SpikePad;
                Require(obstacle.BlocksProjectiles!=energy,"Timed energy hazards incorrectly block bullets.");
                obstacle.ApplyState(0,true);
                Require(obstacle.IsDestroyed&&!obstacle.BlocksProjectiles&&!collider.enabled,"Broken cover still blocks projectiles or damages a runner.");
            }
        }
        finally{UnityEngine.Object.DestroyImmediate(root);}
    }
    private static void Require(bool condition,string message){if(!condition)throw new InvalidOperationException(message);}
}
#endif
