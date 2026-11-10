#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using Running.Game.RunMap;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Two-peer road integration using real NGO/UTP with delay. Both players
/// vote through UI, then verify client shots, enemy fire, loot and cover state.</summary>
[InitializeOnLoad]
public static class RunningContinuousNetworkSmoke
{
    private const string Key="Running.ContinuousNetwork";
    private static int phase;
    private static double step;
    private static bool offered,broken;
    private static bool seeded;
    private static readonly Dictionary<Enemy,Vector3> enemyPositions=new Dictionary<Enemy,Vector3>();
    static RunningContinuousNetworkSmoke(){EditorApplication.update+=Tick;}
    public static void BeginHost()=>Begin("host");
    public static void BeginClient()=>Begin("client");
    private static void Begin(string role)
    {
        SessionState.SetString(Key,role);SessionState.SetString(Key+".Start",DateTime.UtcNow.ToString("O"));
        EditorSceneManager.OpenScene("Assets/Scenes/Bootstrap.unity");EditorApplication.isPlaying=true;
    }
    private static void Tick()
    {
        string role=SessionState.GetString(Key,"");if(role==""||!EditorApplication.isPlaying)return;
        try
        {
            if((DateTime.UtcNow-DateTime.Parse(SessionState.GetString(Key+".Start",""))).TotalSeconds>180)
                throw new TimeoutException($"{role} phase={phase}");
            bool host=role=="host";var manager=NetworkManager.Singleton;
            var room=UnityEngine.Object.FindFirstObjectByType<RoomSession>();
            if(phase==0)
            {
                if(manager==null||room==null)return;
                manager.GetComponent<UnityTransport>().SetConnectionData("127.0.0.1",7888,"0.0.0.0");
#pragma warning disable 0618
                manager.GetComponent<UnityTransport>().SetDebugSimulatorParameters(80,10,0);
#pragma warning restore 0618
                Application.targetFrameRate=120;
                if(host)room.CreateRoom();else room.JoinRoom();phase=1;return;
            }
            if(host&&phase==1)
            {if(manager.ConnectedClientsIds.Count==2){room.StartMatch();phase=2;}return;}
            var game=GameManager.instance;var vote=UnityEngine.Object.FindFirstObjectByType<RunMapNetworkCoordinator>();
            if(game==null||vote?.MapSnapshot==null||manager.LocalClient?.PlayerObject==null)return;
            if((host&&phase==2)||(!host&&phase==1))
            {
                if(host&&!seeded){vote.InitializeRun(1);vote.BeginVoteForReachableNodes(30);seeded=true;}
                if(vote.MapSnapshot.Seed!=1||vote.VoteSnapshot==null||!vote.VoteSnapshot.IsActive)return;
                int choice=-1;
                foreach(int id in vote.ReachableNodeIds)
                {vote.MapSnapshot.TryGetNode(id,out var node);if(node.Type==RunMapNodeType.Combat||node.Type==RunMapNodeType.Elite){choice=id;break;}}
                Require(choice>=0,"Shared seeded map has no road candidate.");
                var button=GameObject.Find("Node_"+choice)?.GetComponent<Button>();if(button==null||!button.interactable)return;
                button.onClick.Invoke();phase=3;return;
            }
            if(phase==3)
            {
                if(game.state!=RunState.Running)return;
                Require(!UnityEngine.Object.FindFirstObjectByType<RunMapRuntimeUI>().IsVisible,"Client map did not hide after both votes.");
                var self=manager.LocalClient.PlayerObject.GetComponent<RunnerController>();if(!self.IsAdvancing)return;
                foreach(var enemy in Enemy.ActiveEnemies)if(enemy!=null)enemyPositions[enemy]=enemy.transform.position;
                step=Time.realtimeSinceStartupAsDouble;phase=4;return;
            }
            double elapsed=Time.realtimeSinceStartupAsDouble-step;
            var loot=game.GetComponent<RunLootDirector>();
            if(host)
            {
                if(offered&&broken&&elapsed>5&&manager.ConnectedClientsIds.Count==1)
                {Finish(0,"RUNNING_CONTINUOUS_NETWORK_HOST_PASS");return;}
                RunnerController client=null;
                foreach(var runner in RunnerController.ActiveRunners)if(runner.OwnerClientId!=manager.LocalClientId)client=runner;
                if(client==null)return;
                if(!offered&&elapsed>1.8)
                {loot.SpawnServer(client.transform.position+Vector3.forward*6,client.LaneId,RuntimeItemCatalog.Default.Find(1341),ItemRarity.Uncommon);offered=true;}
                if(!broken&&elapsed>4)
                {
                    foreach(var obstacle in RunnerObstacleRuntime.ActiveObstacles)
                    {
                        if(obstacle.LaneId!=client.LaneId||obstacle.Plan.Health<=0)continue;
                        obstacle.HitByProjectile(9999,ElementType.None);broken=true;break;
                    }
                    Require(broken,"No destructible cover was generated for client lane.");
                }
                return;
            }
            if(elapsed<5)return;
            var pawn=manager.LocalClient.PlayerObject;
            var gun=pawn.GetComponent<AutoCombat>();var inventory=pawn.GetComponent<ItemManager>();
            Require(gun.FiredShotCount>=3&&gun.LastShotDirection==Vector3.forward,"Client owner did not predict forward automatic fire.");
            Require(inventory.Owns(1341)&&inventory.itemCount>=1,"Replicated physical loot did not reach client inventory.");
            var spec=new ProjectileSpec();inventory.ModifyProjectile(spec);Require(spec.homingTurnRate==28,"Client build did not enable the granted guidance effect.");
            Require(RunnerEnemyProjectileSystem.Instance!=null&&RunnerEnemyProjectileSystem.Instance.ClientSpawnCount>0,"Client cannot see enemy projectiles.");
            foreach(var pair in enemyPositions)
                if(pair.Key!=null&&!pair.Key.IsDead)Require(Vector3.Distance(pair.Value,pair.Key.transform.position)<.01f,"A client defender moved.");
            bool clientBroken=false;
            foreach(var obstacle in UnityEngine.Object.FindObjectsByType<RunnerObstacleRuntime>(FindObjectsInactive.Include,FindObjectsSortMode.None))
                if(obstacle.LaneId==pawn.GetComponent<RunnerController>().LaneId&&obstacle.IsDestroyed)clientBroken=true;
            Require(clientBroken,"Server cover destruction did not reach client.");
            Debug.Log($"CONTINUOUS_CLIENT_EVIDENCE shots={gun.FiredShotCount} inventory={inventory.itemCount} enemyShots={RunnerEnemyProjectileSystem.Instance.ClientSpawnCount} guidance={spec.homingTurnRate} cover=broken map=hidden");
            Finish(0,"RUNNING_CONTINUOUS_NETWORK_CLIENT_PASS delay=80ms/peer vote+forward-fire+enemy-shots+pickup+inventory+cover");
        }
        catch(Exception e){Finish(1,"RUNNING_CONTINUOUS_NETWORK_FAIL "+e);}
    }
    private static void Require(bool value,string message){if(!value)throw new InvalidOperationException(message);}
    private static void Finish(int code,string message)
    {
        if(code==0)Debug.Log(message);else Debug.LogError(message);
        SessionState.SetString(Key,"");NetworkManager.Singleton?.Shutdown();EditorApplication.isPlaying=false;
        EditorApplication.delayCall+=()=>EditorApplication.Exit(code);
    }
}
#endif
