#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using Running.Game.Run.Generation;
using Running.Game.RunMap;
using Unity.Netcode;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class RunningProjectValidator
{
    [MenuItem("Running/Validate Playable Loop")]
    public static void ValidateFromMenu()
    {
        ValidateOrThrow();
        Debug.Log("Running playable-loop validation passed.");
    }

    public static void ValidateCommandLine()
    {
        ValidateOrThrow();
        Debug.Log("RUNNING_VALIDATION_OK");
    }

    private static void ValidateOrThrow()
    {
        ValidateBuildScenes();
        ValidateBootstrap();
        ValidateRunLoop();
        ValidateRunMapDomain();
        ValidateArenaPrefab();
    }

    private static void ValidateBuildScenes()
    {
        EditorBuildSettingsScene[] scenes = EditorBuildSettings.scenes;
        Require(scenes.Length >= 3, "Build Settings must contain Bootstrap, RunLoop and Arena.");
        Require(scenes[0].enabled && scenes[0].path == "Assets/Scenes/Bootstrap.unity",
            "Bootstrap must be the first enabled build scene.");
        Require(Array.Exists(scenes, scene => scene.enabled && scene.path == "Assets/Scenes/RunLoop.unity"),
            "RunLoop must be enabled in Build Settings.");
        Require(Array.Exists(scenes, scene => scene.enabled && scene.path == "Assets/Scenes/Arena.unity"),
            "Arena must be enabled in Build Settings.");
    }

    private static void ValidateBootstrap()
    {
        EditorSceneManager.OpenScene("Assets/Scenes/Bootstrap.unity", OpenSceneMode.Single);
        Require(UnityEngine.Object.FindFirstObjectByType<NetworkManager>() != null,
            "Bootstrap is missing NetworkManager.");
        RoomSession room = UnityEngine.Object.FindFirstObjectByType<RoomSession>();
        Require(room != null, "Bootstrap is missing RoomSession.");

        SerializedObject serialized = new SerializedObject(room);
        Require(serialized.FindProperty("runningScene").stringValue == "RunLoop",
            "RoomSession.runningScene must be RunLoop.");
        Require(serialized.FindProperty("arenaScene").stringValue == "Arena",
            "RoomSession.arenaScene must be Arena.");
    }

    private static void ValidateRunLoop()
    {
        EditorSceneManager.OpenScene("Assets/Scenes/RunLoop.unity", OpenSceneMode.Single);
        GameManager manager = UnityEngine.Object.FindFirstObjectByType<GameManager>();
        SectionManager sections = UnityEngine.Object.FindFirstObjectByType<SectionManager>();
        Require(manager != null, "RunLoop is missing GameManager.");
        Require(sections != null, "RunLoop is missing SectionManager.");
        Require(manager.GetComponent<NetworkObject>() != null,
            "GameManager root must have NetworkObject.");

        GameObject playerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(
            "Assets/Prefabs/Player.prefab");
        Require(playerPrefab != null, "Runner Player prefab is missing.");
        CrowdManager playerCrowd = playerPrefab.GetComponent<CrowdManager>();
        Require(playerCrowd != null, "Runner Player prefab is missing CrowdManager.");
        SerializedProperty startingPopulation =
            new SerializedObject(playerCrowd).FindProperty("startingPopulation");
        Require(startingPopulation != null && startingPopulation.intValue >= 10,
            "Runner starting population is too low for a first-node combat encounter.");

        SerializedObject sectionData = new SerializedObject(sections);
        SerializedProperty prefabs = sectionData.FindProperty("sectionPrefabs");
        var types = new HashSet<SectionType>();
        var hashes = new HashSet<uint>();
        for (int index = 0; index < prefabs.arraySize; index++)
        {
            LevelSection section = prefabs.GetArrayElementAtIndex(index).objectReferenceValue as LevelSection;
            if (section == null)
                continue;
            types.Add(section.Type);
            NetworkObject networkObject = section.GetComponent<NetworkObject>();
            Require(networkObject != null, $"Section {section.name} is missing NetworkObject.");
            SerializedProperty hashProperty =
                new SerializedObject(networkObject).FindProperty("GlobalObjectIdHash");
            uint hash = hashProperty != null
                ? unchecked((uint)hashProperty.longValue)
                : 0u;
            Require(hash != 0,
                $"Section {section.name} has an invalid NGO prefab hash of 0.");
            Require(hashes.Add(hash),
                $"Section {section.name} duplicates NGO prefab hash {hash}.");
        }

        Require(types.Contains(SectionType.Run), "RunLoop catalog is missing Run section.");
        Require(types.Contains(SectionType.Gate), "RunLoop catalog is missing Gate section.");
        Require(types.Contains(SectionType.Combat), "RunLoop catalog is missing Combat section.");

        Require(sections.TryCreatePlan(13579, 4, out SectionRunPlan plan),
            "SectionManager could not generate a four-lane deterministic plan.");
        Require(plan.LaneCount == 4, "Section plan did not preserve requested lane count.");
        foreach (LaneRunPlan lane in plan.Lanes)
        {
            var laneTypes = new HashSet<SectionType>();
            foreach (PlannedSection section in lane.Sections)
                laneTypes.Add(section.Type);
            Require(laneTypes.Contains(SectionType.Run) &&
                    laneTypes.Contains(SectionType.Gate) &&
                    laneTypes.Contains(SectionType.Combat),
                $"Lane {lane.LaneIndex} does not contain Run/Gate/Combat.");
        }

        var generatedSchedules = new HashSet<string>(StringComparer.Ordinal);
        for (int seed = 1; seed <= 32; seed++)
        {
            Require(sections.TryCreatePlan(seed, 1, out SectionRunPlan variedPlan),
                $"SectionManager could not generate seed {seed}.");
            LaneRunPlan lane = variedPlan.Lanes[0];
            var schedule = new List<string>(lane.Sections.Count);
            SectionType? previous = null;
            var present = new HashSet<SectionType>();
            foreach (PlannedSection planned in lane.Sections)
            {
                Require(!previous.HasValue || previous.Value != planned.Type,
                    $"Seed {seed} generated adjacent duplicate sections: {planned.Type}.");
                previous = planned.Type;
                present.Add(planned.Type);
                schedule.Add(planned.Type.ToString());
            }

            Require(present.Contains(SectionType.Run) &&
                    present.Contains(SectionType.Gate) &&
                    present.Contains(SectionType.Combat),
                $"Seed {seed} does not contain every core section type.");
            generatedSchedules.Add(string.Join(">", schedule));
        }

        Require(generatedSchedules.Count >= 8,
            "Road generation is still too repetitive across seeds.");

        SerializedObject managerData = new SerializedObject(manager);
        SerializedProperty upgrades = managerData.FindProperty("upgradePool");
        bool passive = false;
        bool active = false;
        bool temporary = false;
        int treasureQualityCount = 0;
        int levelRewardCount = 0;
        int eventTemporaryCount = 0;
        var upgradeIds = new HashSet<int>();
        for (int index = 0; index < upgrades.arraySize; index++)
        {
            UpgradeData item = upgrades.GetArrayElementAtIndex(index).objectReferenceValue as UpgradeData;
            if (item == null)
                continue;
            Require(upgradeIds.Add(item.NetworkId),
                $"Upgrade pool contains duplicate network ID {item.NetworkId}.");
            Require(item.CreateRuntime() != null,
                $"Upgrade {item.name} cannot create its gameplay runtime.");
            passive |= item.Category == ItemCategory.Passive;
            active |= item.Category == ItemCategory.Active;
            temporary |= item.Category == ItemCategory.Temporary;
            if (ItemAcquisitionPolicy.CanAcquire(item, ItemAcquisitionSource.LevelReward) &&
                item.Category != ItemCategory.Temporary)
            {
                levelRewardCount++;
            }
            if (ItemAcquisitionPolicy.CanAcquire(item, ItemAcquisitionSource.Event) &&
                item.Category == ItemCategory.Temporary)
            {
                eventTemporaryCount++;
            }
            if (item.Rarity >= ItemRarity.Epic &&
                item.IsSourceAllowed(ItemAcquisitionSource.TreasureChest))
            {
                Require(item.SupportsMode(GameplayMode.Runner) &&
                        item.SupportsMode(GameplayMode.BossArena),
                    $"Treasure item {item.name} must work in Running and BossArena.");
                treasureQualityCount++;
            }
        }
        Require(passive && active && temporary,
            "Upgrade pool must include Passive, Active and Temporary items.");
        Require(treasureQualityCount >= 3,
            "Treasure rewards require at least three Epic/Legendary candidates.");
        Require(levelRewardCount >= 3,
            "Level rewards require at least three permanent candidates.");
        Require(eventTemporaryCount >= 3,
            "Event rewards require at least three Temporary candidates.");

        var operations = new HashSet<GateOperation>();
        for (int seed = 0; seed < 256; seed++)
        {
            GateOfferGenerator.GeneratePair(seed, 2.5f, out GateOffer left, out GateOffer right);
            operations.Add(left.Operation);
            operations.Add(right.Operation);
        }
        Require(operations.Count == 4, "Gate generator did not cover +, -, multiply and divide.");
    }

    private static void ValidateRunMapDomain()
    {
        var generator = new RunMapGenerator();
        var settings = new RunMapGenerationSettings();
        for (int seed = 1; seed <= 64; seed++)
        {
            RunMapSnapshot map = generator.Generate(seed, settings);
            Require(map.TryValidate(out string error), $"Invalid run map for seed {seed}: {error}");
            Require(map.Nodes[map.Nodes.Count - 1].Type == RunMapNodeType.Boss,
                $"Seed {seed} does not end in Boss.");

            var types = new HashSet<RunMapNodeType>();
            foreach (RunMapNodeSnapshot node in map.Nodes)
                types.Add(node.Type);
            foreach (RunMapNodeType type in Enum.GetValues(typeof(RunMapNodeType)))
                Require(types.Contains(type), $"Seed {seed} is missing map node type {type}.");
        }
    }

    private static void ValidateArenaPrefab()
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
            "Assets/Prefabs/Player_Arena.prefab");
        Require(prefab != null, "Player_Arena prefab is missing.");
        Require(prefab.GetComponent<ArenaPlayerNetwork>() != null,
            "Player_Arena must contain ArenaPlayerNetwork.");
        Require(prefab.GetComponent<ItemManager>() != null,
            "Player_Arena must contain ItemManager for build restoration.");
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }
}
#endif
