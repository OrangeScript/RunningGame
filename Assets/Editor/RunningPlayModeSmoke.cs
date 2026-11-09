#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using Running.Game.RunMap;
using Unity.Netcode;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public static class RunningPlayModeSmoke
{
    private enum Stage
    {
        WaitingForBootstrap,
        WaitingForRunLoop,
        WaitingForVote,
        WaitingForNode,
        WaitingForSections,
        WaitingForEnemies,
        WaitingForAutomaticDamage,
        WaitingForCombatClear,
        WaitingForReward,
        WaitingForNextVote
    }

    private static Stage stage;
    private static double startedAt;
    private static double stageStartedAt;
    private static int selectedNodeId;
    private static RunnerController runner;
    private static CombatEncounter encounter;
    private static ItemManager itemManager;
    private static int enemyCountBeforeAutomaticFire;
    private static float enemyHealthBeforeAutomaticFire;
    private static bool automaticDamageObserved;
    private static bool previousEnterPlayModeOptionsEnabled;
    private static EnterPlayModeOptions previousEnterPlayModeOptions;

    public static void Run()
    {
        try
        {
            RunningProjectValidator.ValidateCommandLine();
            EditorSceneManager.OpenScene("Assets/Scenes/Bootstrap.unity", OpenSceneMode.Single);

            previousEnterPlayModeOptionsEnabled = EditorSettings.enterPlayModeOptionsEnabled;
            previousEnterPlayModeOptions = EditorSettings.enterPlayModeOptions;
            EditorSettings.enterPlayModeOptionsEnabled = true;
            EditorSettings.enterPlayModeOptions = EnterPlayModeOptions.DisableDomainReload;

            stage = Stage.WaitingForBootstrap;
            startedAt = EditorApplication.timeSinceStartup;
            stageStartedAt = startedAt;
            EditorApplication.update -= Tick;
            EditorApplication.update += Tick;
            EditorApplication.isPlaying = true;
        }
        catch (Exception exception)
        {
            Fail(exception);
        }
    }

    private static void Tick()
    {
        try
        {
            if (EditorApplication.timeSinceStartup - startedAt > 75d)
                throw new TimeoutException($"Smoke test timed out at stage {stage}.");
            if (!EditorApplication.isPlaying)
                return;

            switch (stage)
            {
                case Stage.WaitingForBootstrap:
                    StartSoloWhenReady();
                    break;
                case Stage.WaitingForRunLoop:
                    WaitForRunLoop();
                    break;
                case Stage.WaitingForVote:
                    SubmitRoadVote();
                    break;
                case Stage.WaitingForNode:
                    WaitForSelectedNode();
                    break;
                case Stage.WaitingForSections:
                    VerifySectionsAndStartCombat();
                    break;
                case Stage.WaitingForEnemies:
                    ObserveSpawnedEnemies();
                    break;
                case Stage.WaitingForAutomaticDamage:
                    VerifyAutomaticCombatDamage();
                    break;
                case Stage.WaitingForCombatClear:
                    FinishLane();
                    break;
                case Stage.WaitingForReward:
                    SelectReward();
                    break;
                case Stage.WaitingForNextVote:
                    VerifyNextVoteAndFinish();
                    break;
            }
        }
        catch (Exception exception)
        {
            Fail(exception);
        }
    }

    private static void StartSoloWhenReady()
    {
        if (Time.frameCount < 2)
            return;
        RoomSession room = UnityEngine.Object.FindFirstObjectByType<RoomSession>();
        if (room == null)
            return;

        RunningPresentationCapture.Capture("menu");
        room.StartSoloRunning();
        Advance(Stage.WaitingForRunLoop);
    }

    private static void WaitForRunLoop()
    {
        if (SceneManager.GetActiveScene().name != "RunLoop")
            return;
        if (GameManager.instance == null || NetworkManager.Singleton == null ||
            !NetworkManager.Singleton.IsHost)
            return;

        runner = UnityEngine.Object.FindFirstObjectByType<RunnerController>();
        itemManager = runner != null ? runner.GetComponent<ItemManager>() : null;
        Require(runner != null && runner.IsSpawned, "Solo Runner was not spawned.");
        Require(itemManager != null, "Runner is missing ItemManager.");
        Advance(Stage.WaitingForVote);
    }

    private static void SubmitRoadVote()
    {
        RunMapNetworkCoordinator coordinator =
            UnityEngine.Object.FindFirstObjectByType<RunMapNetworkCoordinator>();
        if (coordinator == null || coordinator.MapSnapshot == null ||
            coordinator.VoteSnapshot == null || !coordinator.VoteSnapshot.IsActive)
            return;

        RunMapRuntimeUI mapUI = UnityEngine.Object.FindFirstObjectByType<RunMapRuntimeUI>();
        Require(mapUI != null && mapUI.IsVisible, "The route map must be visible during voting.");
        Require(mapUI.RouteScroll != null && mapUI.RouteScroll.vertical &&
                !mapUI.RouteScroll.horizontal && mapUI.RouteScroll.content.rect.height > mapUI.RouteScroll.viewport.rect.height,
            "Route map must support scrolling through a tall vertical route.");
        RunningPresentationCapture.Capture("route-map");
        Vector2 originalScroll = mapUI.RouteScroll.content.anchoredPosition;
        mapUI.RouteScroll.verticalNormalizedPosition = 1;
        Canvas.ForceUpdateCanvases();
        Require(Vector2.Distance(originalScroll,mapUI.RouteScroll.content.anchoredPosition) > 100,
            "Scrolling the map did not move its route content.");
        RunningPresentationCapture.Capture("route-top");
        mapUI.RouteScroll.verticalNormalizedPosition = 0;

        selectedNodeId = FindRoadCandidate(coordinator);
        Require(selectedNodeId >= 0, "First map layer contains no Combat/Elite node.");
        GameObject routeNode = GameObject.Find($"Node_{selectedNodeId}");
        Button routeButton = routeNode != null ? routeNode.GetComponent<Button>() : null;
        Require(routeButton != null && routeButton.interactable,
            "The reachable route did not expose an interactive map button.");
        routeButton.onClick.Invoke();
        Advance(Stage.WaitingForNode);
    }

    private static int FindRoadCandidate(RunMapNetworkCoordinator coordinator)
    {
        for (int seed = 1; seed <= 256; seed++)
        {
            int eliteFallback = -1;
            foreach (int nodeId in coordinator.ReachableNodeIds)
            {
                coordinator.MapSnapshot.TryGetNode(nodeId, out RunMapNodeSnapshot node);
                if (node.Type == RunMapNodeType.Combat)
                    return nodeId;
                if (node.Type == RunMapNodeType.Elite && eliteFallback < 0)
                    eliteFallback = nodeId;
            }

            if (seed == 256)
                return eliteFallback;

            coordinator.InitializeRun(seed);
            coordinator.BeginVoteForReachableNodes(20f);
        }

        return -1;
    }

    private static void WaitForSelectedNode()
    {
        RunMapNetworkCoordinator coordinator =
            UnityEngine.Object.FindFirstObjectByType<RunMapNetworkCoordinator>();
        if (coordinator == null || coordinator.CurrentNodeId != selectedNodeId)
            return;
        Advance(Stage.WaitingForSections);
    }

    private static void VerifySectionsAndStartCombat()
    {
        SectionManager manager = UnityEngine.Object.FindFirstObjectByType<SectionManager>();
        if (manager == null || manager.CurrentPlan == null || manager.SpawnedSections.Count == 0)
            return;

        var types = new HashSet<SectionType>();
        RunMapRuntimeUI mapUI = UnityEngine.Object.FindFirstObjectByType<RunMapRuntimeUI>();
        Require(mapUI != null && !mapUI.IsVisible,
            "Resolved map remained visible after the level started.");
        Require(runner.GetComponent<CrowdVisualizer>().VisualUnitCount == 1,
            "Running must show exactly one avatar.");
        Require(runner.transform.Find("CrowdRoot") == null || !runner.transform.Find("CrowdRoot").gameObject.activeSelf,
            "Legacy crowd formation is still visible.");
        Require(runner.MovementHalfWidth >= 7f,
            "Runner movement did not adopt the wide road bounds.");
        CrowdManager power = runner.GetComponent<CrowdManager>();
        PlayerStats stats = runner.GetComponent<PlayerStats>();
        int oldPower = power.Firepower;
        int oldHealth = power.Health;
        AttackPlan before = stats.CreateAttackPlan(oldPower, null);
        power.AddPopulation(10);
        AttackPlan after = stats.CreateAttackPlan(power.Firepower, null);
        Require(after.damageMultiplier > before.damageMultiplier && after.projectileCount == 1 && power.Health == oldHealth,
            "Gate power must increase one avatar's weapon damage without changing health or spawning more shooters.");
        power.SetPopulation(oldPower);
        foreach (LevelSection section in manager.SpawnedSections)
            types.Add(section.Type);
        Require(types.Contains(SectionType.Run) && types.Contains(SectionType.Gate) &&
                types.Contains(SectionType.Combat),
            "Runtime road is missing Run/Gate/Combat.");

        Transform[] transforms = UnityEngine.Object.FindObjectsByType<Transform>(
            FindObjectsInactive.Exclude,
            FindObjectsSortMode.None);
        Require(Array.Exists(transforms, transform => transform.name == "__ProceduralObstacles"),
            "Procedural obstacle field was not created.");
        foreach (LevelSection section in manager.SpawnedSections)
        {
            Gate[] gates = section.GetComponentsInChildren<Gate>();
            if (gates.Length >= 2)
            {
                Require(Mathf.Abs(gates[0].transform.position.z - gates[1].transform.position.z) < .1f &&
                    Mathf.Abs(gates[0].transform.position.x - gates[1].transform.position.x) > 7f,
                    "Gate choices must be side by side across the widened road.");
                Collider playerCollider = runner.GetComponent<CharacterController>();
                int healthBeforeGate = power.Health;
                gates[0].SendMessage("OnTriggerEnter",playerCollider);
                int powerAfterFirst = power.Firepower;
                gates[1].SendMessage("OnTriggerEnter",playerCollider);
                Require(power.Firepower == powerAfterFirst && power.Health == healthBeforeGate,
                    "The same gate pair applied twice, or a gate damaged health.");
                power.SetPopulation(oldPower);
            }
        }
        RunningPresentationCapture.Capture("running");

        encounter = UnityEngine.Object.FindFirstObjectByType<CombatEncounter>();
        Require(encounter != null, "Spawned road contains no CombatEncounter.");
        runner.TeleportToLaneServer(
            runner.LaneId,
            encounter.transform.position - encounter.transform.forward * 2f,
            4f);
        encounter.BeginEncounter(runner);
        Advance(Stage.WaitingForEnemies);
    }

    private static void ObserveSpawnedEnemies()
    {
        Enemy[] enemies = UnityEngine.Object.FindObjectsByType<Enemy>(
            FindObjectsInactive.Exclude,
            FindObjectsSortMode.None);
        if (enemies.Length == 0)
        {
            Require(EditorApplication.timeSinceStartup - stageStartedAt < 8d,
                "CombatEncounter did not spawn enemies.");
            return;
        }

        Require(runner.Phase == PlayerRunPhase.Combat,
            "Runner did not enter independent Combat phase.");
        enemyCountBeforeAutomaticFire = enemies.Length;
        enemyHealthBeforeAutomaticFire = SumEnemyHealth(enemies);
        automaticDamageObserved = false;
        Require(enemyHealthBeforeAutomaticFire > 0f,
            "Spawned enemies have no health to test against.");
        Advance(Stage.WaitingForAutomaticDamage);
    }

    private static void VerifyAutomaticCombatDamage()
    {
        Enemy[] enemies = UnityEngine.Object.FindObjectsByType<Enemy>(
            FindObjectsInactive.Exclude,
            FindObjectsSortMode.None);
        float currentHealth = SumEnemyHealth(enemies);
        automaticDamageObserved |= enemies.Length < enemyCountBeforeAutomaticFire ||
                                   currentHealth < enemyHealthBeforeAutomaticFire - 0.01f;

        if (enemies.Length == 0)
        {
            Require(automaticDamageObserved,
                "Combat cleared without observing authoritative AutoCombat damage.");
            Require(runner.Phase != PlayerRunPhase.Eliminated &&
                    GameManager.instance.state != RunState.GameOver,
                "Enemies were cleared, but the runner was eliminated during the combat trade.");
            Debug.Log("RUNNING_AUTOCOMBAT_CLEAR_OK");
            Advance(Stage.WaitingForCombatClear);
            return;
        }

        if (!automaticDamageObserved)
        {
            Require(runner.Phase != PlayerRunPhase.Eliminated,
                "Runner was eliminated before AutoCombat dealt any damage.");
            Require(EditorApplication.timeSinceStartup - stageStartedAt < 8d,
                "AutoCombat did not damage a spawned enemy.");
            return;
        }

        Require(runner.Phase != PlayerRunPhase.Eliminated,
            "Runner was eliminated before naturally clearing the combat encounter.");
        Require(EditorApplication.timeSinceStartup - stageStartedAt < 20d,
            "AutoCombat damaged enemies but could not naturally clear the encounter.");
    }

    private static float SumEnemyHealth(Enemy[] enemies)
    {
        float total = 0f;
        for (int index = 0; index < enemies.Length; index++)
        {
            if (enemies[index] != null && !enemies[index].IsDead)
                total += Mathf.Max(0f, enemies[index].CurrentHealth);
        }

        return total;
    }

    private static void FinishLane()
    {
        if (runner.Phase != PlayerRunPhase.Running)
            return;
        GameManager.instance.NotifyLaneFinished(runner, runner.LaneId);
        Advance(Stage.WaitingForReward);
    }

    private static void SelectReward()
    {
        if (GameManager.instance.state != RunState.UpgradeSelection)
            return;
        RunningPresentationCapture.Capture("reward");

        UpgradeCardUI[] cards = UnityEngine.Object.FindObjectsByType<UpgradeCardUI>(
            FindObjectsInactive.Exclude,
            FindObjectsSortMode.None);
        foreach (UpgradeCardUI card in cards)
        {
            Button button = card.GetComponentInChildren<Button>(true);
            if (button == null || !card.gameObject.activeInHierarchy)
                continue;
            button.onClick.Invoke();
            Advance(Stage.WaitingForNextVote);
            return;
        }

        Require(EditorApplication.timeSinceStartup - stageStartedAt < 8d,
            "Reward selection did not show an active card.");
    }

    private static void VerifyNextVoteAndFinish()
    {
        RunMapNetworkCoordinator coordinator =
            UnityEngine.Object.FindFirstObjectByType<RunMapNetworkCoordinator>();
        if (coordinator == null || coordinator.VoteSnapshot == null ||
            !coordinator.VoteSnapshot.IsActive)
            return;

        Require(GameManager.instance.state == RunState.MapSelection,
            "Game did not return to MapSelection after the reward.");
        Require(itemManager.itemCount > 0, "Selected reward was not added to ItemManager.");
        Succeed();
    }

    private static void Advance(Stage next)
    {
        stage = next;
        stageStartedAt = EditorApplication.timeSinceStartup;
    }

    private static void Succeed()
    {
        Debug.Log("RUNNING_PLAYMODE_SMOKE_OK");
        CleanupEditorSettings();
        EditorApplication.update -= Tick;
        EditorApplication.Exit(0);
    }

    private static void Fail(Exception exception)
    {
        Debug.LogException(exception);
        CleanupEditorSettings();
        EditorApplication.update -= Tick;
        EditorApplication.Exit(1);
    }

    private static void CleanupEditorSettings()
    {
        EditorSettings.enterPlayModeOptionsEnabled = previousEnterPlayModeOptionsEnabled;
        EditorSettings.enterPlayModeOptions = previousEnterPlayModeOptions;
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }
}
#endif
