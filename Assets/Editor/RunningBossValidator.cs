#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>Behavioural checks for boss movement, reactions, rewards and offline assets.</summary>
public static class RunningBossValidator
{
    public static void ValidateCommandLine()
    {
        RunningProjectValidator.ValidateCommandLine();
        UnityEditor.SceneManagement.EditorSceneManager.NewScene(UnityEditor.SceneManagement.NewSceneSetup.EmptyScene);
        ValidateMotion();
        ValidateBossMotionAndClear();
        ValidateElements();
        ValidateInventory();
        Debug.Log("RUNNING_BOSS_VALIDATION_PASS movement=60Hz camera=deadzone reactions=solo+team catalog=54 icons=offline rewards=unique+recent");
    }

    private static void ValidateMotion()
    {
        var root=new GameObject("MotorValidation",typeof(CharacterController));
        var motor=root.AddComponent<ArenaMotor>();
        try
        {
            root.transform.position=new Vector3(-10,1,-4);motor.SetSimulationEnabled(true);Physics.SyncTransforms();
            for(int i=0;i<60;i++)motor.Simulate(Vector2.right,Vector2.up,MotionPrediction.Step);
            float distance=root.transform.position.x+10;
            Require(distance>7&&distance<8.1f,$"Normal movement crawled: {distance:F3}m/s");
            motor.Restore(new Vector3(-10,1,-4),Vector3.zero);
            for(int i=0;i<60;i++)motor.Simulate(Vector2.right,Vector2.up,MotionPrediction.Step,.42f);
            float focused=root.transform.position.x+10;
            Require(focused>3&&focused<3.5f,$"Focus movement incorrect: {focused:F3}m/s");
            Vector3 center=new Vector3(0,1,0);
            Require(ArenaCameraRig.DeadZoneCenter(center,new Vector3(4,1,2),new Vector2(4.5f,3))==center,"Camera moves inside rectangle.");
            Require(ArenaCameraRig.DeadZoneCenter(center,new Vector3(7,1,-5),new Vector2(4.5f,3))==new Vector3(2.5f,1,-2),"Camera did not respect rectangle edges.");
            var pending=new MotionPrediction();
            foreach(ushort seq in new ushort[]{65534,65535,0,1})pending.Record(ArenaProtocol.Pack(seq,Vector2.right,Vector2.up,false));
            pending.Acknowledge(0);
            Require(pending.Pending.Count==1&&pending.Pending[0].Sequence==1,"Wrapped input acknowledgements dropped a future command.");
            Require(ArenaBossProjectile.SegmentDistance(Vector3.zero,Vector3.right*2,new Vector3(1,10,.5f))==.5f,"Bullet XZ sweep is incorrect.");
            Debug.Log($"BOSS_MOTION_METRICS normal={distance:F3}m focus={focused:F3}m per 60 steps");
        }
        finally { UnityEngine.Object.DestroyImmediate(root); }
    }

    private static void ValidateElements()
    {
        var solo=new BossElementState();var team=new BossElementState();
        solo.Apply(ElementType.Water,1,1);team.Apply(ElementType.Water,1,1);
        solo.Apply(ElementType.Lightning,1,2);team.Apply(ElementType.Lightning,2,2);
        Require(team.BreakGauge>solo.BreakGauge,"Cross-player elemental relay has no benefit.");
        bool broken=false;
        for(int i=0;i<12;i++)
        {
            solo.Apply(i%2==0?ElementType.Water:ElementType.Fire,1,3+i*.5f);
            broken|=solo.LastBrokeShield;
        }
        Require(broken,"A solo dual-element build cannot break mirror armor.");
        var freeze=new BossElementState();freeze.Apply(ElementType.Water,1,1);freeze.Apply(ElementType.Ice,1,2);
        Require(freeze.SlowedUntil>2&&freeze.SlowedUntil<6,"Freeze must have a bounded slowdown.");
        float before=freeze.BreakGauge;freeze.Apply(ElementType.Water,2,2.1f);
        Require(freeze.BreakGauge==before,"Reaction cooldown can be bypassed by another player.");
        Require(freeze.Apply(ElementType.None,1,20)>0,"Physical builds cannot damage the boss.");
    }

    private static void ValidateBossMotionAndClear()
    {
        for(int stage=1;stage<=3;stage++)
        {
            var motion=new ArenaBossMotion(new Vector3(0,1,7));motion.SetStage(stage);
            float min=0,max=0;
            for(int i=0;i<1800;i++)
            {
                Vector3 before=motion.Position;
                var position=motion.Tick(i/60f,1/60f,false);
                Require(Mathf.Abs(position.x)<=7.5f&&position.z>=4.5f&&position.z<=8.5f,"Boss left its upper-field movement area.");
                Require(Vector3.Distance(before,position)<3.3f/60,"Boss movement teleported or exceeds its speed limit.");
                min=Mathf.Min(min,position.x);max=Mathf.Max(max,position.x);
            }
            Require(max-min>8,$"Card {stage} does not move across the field.");
            Vector3 held=motion.Position;motion.HoldUntil(40);
            Require(motion.Tick(31,1,false)==held,"Boss drifted during aimed-shot/exposure hold.");
        }
        var normal=new ArenaBossMotion(new Vector3(0,1,7));var slowed=new ArenaBossMotion(new Vector3(0,1,7));
        for(int i=0;i<120;i++){normal.Tick(i/60f,1/60f,false);slowed.Tick(i/60f,1/60f,true);}
        Require(slowed.Position.x<normal.Position.x*.7f,"Freeze does not slow boss movement.");
        ArenaBossProjectile inside=null,outside=null;
        try
        {
            inside=ArenaBossProjectile.Spawn(Vector3.zero,Vector3.right,2,60,1,.1f,false,0,0,10);
            outside=ArenaBossProjectile.Spawn(Vector3.zero,Vector3.right,2,60,1,.1f,false,0,0,10);
            ArenaBossProjectile.Clear(Vector3.zero,.1f,15);
            Require(!inside.IsCleared&&!outside.IsCleared,"Local clear used a stale visual/origin position.");
            ArenaBossProjectile.Clear(Vector3.right*10,.1f,15);
            Require(inside.IsCleared&&outside.IsCleared&&inside!=null,"Server-time clear did not dissolve bullets before their first Update.");
            Require(ArenaBossProjectile.EvaluatePosition(Vector3.zero,Vector3.forward,5,.4f,100,100.2)==Vector3.zero,"Warning bullets moved before arming.");
        }
        finally
        {
            if(inside!=null)UnityEngine.Object.DestroyImmediate(inside.gameObject);
            if(outside!=null)UnityEngine.Object.DestroyImmediate(outside.gameObject);
        }
        Debug.Log("BOSS_MOTION_CLEAR_VALIDATION_PASS cards=3 bounds speed hold freeze clear=server-time+dissolve");
    }

    private static void ValidateInventory()
    {
        var catalog=RuntimeItemCatalog.Default;
        Require(catalog!=null&&catalog.Items.Count==54,"Expected 54 registered items.");
        var ids=new HashSet<int>();var categories=new HashSet<ItemCategory>();var candidates=new List<UpgradeData>();
        foreach(var item in catalog.Items)
        {
            Require(item!=null&&ids.Add(item.NetworkId),"Null or duplicate stable item ID.");
            Require(item.CreateRuntime()!=null,$"Missing runtime: {item.name}");
            Require(item.Icon!=null,$"Missing offline icon: {item.name}");categories.Add(item.Category);
            if(ItemAcquisitionPolicy.CanAcquire(item,ItemAcquisitionSource.LevelReward))candidates.Add(item);
        }
        Require(categories.Count==3,"Passive/active/temporary categories are incomplete.");
        var recent=new HashSet<int>();var owned=new HashSet<int>();
        for(int round=0;round<4;round++)
        {
            var offer=RewardOfferSampler.Draw(candidates,owned,recent,1234+round);
            Require(offer.Length==3&&new HashSet<int>(offer).Count==3,"Reward must contain three different items.");
            var repeat=RewardOfferSampler.Draw(candidates,owned,recent,1234+round);
            for(int i=0;i<3;i++)Require(offer[i]==repeat[i]&&!recent.Contains(offer[i]),"Reward sampling repeats recent offers or is not deterministic.");
            foreach(int id in offer)recent.Add(id);owned.Add(offer[0]);
        }
    }
    private static void Require(bool valid,string reason) { if(!valid)throw new InvalidOperationException(reason); }
}
#endif
