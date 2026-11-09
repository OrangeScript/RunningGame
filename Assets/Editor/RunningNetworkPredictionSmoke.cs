#if UNITY_EDITOR
using System;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;

/// <summary>Launch Host and Client in separate editor projects. Real UTP delay,
/// real owner input, reconciliation, focus speed and mirrored equipment.</summary>
[InitializeOnLoad]
public static class RunningNetworkPredictionSmoke
{
    private const string SessionKey="Running.PredictionSmoke";
    private static int phase;
    private static double started,stepStarted;
    private static Vector3 initial,focusInitial;
    private static ArenaPlayerNetwork pawn;
    private static RunnerController runner;
    private static Keyboard keyboard;
    private static bool exiting;
    private static int inputStartFrame;
    static RunningNetworkPredictionSmoke(){EditorApplication.update+=Tick;}
    public static void BeginHost()=>Begin("host");
    public static void BeginClient()=>Begin("client");
    public static void BeginRunnerHost()=>Begin("runner-host");
    public static void BeginRunnerClient()=>Begin("runner-client");
    private static void Begin(string role)
    {
        SessionState.SetString(SessionKey,role);SessionState.SetString(SessionKey+".Start",DateTime.UtcNow.ToString("O"));
        EditorSceneManager.OpenScene("Assets/Scenes/Bootstrap.unity");EditorApplication.isPlaying=true;
    }
    private static void Tick()
    {
        string role=SessionState.GetString(SessionKey,"");if(role=="")return;
        if(exiting&&!EditorApplication.isPlayingOrWillChangePlaymode){EditorApplication.Exit(0);return;}
        if(!EditorApplication.isPlaying)return;
        try
        {
            if((DateTime.UtcNow-DateTime.Parse(SessionState.GetString(SessionKey+".Start",""))).TotalSeconds>180)throw new TimeoutException($"{role} phase={phase}");
            bool host=role.Contains("host"),road=role.Contains("runner");
            var manager=NetworkManager.Singleton;var room=UnityEngine.Object.FindFirstObjectByType<RoomSession>();
            if(phase==0)
            {
                if(manager==null||room==null)return;
                manager.GetComponent<UnityTransport>().SetConnectionData("127.0.0.1",7883,"0.0.0.0");
#pragma warning disable 0618
                manager.GetComponent<UnityTransport>().SetDebugSimulatorParameters(80,10,0);
#pragma warning restore 0618
                Application.targetFrameRate=120;
                InputSystem.settings.backgroundBehavior=InputSettings.BackgroundBehavior.IgnoreFocus;
                InputSystem.settings.editorInputBehaviorInPlayMode=InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
                if(host){room.CreateRoom();if(!road)room.SelectArena();}else room.JoinRoom();
                phase=1;return;
            }
            if(host)
            {
                if(phase==1&&manager.ConnectedClientsIds.Count==2){room.StartMatch();phase=2;}
                if(phase>=2)
                {
                    var boss=UnityEngine.Object.FindFirstObjectByType<ArenaBossController>();if(boss!=null)boss.enabled=false;
                    if(road&&GameManager.instance!=null)PrepareRoad();
                    if(phase==2&&manager.ConnectedClientsIds.Count==2&&SceneManager.GetActiveScene().name==(road?"RunLoop":"Arena"))phase=3;
                    if(phase==3&&manager.ConnectedClientsIds.Count==1)Pass("HOST_PEER_COMPLETED");
                }
                return;
            }
            if(phase==1)
            {
                var obj=manager.LocalClient?.PlayerObject;if(obj==null||!obj.IsSpawned)return;
                pawn=obj.GetComponent<ArenaPlayerNetwork>();runner=obj.GetComponent<RunnerController>();
                if(road&&(runner==null||runner.Phase!=PlayerRunPhase.Running))return;
                if(!road&&(pawn==null||!pawn.IsLocallyPredicted||pawn.GetComponent<ItemManager>().itemCount<2))return;
                keyboard=InputSystem.AddDevice<Keyboard>("PredictionKeyboard");
                var input=obj.GetComponent<PlayerInput>();
                UnityEngine.InputSystem.Users.InputUser.PerformPairingWithDevice(keyboard,input.user);
                input.actions.devices=new InputDevice[]{keyboard};input.ActivateInput();
                stepStarted=Time.realtimeSinceStartupAsDouble;phase=7;return;
            }
            Transform target=road?runner.transform:pawn.transform;
            double elapsed=Time.realtimeSinceStartupAsDouble-stepStarted;
            if(phase==7&&elapsed>.6)
            {
                initial=target.position;stepStarted=Time.realtimeSinceStartupAsDouble;inputStartFrame=Time.frameCount;
                SetKeys(Key.D,Key.Space);phase=2;return;
            }
            if(phase==2&&elapsed>=.075&&Time.frameCount-inputStartFrame>=3)
            {
                float moved=target.position.x-initial.x;
                var moveAction=target.GetComponent<PlayerInput>().actions.FindAction(road?"Move":"Arena/Move");
                Require(moved>.08f,$"Owner did not respond: {moved:F3} key={keyboard.dKey.isPressed} action={moveAction.ReadValueAsObject()} frames={Time.frameCount-inputStartFrame}");
                if(!road)Require(UnityEngine.Object.FindObjectsByType<ArenaRuntimeProjectile>(FindObjectsSortMode.None).Length>0,"Predicted shot is missing.");
                Debug.Log($"CLIENT_IMMEDIATE_RESPONSE moved={moved:F3} after={elapsed:F3}s with UTP delay=80ms/peer");phase=3;
            }
            if(phase==3&&elapsed>=.8)
            {
                float moved=target.position.x-initial.x;
                Require(moved>(road?4:4.5f),$"Sustained owner speed too low: {moved:F3}");
                SetKeys();phase=4;stepStarted=Time.realtimeSinceStartupAsDouble;return;
            }
            if(phase==4&&elapsed>1)
            {
                if(road){Pass("RUNNING_CLIENT_PREDICTION_PASS runner immediate+speed+stop");return;}
                float error=Vector3.Distance(target.position,pawn.LatestAuthorityPosition);
                Require(error<.25f,$"Permanent owner/server drift remains: {error:F3}");
                focusInitial=target.position;SetKeys(Key.A,Key.LeftShift);stepStarted=Time.realtimeSinceStartupAsDouble;phase=5;return;
            }
            if(phase==5&&elapsed>=1)
            {
                float moved=focusInitial.x-target.position.x;Require(moved>2.8f&&moved<4.2f,$"Focus speed incorrect: {moved:F3}");
                SetKeys();Debug.Log($"CLIENT_FOCUS_SPEED {moved:F3}m/s");phase=6;stepStarted=Time.realtimeSinceStartupAsDouble;return;
            }
            if(phase==6&&elapsed>1)
            {
                float error=Vector3.Distance(target.position,pawn.LatestAuthorityPosition);Require(error<.25f,$"Reconciliation error={error}");
                Pass($"RUNNING_CLIENT_PREDICTION_PASS arena immediate+cosmetic-shot+speed+focus+inventory drift={error:F4}");
            }
        }
        catch(Exception e){Debug.LogError("RUNNING_PREDICTION_SMOKE_FAIL "+e);SessionState.SetString(SessionKey,"");EditorApplication.Exit(1);}
    }
    private static bool roadPrepared;
    private static void PrepareRoad()
    {
        if(roadPrepared)return;
        var players=UnityEngine.Object.FindObjectsByType<RunnerController>(FindObjectsSortMode.None);if(players.Length<2)return;
        int i=0;foreach(var player in players){player.TeleportToLaneServer(i,new Vector3(i*22,0,0),7.25f);player.BeginRunServer();i++;}
        roadPrepared=true;
    }
    private static void SetKeys(params Key[] keys)=>InputSystem.QueueStateEvent(keyboard,new KeyboardState(keys));
    private static void Require(bool condition,string message){if(!condition)throw new InvalidOperationException(message);}
    private static void Pass(string message)
    {
        Debug.Log(message);SessionState.SetString(SessionKey,"");exiting=true;
        NetworkManager.Singleton?.Shutdown();EditorApplication.isPlaying=false;
        EditorApplication.delayCall+=()=>EditorApplication.Exit(0);
    }
}
#endif
