#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEngine;

public static class RunningContinuousValidator
{
    public static void ValidateCommandLine()
    {
        RunningBossValidator.ValidateCommandLine();
        RunnerWeaponsValidator.Validate();
        RunningObstacleValidator.Validate();
        RunningEnemyRulesValidator.ValidateCommandLine();
        ValidateLoot();
        Debug.Log("RUNNING_CONTINUOUS_VALIDATION_PASS forward-weapons stationary-defenders obstacles=7 catalog=54 gate+drop-source defense+homing deterministic-loot");
    }
    private static void ValidateLoot()
    {
        var catalog=RuntimeItemCatalog.Default;Require(catalog!=null,"No item catalog.");
        int common=0;var ids=new HashSet<int>();
        foreach(var item in catalog.Items)
        {
            Require(item!=null&&ids.Add(item.NetworkId),"Duplicate/null item definition.");
            if(item.Category==ItemCategory.Passive&&item.Rarity==ItemRarity.Common&&
                ItemAcquisitionPolicy.CanAcquire(item,ItemAcquisitionSource.InLevelDrop))common++;
            if(item.Category!=ItemCategory.Passive)
                Require(!ItemAcquisitionPolicy.CanAcquire(item,ItemAcquisitionSource.InLevelDrop)&&
                    !ItemAcquisitionPolicy.CanAcquire(item,ItemAcquisitionSource.RoadGate),"Road rewards grant active/temporary items.");
            if(item.Rarity>=ItemRarity.Epic)
                Require(!ItemAcquisitionPolicy.CanAcquire(item,ItemAcquisitionSource.RoadGate),"Ordinary road gate grants an epic.");
        }
        Require(common>=10,"Common drop pool is too small to sustain the road.");
        var owned=new HashSet<int>();var reserved=new HashSet<int>();
        for(int i=0;i<10;i++)
        {
            var item=RunLootPolicy.Choose(catalog.Items,ItemRarity.Common,ItemAcquisitionSource.InLevelDrop,owned,reserved,i*137);
            Require(item!=null&&!owned.Contains(item.NetworkId)&&!reserved.Contains(item.NetworkId),"Repeated or missing fresh common drop.");
            var repeat=RunLootPolicy.Choose(catalog.Items,ItemRarity.Common,ItemAcquisitionSource.InLevelDrop,owned,reserved,i*137);
            Require(item.NetworkId==repeat.NetworkId,"Loot roll is not deterministic.");reserved.Add(item.NetworkId);
        }
        var defense=catalog.Find(1344).CreateRuntime();var stats=new RunnerBuildStats();defense.ModifyRunnerStats(stats);
        Require(stats.MaxHealthBonus==25&&stats.DamageReduction>.079f,"Armor pickup does not strengthen Running defense.");
        var magnet=catalog.Find(1342).CreateRuntime();magnet.ModifyRunnerStats(stats);
        Require(stats.PickupRadius>2,"Magnet has no actual pickup radius effect.");
        var spec=new ProjectileSpec();catalog.Find(1341).CreateRuntime().ModifyProjectile(spec);
        Require(spec.homingTurnRate==28&&spec.homingMaxAngle==12,"Guidance pickup does not enable mild homing.");
        var arena=new ProjectileSpec{gameplayMode=GameplayMode.BossArena};catalog.Find(1341).CreateRuntime().ModifyProjectile(arena);
        Require(arena.pierce==1&&arena.speed>10,"Guidance hardware has no meaningful Arena adaptation.");
        var explosionPrefab=UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/ExplosionVFX.prefab");
        var explosion=UnityEngine.Object.Instantiate(explosionPrefab);
        try
        {
            explosion.GetComponent<ExplosionVFX>().Initialize(4);
            foreach(var collider in explosion.GetComponentsInChildren<Collider>())Require(!collider.enabled,"Explosion visuals block running.");
            foreach(var mesh in explosion.GetComponentsInChildren<MeshRenderer>())Require(!mesh.enabled,"Opaque explosion geometry hides the road.");
        }
        finally{UnityEngine.Object.DestroyImmediate(explosion);}
        for(int seed=0;seed<500;seed++)
        {
            GateOfferGenerator.GeneratePair(seed,2,out var left,out var right);
            Require(IsPositive(left.Operation)||IsPositive(right.Operation),"A gate pair forces two negative choices.");
        }
    }
    private static bool IsPositive(GateOperation operation)=>operation==GateOperation.Add||operation==GateOperation.Multiply;
    private static void Require(bool value,string message){if(!value)throw new InvalidOperationException(message);}
}
#endif
