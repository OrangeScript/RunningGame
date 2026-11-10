#if UNITY_EDITOR
using System;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>Two real peers with UTP latency: boss movement, bullet lifetime,
/// timestamped local clear and survival of bullets outside the clear.</summary>
[InitializeOnLoad]
public static class RunningBossNetworkSmoke
{
    private const string SessionKey="Running.BossNetworkSmoke";
    private static int phase;
    private static double step;
    private static Vector3 initialBoss;
    private static ArenaBossProjectile inside,outside;
    static RunningBossNetworkSmoke() { EditorApplication.update+=Tick; }
    public static void BeginHost()=>Begin("host");
    public static void BeginClient()=>Begin("client");
    private static void Begin(string role)
    {
        SessionState.SetString(SessionKey,role);
        SessionState.SetString(SessionKey+".Start",DateTime.UtcNow.ToString("O"));
        EditorSceneManager.OpenScene("Assets/Scenes/Bootstrap.unity");EditorApplication.isPlaying=true;
    }
    private static void Tick()
    {
        string role=SessionState.GetString(SessionKey,"");
        if(role==""||!EditorApplication.isPlaying)return;
        try
        {
            if((DateTime.UtcNow-DateTime.Parse(SessionState.GetString(SessionKey+".Start",""))).TotalSeconds>150)
                throw new TimeoutException($"{role} phase={phase}");
            bool host=role=="host";
            var manager=NetworkManager.Singleton;var room=UnityEngine.Object.FindFirstObjectByType<RoomSession>();
            if(phase==0)
            {
                if(manager==null||room==null)return;
                manager.GetComponent<UnityTransport>().SetConnectionData("127.0.0.1",7883,"0.0.0.0");
#pragma warning disable 0618
                manager.GetComponent<UnityTransport>().SetDebugSimulatorParameters(80,10,0);
#pragma warning restore 0618
                Application.targetFrameRate=120;
                if(host){room.CreateRoom();room.SelectArena();}else room.JoinRoom();
                phase=1;return;
            }
            if(host&&phase==1)
            {
                if(manager.ConnectedClientsIds.Count==2){room.StartMatch();phase=2;}
                return;
            }
            var arena=UnityEngine.Object.FindFirstObjectByType<ArenaBootstrap>();
            if(arena==null||arena.BossMaxHealth<=0||manager.LocalClient?.PlayerObject==null)return;
            if((host&&phase==2)||(!host&&phase==1))
            {
                if(host)
                    foreach(var pawn in ArenaPlayerNetwork.ActivePlayers)pawn.GrantShieldServer(100,60);
                initialBoss=arena.BossPosition;step=Time.realtimeSinceStartupAsDouble;phase=3;return;
            }
            double elapsed=Time.realtimeSinceStartupAsDouble-step;
            if(host)
            {
                if(phase==3&&elapsed>5.5)
                {
                    var boss=UnityEngine.Object.FindFirstObjectByType<ArenaBossController>();boss.enabled=false;
                    arena.ClearBossBullets();
                    arena.SpawnBossBullet(new Vector3(-10,.75f,0),Vector3.right,2,30,1,.14f,0);
                    arena.SpawnBossBullet(new Vector3(-10,.75f,5),Vector3.right,2,30,1,.14f,0);
                    step=Time.realtimeSinceStartupAsDouble;phase=4;return;
                }
                if(phase==4&&elapsed>2)
                {
                    var probe=FindProbe(0);Require(probe!=null,"Server probe disappeared early.");
                    Vector3 clearAt=probe.PositionAt(manager.ServerTime.Time);
                    arena.ClearBossBullets(clearAt,.06f,ArenaBulletClearReason.GrazeBurst);
                    Require(probe.IsCleared,"Server failed its local clear.");phase=5;return;
                }
                if(phase==5&&manager.ConnectedClientsIds.Count==1)Finish(0,"RUNNING_BOSS_NETWORK_HOST_PASS");
                return;
            }
            if(phase==3&&elapsed>3)
            {
                Require(arena.ReceivedBossPoseCount>=20,"Client did not receive the boss movement stream.");
                Require(Vector3.Distance(initialBoss,arena.BossPosition)>1.5f,"Client boss is stationary.");
                Require(ArenaBossProjectile.LiveCount>10,"Client did not receive the boss's bullet waves.");
                Debug.Log($"BOSS_CLIENT_MOVEMENT poses={arena.ReceivedBossPoseCount} distance={Vector3.Distance(initialBoss,arena.BossPosition):F3}");
                phase=4;return;
            }
            if(phase==4)
            {
                inside=FindProbe(0);outside=FindProbe(5);
                if(inside==null||outside==null)return;
                Require(!inside.IsCleared&&!outside.IsCleared,"Probe was cleared before the local clear event.");
                phase=5;return;
            }
            if(phase==5&&(inside==null||inside.IsCleared))
            {
                Require(outside!=null&&!outside.IsCleared,"Local clear removed a bullet outside its radius.");
                Require(arena.ClearNotice.Contains("擦弹"),"Client missed the clear reason.");
                Require(inside!=null,"Bullet popped away instead of dissolving.");
                Debug.Log("BOSS_CLIENT_CLEAR visible-dissolve=true outside-survives=true radius=0.06 server-time=true");
                Finish(0,"RUNNING_BOSS_NETWORK_CLIENT_PASS movement+waves+timestamped-clear+dissolve with 80ms delay/peer");
            }
        }
        catch(Exception e){Finish(1,"RUNNING_BOSS_NETWORK_FAIL "+e);}
    }
    private static ArenaBossProjectile FindProbe(float z)
    {
        foreach(var bullet in UnityEngine.Object.FindObjectsByType<ArenaBossProjectile>(FindObjectsSortMode.None))
        {
            var origin=bullet.PositionAt(bullet.BornTime);
            if(!bullet.IsCleared&&Mathf.Abs(origin.x+10)<.01f&&Mathf.Abs(origin.z-z)<.01f)return bullet;
        }
        return null;
    }
    private static void Require(bool condition,string message) { if(!condition)throw new InvalidOperationException(message); }
    private static void Finish(int code,string message)
    {
        if(code==0)Debug.Log(message);else Debug.LogError(message);
        SessionState.SetString(SessionKey,"");NetworkManager.Singleton?.Shutdown();EditorApplication.isPlaying=false;
        EditorApplication.delayCall+=()=>EditorApplication.Exit(code);
    }
}
#endif
