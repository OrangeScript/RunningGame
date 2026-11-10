#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using Running.Game.RunMap;
using Unity.Netcode;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>Real Bootstrap -> vote -> moving road -> loot -> reward -> next map.
/// Controlled collider probes test combat contracts before the natural road.</summary>
public static class RunningContinuousPlayModeSmoke
{
    private static int phase,roadSeed,initialItems;
    private static double started,step,naturalStarted;
    private static RunnerController runner;
    private static ItemManager items;
    private static CrowdManager health;
    private static AutoCombat gun;
    private static RunLootDirector loot;
    private static Enemy target,second;
    private static float targetHealth,secondHealth,naturalZ;
    private static Vector3 stationaryPosition,deathPosition;
    private static uint droppedId;
    private static int droppedItem;
    private static RunnerObstacleRuntime cover;
    private static GameObject probeFloor,coverRoot;
    private static Keyboard keyboard;
    private static bool priorOptions;
    private static EnterPlayModeOptions priorFlags;
    public static void Run()
    {
        RunningContinuousValidator.ValidateCommandLine();
        priorOptions=EditorSettings.enterPlayModeOptionsEnabled;priorFlags=EditorSettings.enterPlayModeOptions;
        EditorSettings.enterPlayModeOptionsEnabled=true;EditorSettings.enterPlayModeOptions=EnterPlayModeOptions.DisableDomainReload;
        EditorSceneManager.OpenScene("Assets/Scenes/Bootstrap.unity");phase=0;started=EditorApplication.timeSinceStartup;
        EditorApplication.update+=Tick;EditorApplication.isPlaying=true;
    }
    private static void Tick()
    {
        if(!EditorApplication.isPlaying)return;
        try
        {
            if(EditorApplication.timeSinceStartup-started>180)throw new TimeoutException("continuous phase="+phase);
            double elapsed=Time.timeAsDouble-step;
            switch(phase)
            {
                case 0:
                    var room=UnityEngine.Object.FindFirstObjectByType<RoomSession>();if(room==null||Time.frameCount<2)return;
                    NetworkManager.Singleton.GetComponent<Unity.Netcode.Transports.UTP.UnityTransport>().SetConnectionData("127.0.0.1",7886,"0.0.0.0");
                    room.StartSoloRunning();Next(1);break;
                case 1:
                    if(SceneManager.GetActiveScene().name!="RunLoop"||GameManager.instance==null)return;
                    runner=NetworkManager.Singleton.LocalClient?.PlayerObject?.GetComponent<RunnerController>();if(runner==null)return;
                    items=runner.GetComponent<ItemManager>();health=runner.GetComponent<CrowdManager>();gun=runner.GetComponent<AutoCombat>();
                    loot=GameManager.instance.GetComponent<RunLootDirector>();
                    var coordinator=UnityEngine.Object.FindFirstObjectByType<RunMapNetworkCoordinator>();
                    if(coordinator?.VoteSnapshot==null||!coordinator.VoteSnapshot.IsActive)return;
                    coordinator.InitializeRun(7813);coordinator.BeginVoteForReachableNodes(30);
                    int node=-1;
                    foreach(int id in coordinator.ReachableNodeIds)
                    {coordinator.MapSnapshot.TryGetNode(id,out var data);if(data.Type==RunMapNodeType.Combat){node=id;break;}}
                    if(node<0)
                    {
                        coordinator.InitializeRun(1);coordinator.BeginVoteForReachableNodes(30);
                        foreach(int id in coordinator.ReachableNodeIds)
                        {coordinator.MapSnapshot.TryGetNode(id,out var data);if(data.Type==RunMapNodeType.Combat||data.Type==RunMapNodeType.Elite){node=id;break;}}
                    }
                    Require(node>=0,"No reachable road node.");
                    var nodeButton=GameObject.Find("Node_"+node)?.GetComponent<Button>();
                    Require(nodeButton!=null,"Route button is missing.");nodeButton.onClick.Invoke();Next(2);break;
                case 2:
                    var sections=UnityEngine.Object.FindFirstObjectByType<SectionManager>();
                    if(sections?.CurrentPlan==null||GameManager.instance.state!=RunState.Running)return;
                    Require(!UnityEngine.Object.FindFirstObjectByType<RunMapRuntimeUI>().IsVisible,"Map remained visible over the road.");
                    roadSeed=sections.ActiveSeed;
                    if(gun.FiredShotCount==0)return;
                    Require(gun.LastShotDirection==Vector3.forward,"Automatic gun aimed toward a target.");
                    gun.enabled=false;
                    probeFloor=GameObject.CreatePrimitive(PrimitiveType.Cube);probeFloor.name="QA_ProbeFloor";
                    probeFloor.transform.position=new Vector3(100,-.55f,1200);probeFloor.transform.localScale=new Vector3(30,1,600);
                    runner.TeleportToLaneServer(7,new Vector3(100,0,1000),7.25f);
                    target=SpawnEnemy(new Vector3(103,0,1010),7);targetHealth=target.CurrentHealth;stationaryPosition=target.transform.position;
                    Fire(4);Next(3);break;
                case 3:
                    if(elapsed<.7)return;
                    Require(target!=null&&target.CurrentHealth==targetHealth,"A straight shot damaged an off-axis enemy.");
                    Require(target.transform.position==stationaryPosition,"A stationary defender moved.");Despawn(target);
                    target=SpawnEnemy(runner.transform.position+Vector3.forward*9,7);targetHealth=target.CurrentHealth;
                    Fire(999,false);Next(4);break;
                case 4:
                    if(elapsed<.6)return;
                    Require(target!=null&&target.CurrentHealth==targetHealth,"Cosmetic projectile mutated server health.");
                    initialItems=items.itemCount;deathPosition=target.transform.position;
                    Fire(999);Next(5);break;
                case 5:
                    if(elapsed<.55)return;
                    Require(target==null||target.IsDead,"Aligned actual projectile failed to kill.");
                    droppedId=loot.LastSpawnedPickupId;
                    Require(loot.TryGetPickup(droppedId,out var point,out int lane,out droppedItem)&&lane==7&&droppedItem!=0,"Kill did not create a lane-owned passive.");
                    Require(Vector2.Distance(new Vector2(point.x,point.z),new Vector2(deathPosition.x,deathPosition.z))<.05f,"Drop did not remain at the defeated enemy.");
                    RunningPresentationCapture.Capture("running-drop");Next(6);break;
                case 6:
                    if(items.itemCount<=initialItems)return;
                    Require(items.Owns(droppedItem)&&!loot.TryGetPickup(droppedId,out _,out _,out _),"Physical pickup failed to grant/remove exactly once.");
                    Debug.Log("RUNNING_ACTUAL_FORWARD_HITS_PASS off-axis=miss aligned=hit cosmetic=no-damage kill=original-position pickup=automatic");
                    target=SpawnEnemy(runner.transform.position+new Vector3(1.4f,0,10),7);targetHealth=target.CurrentHealth;
                    var guided=new ProjectileSpec{damage=2,speed=22,homingTurnRate=28,homingMaxAngle=12,homingAcquireRange=24};
                    Fire(guided);Next(7);break;
                case 7:
                    if(elapsed<.65)return;
                    Require(target!=null&&target.CurrentHealth<targetHealth,"Obtained mild guidance did not bend toward a forward target.");Despawn(target);
                    target=SpawnEnemy(runner.transform.position+Vector3.forward*8,7);second=SpawnEnemy(runner.transform.position+Vector3.forward*12,7);
                    targetHealth=target.CurrentHealth;secondHealth=second.CurrentHealth;
                    Fire(new ProjectileSpec{damage=2,speed=22,pierce=1});Next(8);break;
                case 8:
                    if(elapsed<.7)return;
                    Require(target.CurrentHealth<targetHealth&&second.CurrentHealth<secondHealth,"Piercing did not hit two collinear enemies.");Despawn(target);Despawn(second);
                    coverRoot=new GameObject("QA_Cover");coverRoot.transform.position=new Vector3(runner.transform.position.x,0,runner.transform.position.z);
                    cover=RunnerObstacleRuntime.Create(coverRoot.transform,new RunnerObstaclePlan(ProceduralObstacleKind.Crate,
                        new Vector3(0,1,7),new Vector3(2,2,1),0,0,0,8,8),7,true,0,991,88);
                    target=SpawnEnemy(runner.transform.position+Vector3.forward*11,7);targetHealth=target.CurrentHealth;
                    Fire(2);Next(9);break;
                case 9:
                    if(elapsed<.6)return;
                    Require(target.CurrentHealth==targetHealth&&cover.HealthFraction<1,"Cover failed to intercept a real shot.");
                    cover.HitByProjectile(999,ElementType.None);Fire(2);Next(10);break;
                case 10:
                    if(elapsed<.7)return;
                    Require(target.CurrentHealth<targetHealth&&!cover.BlocksProjectiles,"Destroyed cover still blocked the firing lane.");Despawn(target);
                    UnityEngine.Object.Destroy(coverRoot);
                    VerifyGate();Next(11);break;
                case 11:
                    if(elapsed<.3)return;
                    RunningPresentationCapture.Capture("running-item-gate");
                    health.AddShield(40);int before=health.Health;int shield=health.Shield;
                    int expected=Mathf.Max(1,Mathf.RoundToInt(10*(1-items.GetRunnerStats().DamageReduction)));
                    Require(health.TryDamageHealth(10)&&health.Health==before&&health.Shield==shield-expected,"Runner armor/shield does not absorb actual damage.");
                    Require(health.MaxHealth>=125,"Armor did not increase maximum health.");
                    Require(!health.TryDamageHealth(10),"Runner hit invulnerability does not prevent stacked same-frame hits.");
                    RebuildNaturalRoad();Next(12);break;
                case 12:
                    DriveRoad();
                    if(GameManager.instance.state==RunState.GameOver)throw new InvalidOperationException("Autopilot was eliminated on the natural road.");
                    if(GameManager.instance.state==RunState.UpgradeSelection){SetKeys();Next(13);return;}
                    float expectedZ=naturalZ+(float)(Time.timeAsDouble-naturalStarted)*runner.ForwardSpeed;
                    Require(runner.transform.position.z>=expectedZ-.5f,$"Runner stopped: actual={runner.transform.position.z:F3} expected={expectedZ:F3} phase={runner.Phase} dt={Time.deltaTime:F3} collision={runner.GetComponent<CharacterController>().collisionFlags}");
                    if(elapsed>8&&elapsed<8.15)RunningPresentationCapture.Capture("running-forward");
                    break;
                case 13:
                    var buttons=UnityEngine.Object.FindObjectsByType<UpgradeCardUI>(FindObjectsSortMode.None);
                    if(buttons.Length==0){Require(elapsed<8,"No active end-of-road choice card.");return;}
                    RunningPresentationCapture.Capture("running-reward");initialItems=items.itemCount;
                    buttons[0].GetComponent<Button>().onClick.Invoke();Next(14);break;
                case 14:
                    if(GameManager.instance.state!=RunState.MapSelection)return;
                    Require(items.itemCount==initialItems+1,"End reward did not grant an item.");
                    Require(loot.ActivePickupCount==0&&Enemy.ActiveEnemies.Count==0,"Previous road left live enemies or pickups over the map.");
                    Next(15);break;
                case 15:
                    if(elapsed<.5)return;
                    health.TryDamageHealth(99999);if(health.Health>0)return;Next(16);break;
                case 16:
                    if(GameManager.instance.state!=RunState.GameOver)return;
                    var retryButton=GameObject.Find("RetryRun")?.GetComponent<Button>();if(retryButton==null)return;
                    RunningPresentationCapture.Capture("running-defeat");retryButton.onClick.Invoke();Next(17);break;
                case 17:
                    var freshObject=NetworkManager.Singleton.LocalClient?.PlayerObject;
                    if(freshObject==null||!freshObject.IsSpawned)return;
                    var fresh=freshObject.GetComponent<RunnerController>();
                    if(fresh==null||fresh==runner||GameManager.instance==null||GameManager.instance.state!=RunState.MapSelection)return;
                    Require(fresh.GetComponent<ItemManager>().itemCount==0&&fresh.GetComponent<CrowdManager>().Health==100&&fresh.GetComponent<CrowdManager>().Shield==0,"Fresh run retained old equipment, damage or shields.");
                    Debug.Log("RUNNING_CONTINUOUS_PLAYMODE_PASS stationary+straight+homing+pierce+cover gate=deterministic+once drop=pickup defense=real road=continuous reward=three-choice next-map=clean defeat+retry=fresh");Finish(0);break;
            }
        }
        catch(Exception e){Debug.LogError("RUNNING_CONTINUOUS_PLAYMODE_FAIL "+e);Finish(1);}
    }
    private static void Next(int value){phase=value;step=Time.timeAsDouble;}
    private static Enemy SpawnEnemy(Vector3 point,int lane)
    {
        point.y=0;var prefab=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Enemy.prefab");
        var enemy=UnityEngine.Object.Instantiate(prefab,point,Quaternion.identity).GetComponent<Enemy>();
        enemy.ConfigureServer(7819+phase,lane,1,EnemyArchetype.Chaser);enemy.NetworkObject.Spawn(true);enemy.enabled=false;
        Physics.SyncTransforms();return enemy;
    }
    private static void Despawn(Enemy enemy){if(enemy!=null&&enemy.IsSpawned)enemy.NetworkObject.Despawn(true);}
    private static void Fire(float damage,bool authority=true)=>Fire(new ProjectileSpec{damage=damage,speed=22},authority);
    private static void Fire(ProjectileSpec spec,bool authority=true)
    {
        var prefab=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Projectile.prefab").GetComponent<Projectile>();
        var shot=UnityEngine.Object.Instantiate(prefab,runner.transform.position+new Vector3(0,.9f,.8f),Quaternion.identity);
        shot.InitializeForward(spec,Vector3.forward,runner.LaneId,runner.OwnerClientId,36,authority,runner);
    }
    private static void VerifyGate()
    {
        Gate[] pair=null;
        var sections=UnityEngine.Object.FindFirstObjectByType<SectionManager>();
        foreach(var piece in sections.SpawnedSections){var gates=piece.GetComponentsInChildren<Gate>();if(gates.Length==2){pair=gates;break;}}
        Require(pair!=null,"Road has no gate pair.");
        Array.Sort(pair,(a,b)=>a.transform.position.x.CompareTo(b.transform.position.x));
        pair[0].ConfigureServer(new GateOffer(GateOperation.Add,8),0,1344);
        pair[1].ConfigureServer(new GateOffer(GateOperation.Multiply,2),0,1341);
        float center=(pair[0].transform.position.x+pair[1].transform.position.x)*.5f;
        runner.TeleportToLaneServer(0,new Vector3(center,0,pair[0].transform.position.z-1.5f),7.25f);
        int power=health.Firepower;
        pair[1].SendMessage("OnTriggerEnter",runner.GetComponent<CharacterController>());
        pair[0].SendMessage("OnTriggerEnter",runner.GetComponent<CharacterController>());
        Require(health.Firepower==power+8&&items.Owns(1344),"Gate center depended on trigger callback order or did not grant its icon's passive.");
        int count=items.itemCount;power=health.Firepower;
        runner.TeleportToLaneServer(0,new Vector3(pair[1].transform.position.x,0,pair[1].transform.position.z-1.5f),7.25f);
        pair[1].SendMessage("OnTriggerEnter",runner.GetComponent<CharacterController>());
        Require(health.Firepower==power&&items.itemCount==count,"Gate pair granted twice.");
        Debug.Log("RUNNING_GATE_ACTUAL_PASS center=deterministic passive=granted pair=once");
    }
    private static void RebuildNaturalRoad()
    {
        Projectile.ClearLane(7);RunnerEnemyProjectileSystem.Instance?.ClearServer();loot.ClearRoadServer();
        if(probeFloor!=null)UnityEngine.Object.Destroy(probeFloor);
        var sections=UnityEngine.Object.FindFirstObjectByType<SectionManager>();
        Require(sections.TryBuildFiniteRun(roadSeed,1),"Natural road rebuild failed.");
        var start=sections.CurrentPlan.Lanes[0].WorldOrigin;
        runner.TeleportToLaneServer(0,start,7.25f);runner.BeginRunServer();health.SetPopulation(20);gun.enabled=true;
        InputSystem.settings.backgroundBehavior=InputSettings.BackgroundBehavior.IgnoreFocus;
        InputSystem.settings.editorInputBehaviorInPlayMode=InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
        keyboard=InputSystem.AddDevice<Keyboard>("ContinuousSmokeKeyboard");
        var input=runner.GetComponent<PlayerInput>();UnityEngine.InputSystem.Users.InputUser.PerformPairingWithDevice(keyboard,input.user);
        input.actions.devices=new InputDevice[]{keyboard};input.ActivateInput();
        naturalStarted=Time.timeAsDouble;naturalZ=start.z;
    }
    private static void DriveRoad()
    {
        float x=runner.LaneCenterX,z=runner.transform.position.z,best=30;
        foreach(var enemy in Enemy.ActiveEnemies)
        {
            if(enemy==null||enemy.IsDead||enemy.LaneId!=runner.LaneId)continue;
            float distance=enemy.transform.position.z-z;
            if(distance>2&&distance<best){best=distance;x=enemy.transform.position.x;}
        }
        for(uint id=1;id<=loot.LastSpawnedPickupId;id++)
            if(loot.TryGetPickup(id,out var position,out int lane,out _)&&lane==runner.LaneId&&position.z-z>-.5f&&position.z-z<10)
                x=position.x;
        foreach(var obstacle in RunnerObstacleRuntime.ActiveObstacles)
        {
            if(obstacle==null||obstacle.IsDestroyed||obstacle.LaneId!=runner.LaneId)continue;
            float dz=obstacle.transform.position.z-z;if(dz<0||dz>7)continue;
            Vector2 bounds=obstacle.Plan.SweptXBounds+Vector2.one*runner.LaneCenterX;
            if(x>=bounds.x-.6f&&x<=bounds.y+.6f)x=bounds.x>runner.LaneCenterX-4?runner.LaneCenterX-6:runner.LaneCenterX+6;
        }
        foreach(var bullet in UnityEngine.Object.FindObjectsByType<RunnerEnemyProjectile>(FindObjectsSortMode.None))
            if(bullet.IsAuthoritative&&!bullet.IsDissolving&&bullet.LaneId==runner.LaneId&&
                bullet.transform.position.z>z&&bullet.transform.position.z-z<6&&Mathf.Abs(bullet.transform.position.x-runner.transform.position.x)<1)
                x=runner.transform.position.x<runner.LaneCenterX+3?runner.transform.position.x+2:runner.transform.position.x-2;
        float difference=x-runner.transform.position.x;
        if(difference>.2f)SetKeys(Key.D);else if(difference<-.2f)SetKeys(Key.A);else SetKeys();
    }
    private static void SetKeys(params Key[] keys){if(keyboard!=null)InputSystem.QueueStateEvent(keyboard,new KeyboardState(keys));}
    private static void Require(bool value,string message){if(!value)throw new InvalidOperationException(message);}
    private static void Finish(int code)
    {
        EditorApplication.update-=Tick;NetworkManager.Singleton?.Shutdown();EditorApplication.isPlaying=false;
        EditorSettings.enterPlayModeOptionsEnabled=priorOptions;EditorSettings.enterPlayModeOptions=priorFlags;
        EditorApplication.delayCall+=()=>EditorApplication.Exit(code);
    }
}
#endif
