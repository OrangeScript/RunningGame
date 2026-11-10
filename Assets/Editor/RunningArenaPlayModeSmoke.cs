#if UNITY_EDITOR
using System;
using System.Globalization;
using Unity.Netcode;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Batch-mode, cross-scene Play Mode smoke for Bootstrap -> RunLoop -> Arena.
/// Run with -executeMethod RunningArenaPlayModeSmoke.Begin.
/// </summary>
[InitializeOnLoad]
public static class RunningArenaPlayModeSmoke
{
    private const string Prefix = "[RunningArenaSmoke]";
    private const string ActiveKey = "RunningArenaSmoke.Active";
    private const string PhaseKey = "RunningArenaSmoke.Phase";
    private const string StartTicksKey = "RunningArenaSmoke.StartTicks";
    private const string ExitPendingKey = "RunningArenaSmoke.ExitPending";
    private const string ExitCodeKey = "RunningArenaSmoke.ExitCode";
    private const double TimeoutSeconds = 150d;

    private static float bossHealthBeforeShot;
    private static float bossHealthBeforeActiveItem;
    private static int temporaryItemId;
    private static float temporaryRemainingBeforeTransition;
    private static bool matchEndedEventReceived;
    private static bool subscribedToMatchEvent;

    static RunningArenaPlayModeSmoke()
    {
        EditorApplication.update -= Tick;
        EditorApplication.update += Tick;
    }

    public static void Begin()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("Arena smoke must start from Edit Mode.");

        SessionState.SetBool(ActiveKey, true);
        SessionState.SetInt(PhaseKey, 0);
        SessionState.SetString(
            StartTicksKey,
            DateTime.UtcNow.Ticks.ToString(CultureInfo.InvariantCulture));
        SessionState.SetBool(ExitPendingKey, false);
        SessionState.SetInt(ExitCodeKey, 1);

        string bootstrapPath = "Assets/Scenes/Bootstrap.unity";
        EditorSceneManager.OpenScene(bootstrapPath, OpenSceneMode.Single);
        Debug.Log($"{Prefix} BEGIN scene={bootstrapPath}");
        EditorApplication.isPlaying = true;
    }

    private static void Tick()
    {
        if (SessionState.GetBool(ExitPendingKey, false) && !EditorApplication.isPlayingOrWillChangePlaymode)
        {
            int exitCode = SessionState.GetInt(ExitCodeKey, 1);
            SessionState.SetBool(ExitPendingKey, false);
            if (Application.isBatchMode)
                EditorApplication.Exit(exitCode);
            return;
        }

        if (!SessionState.GetBool(ActiveKey, false))
            return;

        try
        {
            if (ElapsedSeconds() > TimeoutSeconds)
            {
                Fail($"Timed out in phase {SessionState.GetInt(PhaseKey, -1)}; activeScene={SceneManager.GetActiveScene().name}");
                return;
            }

            if (!EditorApplication.isPlaying)
                return;

            switch (SessionState.GetInt(PhaseKey, 0))
            {
                case 0:
                    StartSoloRun();
                    break;
                case 1:
                    TransitionToArena();
                    break;
                case 2:
                    ValidateArenaAndFire();
                    break;
                case 3:
                    ValidateBossDamageAndKill();
                    break;
                case 4:
                    ValidateCrossModeActiveItem();
                    break;
                case 5:
                    ValidateVictory();
                    break;
            }
        }
        catch (Exception exception)
        {
            Fail(exception.ToString());
        }
    }

    private static void StartSoloRun()
    {
        if (SceneManager.GetActiveScene().name != "Bootstrap")
            return;

        RoomSession room = UnityEngine.Object.FindFirstObjectByType<RoomSession>();
        if (room == null)
            return;

        Debug.Log($"{Prefix} Starting solo Host through RoomSession.");
        NetworkManager.Singleton.GetComponent<Unity.Netcode.Transports.UTP.UnityTransport>().SetConnectionData("127.0.0.1",7885,"0.0.0.0");
        room.StartSoloRunning();
        SessionState.SetInt(PhaseKey, 1);
    }

    private static void TransitionToArena()
    {
        if (SceneManager.GetActiveScene().name != "RunLoop")
            return;

        NetworkManager manager = NetworkManager.Singleton;
        RoomSession room = UnityEngine.Object.FindFirstObjectByType<RoomSession>();
        if (manager == null || !manager.IsHost || GameManager.instance == null || room == null)
            return;

        NetworkObject runPlayer = manager.LocalClient != null ? manager.LocalClient.PlayerObject : null;
        if (runPlayer != null) runPlayer.GetComponent<CrowdManager>()?.SetPopulation(40);
        ItemManager runItems = runPlayer != null ? runPlayer.GetComponent<ItemManager>() : null;
        UpgradeData thunder = AssetDatabase.LoadAssetAtPath<UpgradeData>(
            "Assets/Scripts/Game/Effect Asset/Ugrade/Upgrade_ThunderStrike.asset");
        UpgradeData temporary = AssetDatabase.LoadAssetAtPath<UpgradeData>(
            "Assets/Scripts/Game/Effect Asset/Ugrade/Temp_Overclock.asset");
        if (runItems == null || thunder == null || temporary == null)
        {
            throw new InvalidOperationException("Could not resolve ItemManager, Thunder Strike, or temporary boost before Boss transition.");
        }

        bool hasThunder = false;
        for (int i = 0; i < runItems.SyncedItemCount; i++)
            hasThunder |= runItems.GetSyncedItemId(i) == thunder.NetworkId;
        if (!hasThunder && !runItems.TryAcquireServer(thunder, ItemAcquisitionSource.Debug))
            throw new InvalidOperationException("Could not grant Thunder Strike before Boss transition.");

        if (!runItems.TryAcquireServer(temporary, ItemAcquisitionSource.Debug))
            throw new InvalidOperationException("Could not grant temporary Overclock before Boss transition.");
        temporaryItemId = temporary.NetworkId;
        temporaryRemainingBeforeTransition = FindRemainingTime(
            runItems.CaptureBuildSnapshot(),
            temporaryItemId);
        if (temporaryRemainingBeforeTransition <= 0f)
            throw new InvalidOperationException("Temporary Overclock had no remaining time before transition.");

        if (!room.TransitionToBossArena())
            return;

        Debug.Log($"{Prefix} TransitionToBossArena accepted.");
        SessionState.SetInt(PhaseKey, 2);
    }

    private static void ValidateArenaAndFire()
    {
        if (SceneManager.GetActiveScene().name != "Arena")
            return;

        NetworkManager manager = NetworkManager.Singleton;
        ArenaBootstrap bootstrap = UnityEngine.Object.FindFirstObjectByType<ArenaBootstrap>();
        ArenaPlayerNetwork[] players = UnityEngine.Object.FindObjectsByType<ArenaPlayerNetwork>(
            FindObjectsInactive.Exclude,
            FindObjectsSortMode.None);

        if (manager == null || !manager.IsHost || bootstrap == null || bootstrap.BossMaxHealth <= 0f)
            return;

        ArenaPlayerNetwork validPlayer = null;
        int validPlayerCount = 0;
        for (int i = 0; i < players.Length; i++)
        {
            if (players[i].IsSpawned && players[i].IsParticipatingPawn)
            {
                validPlayer = players[i];
                validPlayerCount++;
            }
        }

        if (validPlayerCount != 1 || validPlayer == null)
            throw new InvalidOperationException($"Expected exactly one valid Arena player, found {validPlayerCount}.");
        if (validPlayer.Firepower != 40)
            throw new InvalidOperationException("Gate firepower did not survive the Running -> Arena transition.");
        if (Mathf.Abs(validPlayer.transform.position.x) > 16f || Mathf.Abs(validPlayer.transform.position.z) > 10f)
            throw new InvalidOperationException("Arena player spawned outside the safe arena interior.");

        if (GameObject.Find("ArenaBoss_Runtime") == null)
            throw new InvalidOperationException("Runtime Boss object was not created.");

        if (Camera.main == null || Camera.main.GetComponent<ArenaCameraRig>() == null)
            throw new InvalidOperationException("Top-down ArenaCameraRig was not installed on Main Camera.");
        if (!Camera.main.orthographic)
            throw new InvalidOperationException("Boss Arena must use the shared orthographic framing.");

        ItemManager items = validPlayer.GetComponent<ItemManager>();
        if (items == null || items.CurrentMode != GameplayMode.BossArena)
            throw new InvalidOperationException("Arena player ItemManager was not restored in BossArena mode.");
        if (validPlayer.GetComponent<ArenaActiveItemInput>() == null)
            throw new InvalidOperationException("Arena Q/E active-item input adapter was not installed.");

        bool hasThunder = false;
        for (int i = 0; i < items.SyncedItemCount; i++)
            hasThunder |= items.GetSyncedItemId(i) == 1008;
        if (!hasThunder)
            throw new InvalidOperationException("Thunder Strike did not survive Runner -> BossArena build restoration.");

        float restoredTemporaryTime = FindRemainingTime(
            items.CaptureBuildSnapshot(),
            temporaryItemId);
        if (restoredTemporaryTime <= 0f ||
            restoredTemporaryTime > temporaryRemainingBeforeTransition + 0.1f ||
            restoredTemporaryTime < temporaryRemainingBeforeTransition - 5f)
        {
            throw new InvalidOperationException(
                $"Temporary item duration was not preserved across the scene transition: " +
                $"before={temporaryRemainingBeforeTransition:F2}, after={restoredTemporaryTime:F2}.");
        }

        if (!subscribedToMatchEvent)
        {
            ArenaBootstrap.MatchEnded += HandleMatchEnded;
            subscribedToMatchEvent = true;
        }

        RunningPresentationCapture.Capture("arena");

        bossHealthBeforeShot = bootstrap.BossHealth;
        if (!validPlayer.FireServerAt(GameObject.Find("ArenaBoss_Runtime").transform.position, 30f))
            throw new InvalidOperationException("Server player refused the smoke-test shot.");

        Debug.Log($"{Prefix} Arena validated; firing authoritative shot. bossHealth={bossHealthBeforeShot:F1}");
        SessionState.SetInt(PhaseKey, 3);
    }

    private static void ValidateBossDamageAndKill()
    {
        ArenaBootstrap bootstrap = UnityEngine.Object.FindFirstObjectByType<ArenaBootstrap>();
        if (bootstrap == null || bootstrap.BossHealth >= bossHealthBeforeShot)
            return;

        ArenaPlayerNetwork player = FindParticipatingOwner();
        GameObject boss = GameObject.Find("ArenaBoss_Runtime");
        if (player == null || boss == null)
            throw new InvalidOperationException("Arena player or Boss disappeared after damage validation.");

        Debug.Log($"{Prefix} Authoritative projectile damaged Boss: {bossHealthBeforeShot:F1} -> {bootstrap.BossHealth:F1}");
        ItemManager items = player.GetComponent<ItemManager>();
        if (items == null)
            throw new InvalidOperationException("Arena ItemManager disappeared before active-item validation.");

        bossHealthBeforeActiveItem = bootstrap.BossHealth;
        items.RequestActivateActiveItem(0);
        SessionState.SetInt(PhaseKey, 4);
    }

    private static void ValidateCrossModeActiveItem()
    {
        ArenaBootstrap bootstrap = UnityEngine.Object.FindFirstObjectByType<ArenaBootstrap>();
        if (bootstrap == null || bootstrap.BossHealth >= bossHealthBeforeActiveItem)
            return;

        ArenaPlayerNetwork player = FindParticipatingOwner();
        GameObject boss = GameObject.Find("ArenaBoss_Runtime");
        if (player == null || boss == null)
            throw new InvalidOperationException("Arena player or Boss disappeared during active-item validation.");

        Debug.Log($"{Prefix} Q/E active slot path damaged Boss with restored Thunder Strike: {bossHealthBeforeActiveItem:F1} -> {bootstrap.BossHealth:F1}");
        if (!player.FireServerAt(boss.transform.position, 1000f))
            throw new InvalidOperationException("Server player refused the lethal smoke-test shot.");

        SessionState.SetInt(PhaseKey, 5);
    }

    private static void ValidateVictory()
    {
        ArenaBootstrap bootstrap = UnityEngine.Object.FindFirstObjectByType<ArenaBootstrap>();
        if (bootstrap == null || bootstrap.Result != ArenaMatchResult.Victory)
            return;
        if (!matchEndedEventReceived)
            throw new InvalidOperationException("Victory state replicated but ArenaBootstrap.MatchEnded was not raised.");

        Pass($"Arena loaded, one player spawned, Boss/HUD/camera present, BossArena items active, damage loop and Victory event verified. bossHealth={bootstrap.BossHealth:F1}");
    }

    private static ArenaPlayerNetwork FindParticipatingOwner()
    {
        ArenaPlayerNetwork[] players = UnityEngine.Object.FindObjectsByType<ArenaPlayerNetwork>(
            FindObjectsInactive.Exclude,
            FindObjectsSortMode.None);
        for (int i = 0; i < players.Length; i++)
        {
            if (players[i].IsSpawned && players[i].IsOwner && players[i].IsParticipatingPawn)
                return players[i];
        }

        return null;
    }

    private static float FindRemainingTime(PlayerItemSnapshot[] snapshots, int networkId)
    {
        for (int i = 0; i < snapshots.Length; i++)
        {
            if (snapshots[i].Definition != null &&
                snapshots[i].Definition.NetworkId == networkId)
            {
                return snapshots[i].RemainingTime;
            }
        }

        return -1f;
    }

    private static void HandleMatchEnded(ArenaMatchResult result)
    {
        if (result == ArenaMatchResult.Victory)
            matchEndedEventReceived = true;
    }

    private static double ElapsedSeconds()
    {
        string raw = SessionState.GetString(StartTicksKey, "0");
        if (!long.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out long ticks) || ticks <= 0)
            return 0d;

        return (DateTime.UtcNow - new DateTime(ticks, DateTimeKind.Utc)).TotalSeconds;
    }

    private static void Pass(string details)
    {
        Debug.Log($"{Prefix} RUNNING_ARENA_SMOKE_PASS {details}");
        Finish(0);
    }

    private static void Fail(string details)
    {
        Debug.LogError($"{Prefix} RUNNING_ARENA_SMOKE_FAIL {details}");
        Finish(1);
    }

    private static void Finish(int exitCode)
    {
        if (subscribedToMatchEvent)
        {
            ArenaBootstrap.MatchEnded -= HandleMatchEnded;
            subscribedToMatchEvent = false;
        }

        SessionState.SetBool(ActiveKey, false);
        SessionState.SetInt(ExitCodeKey, exitCode);
        SessionState.SetBool(ExitPendingKey, true);
        if (EditorApplication.isPlaying)
            EditorApplication.isPlaying = false;
    }
}
#endif
