#if UNITY_EDITOR
using System;
using Unity.Netcode;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>Exercises the actual practice entry, three spell cards, hit
/// invulnerability, elemental break, active bomb, victory and retry.</summary>
public static class RunningBossEncounterSmoke
{
    private static int phase,reactionHits;
    private static double start,step;
    private static ArenaBossController boss;
    private static ArenaPlayerNetwork pawn;
    private static ArenaBootstrap arena;
    private static UnityEngine.InputSystem.Keyboard focusKeyboard;
    private static bool previousOptions;
    private static EnterPlayModeOptions previousFlags;
    public static void Run()
    {
        RunningBossValidator.ValidateCommandLine();
        previousOptions=EditorSettings.enterPlayModeOptionsEnabled;previousFlags=EditorSettings.enterPlayModeOptions;
        EditorSettings.enterPlayModeOptionsEnabled=true;EditorSettings.enterPlayModeOptions=EnterPlayModeOptions.DisableDomainReload;
        EditorSceneManager.OpenScene("Assets/Scenes/Bootstrap.unity");phase=0;start=EditorApplication.timeSinceStartup;
        EditorApplication.update+=Tick;EditorApplication.isPlaying=true;
    }
    private static void Tick()
    {
        if(!EditorApplication.isPlaying)return;
        try
        {
            if(focusKeyboard!=null&&phase>=2&&phase<=7)
                UnityEngine.InputSystem.InputSystem.QueueStateEvent(focusKeyboard,new UnityEngine.InputSystem.LowLevel.KeyboardState(UnityEngine.InputSystem.Key.LeftShift));
            if(EditorApplication.timeSinceStartup-start>100)throw new TimeoutException("Boss encounter phase="+phase);
            if(phase==0)
            {
                var room=UnityEngine.Object.FindFirstObjectByType<RoomSession>();if(room==null)return;
                RunningPresentationCapture.Capture("boss-menu");room.StartSoloBoss();phase=1;return;
            }
            if(phase==1)
            {
                boss=UnityEngine.Object.FindFirstObjectByType<ArenaBossController>();
                pawn=NetworkManager.Singleton.LocalClient?.PlayerObject?.GetComponent<ArenaPlayerNetwork>();
                arena=UnityEngine.Object.FindFirstObjectByType<ArenaBootstrap>();
                if(boss==null||pawn==null||arena==null||arena.BossMaxHealth<=0)return;
                Require(UnityEngine.EventSystems.EventSystem.current!=null,"Practice scene has no UI input system.");
                var items=pawn.GetComponent<ItemManager>();Require(items.itemCount==3&&items.Owns(1301)&&items.Owns(1302),"Solo practice lacks a viable dual-element build.");
                pawn.SetRunFirepowerServer(40);
                UnityEngine.InputSystem.InputSystem.settings.backgroundBehavior=UnityEngine.InputSystem.InputSettings.BackgroundBehavior.IgnoreFocus;
                UnityEngine.InputSystem.InputSystem.settings.editorInputBehaviorInPlayMode=UnityEngine.InputSystem.InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
                var keyboard=UnityEngine.InputSystem.InputSystem.AddDevice<UnityEngine.InputSystem.Keyboard>("BossFocusKeyboard");
                focusKeyboard=keyboard;keyboard.MakeCurrent();
                var input=pawn.GetComponent<UnityEngine.InputSystem.PlayerInput>();
                UnityEngine.InputSystem.Users.InputUser.PerformPairingWithDevice(keyboard,input.user);
                input.actions.devices=new UnityEngine.InputSystem.InputDevice[]{keyboard};
                UnityEngine.InputSystem.InputSystem.QueueStateEvent(keyboard,new UnityEngine.InputSystem.LowLevel.KeyboardState(UnityEngine.InputSystem.Key.LeftShift));
                step=Time.timeAsDouble;phase=2;return;
            }
            double elapsed=Time.timeAsDouble-step;
            if(phase==2&&elapsed>5)
            {
                Require(pawn.IsFocused,"Focus keyboard did not reach the player motor.");
                Require(ArenaBossProjectile.LiveCount>20,"Card 1 emitted no meaningful ring pattern.");
                float hp=pawn.CurrentHealth;
                pawn.ApplyArenaDamage(new ArenaDamageInfo(1,2,0,pawn.transform.position,Vector3.forward));
                pawn.ApplyArenaDamage(new ArenaDamageInfo(1,2,0,pawn.transform.position,Vector3.forward));
                Require(Mathf.Abs(pawn.CurrentHealth-(hp-1))<.01f,"Hit invulnerability failed or projectile damage is disconnected.");
                pawn.GrantShieldServer(100,80);
                RunningPresentationCapture.Capture("boss-card-1");
                boss.ApplyArenaDamage(new ArenaDamageInfo(145,1,0,boss.transform.position,Vector3.forward));
                step=Time.timeAsDouble;phase=3;return;
            }
            if(phase==3&&elapsed>5)
            {
                Require(boss.Stage==2&&ArenaBossProjectile.LiveCount>10,"Card 2 did not activate or fire.");
                RunningPresentationCapture.Capture("boss-card-2");
                boss.ApplyArenaDamage(new ArenaDamageInfo(160,1,0,boss.transform.position,Vector3.forward));
                step=Time.timeAsDouble;phase=4;return;
            }
            if(phase==4&&elapsed>5)
            {
                Require(boss.Stage==3&&ArenaBossProjectile.LiveCount>20,"Final card did not activate or fire.");
                RunningPresentationCapture.Capture("boss-card-3");
                phase=5;step=Time.timeAsDouble;return;
            }
            if(phase==5&&elapsed>.55)
            {
                Require(pawn.FireServerAt(boss.transform.position,.01f),"Dual-element projectile was rejected.");
                reactionHits++;
                step=Time.timeAsDouble;
                if(boss.Elements.ExposedUntil>Time.time)
                {
                    Require(boss.Elements.ExposedUntil>Time.time,"Elemental break has no output window.");
                    RunningPresentationCapture.Capture("boss-break");phase=6;
                }
                Require(reactionHits<16,"Actual solo dual-element projectiles never broke the armor.");return;
            }
            if(phase==6&&elapsed>2.5)
            {
                Require(ArenaBossProjectile.LiveCount>0,"Boss failed to resume after a break.");
                pawn.GetComponent<ItemManager>().RequestActivateActiveItem(0);phase=7;step=Time.timeAsDouble;return;
            }
            if(phase==7&&elapsed>.1)
            {
                Require(ArenaBossProjectile.LiveCount==0,"Active bomb did not clear the bullets.");
                Require(pawn.GetComponent<ItemManager>().GetActiveCooldown(0)>20,"Active cooldown was not started.");
                Require(pawn.FireServerAt(boss.transform.position,1000),"Boss damage firing path rejected a live player.");
                phase=8;step=Time.timeAsDouble;return;
            }
            if(phase==8&&arena.Result==ArenaMatchResult.Victory)
            { phase=10;step=Time.timeAsDouble;return; }
            if(phase==10&&elapsed>.1)
            {
                RunningPresentationCapture.Capture("boss-victory");
                var retry=GameObject.Find("Retry").GetComponent<UnityEngine.UI.Button>();
                retry.onClick.Invoke();phase=9;return;
            }
            if(phase==9)
            {
                var next=UnityEngine.Object.FindFirstObjectByType<ArenaBossController>();
                if(next==null||next==boss||!next.IsArenaAlive)return;
                var player=NetworkManager.Singleton.LocalClient?.PlayerObject?.GetComponent<ArenaPlayerNetwork>();
                Require(player!=null&&player.GetComponent<ItemManager>().itemCount==3&&player.Firepower==40,"Retry lost the build or gate firepower.");
                Debug.Log("RUNNING_BOSS_ENCOUNTER_PASS solo-entry cards=3 hit+invulnerability reaction-break active-clear victory retry icons");Finish(0);
            }
        }
        catch(Exception e){Debug.LogError("RUNNING_BOSS_ENCOUNTER_FAIL "+e);Finish(1);}
    }
    private static void Require(bool condition,string message){if(!condition)throw new InvalidOperationException(message);}
    private static void Finish(int code)
    {
        EditorApplication.update-=Tick;
        NetworkManager.Singleton?.Shutdown();EditorApplication.isPlaying=false;
        EditorSettings.enterPlayModeOptionsEnabled=previousOptions;EditorSettings.enterPlayModeOptions=previousFlags;
        EditorApplication.delayCall+=()=>EditorApplication.Exit(code);
    }
}
#endif
