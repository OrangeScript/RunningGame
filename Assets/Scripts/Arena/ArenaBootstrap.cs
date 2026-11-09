using System;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

/// <summary>
/// Zero-reference Arena installer. It creates presentation, bounds, Boss and
/// custom-message replication when the Arena scene is loaded.
/// </summary>
[DefaultExecutionOrder(-500)]
public sealed class ArenaBootstrap : MonoBehaviour
{
    private const string BulletMessage = "Running.Arena.BossBullet.v1";
    private const string StateMessage = "Running.Arena.BossState.v1";
    private static bool hooksInstalled;

    [SerializeField] private Vector2 arenaHalfExtents = new Vector2(18f, 12f);
    [SerializeField] private Vector3 bossPosition = new Vector3(0f, 1f, 7f);
    [SerializeField] private float bossBaseHealth = 220f;

    private NetworkManager networkManager;
    private ArenaBossController boss;
    private GameObject bossVisual;
    private bool messagesRegistered;
    private bool environmentCreated;
    private float nextPlayerScan;
    private bool loggedMissingPlayerNetwork;

    public static event Action<ArenaMatchResult> MatchEnded;

    public bool IsServerAuthority => networkManager != null && networkManager.IsServer;
    public float BossHealth { get; private set; }
    public float BossMaxHealth { get; private set; }
    public ArenaMatchResult Result { get; private set; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void InstallHooks()
    {
        if (!hooksInstalled)
        {
            hooksInstalled = true;
            SceneManager.sceneLoaded += HandleSceneLoaded;
        }

        TryInstall(SceneManager.GetActiveScene());
    }

    private static void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        TryInstall(scene);
    }

    private static void TryInstall(Scene scene)
    {
        if (!scene.IsValid() || scene.name.IndexOf("Arena", StringComparison.OrdinalIgnoreCase) < 0)
            return;
        if (FindFirstObjectByType<ArenaBootstrap>() != null)
            return;

        GameObject root = new GameObject("ArenaBootstrap_Runtime");
        SceneManager.MoveGameObjectToScene(root, scene);
        root.AddComponent<ArenaBootstrap>();
    }

    private void Awake()
    {
        networkManager = NetworkManager.Singleton;
        CreateEnvironment();
        gameObject.AddComponent<ArenaHUD>();
    }

    private void Update()
    {
        if (networkManager == null)
            networkManager = NetworkManager.Singleton;

        if (networkManager == null || !networkManager.IsListening)
            return;

        RegisterMessages();

        if (Time.unscaledTime >= nextPlayerScan)
        {
            nextPlayerScan = Time.unscaledTime + 0.5f;
            EnsurePlayerRuntimeComponents();
        }

        if (networkManager.IsServer && boss == null && HasSpawnedPlayer())
        {
            boss = bossVisual.GetComponent<ArenaBossController>();
            if (boss == null)
                boss = bossVisual.AddComponent<ArenaBossController>();
            boss.Initialize(this, bossBaseHealth);
        }
    }

    private void OnDestroy()
    {
        if (messagesRegistered && networkManager != null && networkManager.CustomMessagingManager != null)
        {
            networkManager.CustomMessagingManager.UnregisterNamedMessageHandler(BulletMessage);
            networkManager.CustomMessagingManager.UnregisterNamedMessageHandler(StateMessage);
        }
    }

    public void SpawnBossBullet(
        Vector3 origin,
        Vector3 direction,
        float speed,
        float lifetime,
        float damage,
        float radius)
    {
        if (!IsServerAuthority)
            return;

        ArenaBossProjectile.Spawn(origin, direction, speed, lifetime, damage, radius, true);

        using (FastBufferWriter writer = new FastBufferWriter(96, Allocator.Temp))
        {
            writer.WriteValueSafe(origin);
            writer.WriteValueSafe(direction);
            writer.WriteValueSafe(speed);
            writer.WriteValueSafe(lifetime);
            writer.WriteValueSafe(damage);
            writer.WriteValueSafe(radius);
            networkManager.CustomMessagingManager.SendNamedMessageToAll(
                BulletMessage,
                writer,
                NetworkDelivery.UnreliableSequenced);
        }
    }

    public void BroadcastBossState(float health, float maxHealth, ArenaMatchResult result)
    {
        if (!IsServerAuthority)
            return;

        ApplyBossState(health, maxHealth, result);
        using (FastBufferWriter writer = new FastBufferWriter(32, Allocator.Temp))
        {
            writer.WriteValueSafe(health);
            writer.WriteValueSafe(maxHealth);
            byte rawResult = (byte)result;
            writer.WriteValueSafe(rawResult);
            networkManager.CustomMessagingManager.SendNamedMessageToAll(
                StateMessage,
                writer,
                NetworkDelivery.ReliableSequenced);
        }
    }

    public void NotifyMatchEnded(ArenaMatchResult result)
    {
        Result = result;
        MatchEnded?.Invoke(result);
    }

    private void RegisterMessages()
    {
        if (messagesRegistered || networkManager.CustomMessagingManager == null)
            return;

        networkManager.CustomMessagingManager.RegisterNamedMessageHandler(BulletMessage, HandleBulletMessage);
        networkManager.CustomMessagingManager.RegisterNamedMessageHandler(StateMessage, HandleStateMessage);
        messagesRegistered = true;
    }

    private void HandleBulletMessage(ulong senderClientId, FastBufferReader reader)
    {
        if (networkManager == null || senderClientId != NetworkManager.ServerClientId || networkManager.IsServer)
            return;

        reader.ReadValueSafe(out Vector3 origin);
        reader.ReadValueSafe(out Vector3 direction);
        reader.ReadValueSafe(out float speed);
        reader.ReadValueSafe(out float lifetime);
        reader.ReadValueSafe(out float damage);
        reader.ReadValueSafe(out float radius);
        ArenaBossProjectile.Spawn(origin, direction, speed, lifetime, damage, radius, false);
    }

    private void HandleStateMessage(ulong senderClientId, FastBufferReader reader)
    {
        if (networkManager == null || senderClientId != NetworkManager.ServerClientId || networkManager.IsServer)
            return;

        reader.ReadValueSafe(out float health);
        reader.ReadValueSafe(out float maxHealth);
        reader.ReadValueSafe(out byte rawResult);
        ArenaMatchResult previous = Result;
        ApplyBossState(health, maxHealth, (ArenaMatchResult)rawResult);
        if (previous == ArenaMatchResult.None && Result != ArenaMatchResult.None)
            MatchEnded?.Invoke(Result);
    }

    private void ApplyBossState(float health, float maxHealth, ArenaMatchResult result)
    {
        BossHealth = Mathf.Max(0f, health);
        BossMaxHealth = Mathf.Max(1f, maxHealth);
        Result = result;

        if (bossVisual != null)
        {
            float ratio = Mathf.Clamp01(BossHealth / BossMaxHealth);
            bossVisual.transform.localScale = new Vector3(2.4f, 1.4f + ratio * 1.6f, 2.4f);
            Renderer renderer = bossVisual.GetComponent<Renderer>();
            if (renderer != null)
                renderer.material.color = result == ArenaMatchResult.Victory ? Color.gray : Color.Lerp(new Color(.62f,.35f,.29f),new Color(.38f,.34f,.40f),1f-ratio);
        }
    }

    private void CreateEnvironment()
    {
        if (environmentCreated)
            return;
        environmentCreated = true;

        RenderSettings.fog = false;
        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
        RenderSettings.ambientLight = new Color(.65f,.67f,.63f);
        foreach (Light light in FindObjectsByType<Light>(FindObjectsSortMode.None))
        {
            if (light.type != LightType.Directional) continue;
            light.shadows=LightShadows.Soft; light.shadowStrength=.35f; light.intensity=.9f;
        }
        foreach (var volume in FindObjectsByType<UnityEngine.Rendering.Volume>(FindObjectsSortMode.None)) volume.weight=0;
        foreach (var mesh in FindObjectsByType<MeshFilter>(FindObjectsSortMode.None))
        {
            if (mesh.name == "Cube") mesh.gameObject.SetActive(false);
            if (mesh.name == "Plane")
            {
                mesh.transform.localScale=new Vector3(3.6f,1,2.4f);
                RunSectionPresentation.Paint(mesh.GetComponent<Renderer>(),new Color(.68f,.72f,.68f));
            }
        }

        Camera camera = Camera.main;
        if (camera != null && camera.GetComponent<ArenaCameraRig>() == null)
            camera.gameObject.AddComponent<ArenaCameraRig>();

        CreateWall("ArenaWall_N", new Vector3(0f, 1f, arenaHalfExtents.y), new Vector3(arenaHalfExtents.x * 2f + 1f, 2f, 0.5f));
        CreateWall("ArenaWall_S", new Vector3(0f, 1f, -arenaHalfExtents.y), new Vector3(arenaHalfExtents.x * 2f + 1f, 2f, 0.5f));
        CreateWall("ArenaWall_E", new Vector3(arenaHalfExtents.x, 1f, 0f), new Vector3(0.5f, 2f, arenaHalfExtents.y * 2f + 1f));
        CreateWall("ArenaWall_W", new Vector3(-arenaHalfExtents.x, 1f, 0f), new Vector3(0.5f, 2f, arenaHalfExtents.y * 2f + 1f));

        bossVisual = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        bossVisual.name = "ArenaBoss_Runtime";
        bossVisual.transform.position = bossPosition;
        bossVisual.transform.localScale = new Vector3(2.4f, 2.8f, 2.4f);
        Renderer bossRenderer = bossVisual.GetComponent<Renderer>();
        if (bossRenderer != null)
            bossRenderer.material.color = new Color(.62f,.35f,.29f);
    }

    private static void CreateWall(string wallName, Vector3 position, Vector3 scale)
    {
        GameObject wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
        wall.name = wallName;
        wall.transform.position = position;
        wall.transform.localScale = scale;
        Renderer renderer = wall.GetComponent<Renderer>();
        if (renderer != null)
            renderer.material.color = new Color(.43f,.49f,.44f);
    }

    private void EnsurePlayerRuntimeComponents()
    {
        PlayerInput[] playerInputs = FindObjectsByType<PlayerInput>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        for (int i = 0; i < playerInputs.Length; i++)
        {
            GameObject player = playerInputs[i].gameObject;
            if (player.transform.Find("ArenaAvatar") == null)
            {
                foreach (Renderer legacy in player.GetComponentsInChildren<Renderer>()) legacy.enabled=false;
                var avatar=GameObject.CreatePrimitive(PrimitiveType.Capsule);
                avatar.name="ArenaAvatar";
                avatar.transform.SetParent(player.transform,false);
                avatar.transform.localPosition=Vector3.up;
                avatar.transform.localScale=new Vector3(.8f,1,.8f);
                avatar.GetComponent<Collider>().enabled=false;
                RunSectionPresentation.Paint(avatar.GetComponent<Renderer>(),MinimalUI.Accent);
            }
            ArenaInputReader input = GetOrAdd<ArenaInputReader>(player);
            ArenaMotor motor = GetOrAdd<ArenaMotor>(player);
            GetOrAdd<ArenaRemoteBuffer>(player);
            GetOrAdd<ArenaAnimatorDriver>(player);
            GetOrAdd<ArenaActiveItemInput>(player);
            motor.SetBounds(Vector2.zero, arenaHalfExtents);

            ArenaPlayerNetwork playerNetwork = player.GetComponent<ArenaPlayerNetwork>();
            if (playerNetwork != null)
            {
                playerNetwork.RefreshRuntimeSetup();
            }
            else if (!loggedMissingPlayerNetwork)
            {
                loggedMissingPlayerNetwork = true;
                Debug.LogError("Player_Arena prefab must contain ArenaPlayerNetwork. ArenaBootstrap supplied all non-network components, but NetworkBehaviour components must exist on the registered prefab before spawn.");
            }

            if (networkManager != null && player.GetComponent<NetworkObject>() is NetworkObject networkObject)
                input.SetOwnerInputEnabled(networkObject.IsOwner && playerNetwork != null && playerNetwork.IsArenaAlive);
        }
    }

    private static T GetOrAdd<T>(GameObject target) where T : Component
    {
        T component = target.GetComponent<T>();
        return component != null ? component : target.AddComponent<T>();
    }

    private static bool HasSpawnedPlayer()
    {
        ArenaPlayerNetwork[] players = FindObjectsByType<ArenaPlayerNetwork>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        for (int i = 0; i < players.Length; i++)
        {
            if (players[i].IsSpawned && players[i].NetworkObject != null && players[i].NetworkObject.IsPlayerObject)
                return true;
        }

        return false;
    }

}
