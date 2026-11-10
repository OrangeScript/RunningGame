using System.Collections;
using System.Collections.Generic;
using TMPro;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;


public enum GameMode
{
    Running,Arena
}
public class RoomSession : MonoBehaviour
{

    [Header("Network")]
    [SerializeField] private NetworkManager networkManager;

    [Header("Player Prefabs")]
    [SerializeField] private NetworkObject runnerPlayerPrefab;
    [SerializeField] private NetworkObject arenaPlayerPrefab;

    [Header("Scenes")]
    [SerializeField] private string runningScene = "RunGameplay";
    [SerializeField] private string arenaScene = "Arena_01";

    [Header("UI")]
    [SerializeField] private GameObject networkUIRoot;
    [SerializeField] private GameObject mainMenuPanel;
    [SerializeField] private GameObject lobbyPanel;
    [SerializeField] private TMP_Text statusText;
    [SerializeField] private TMP_Text playerCountText;
    [SerializeField] private TMP_Text modeText;
    [SerializeField] private Button startMatchButton;

    private enum RoomPhase
    {
        Menu,
        Lobby,
        Loading,
        Playing
    }

    private GameMode selectedMode = GameMode.Running;
    RoomPhase phase = RoomPhase.Menu;
    bool soloRunning;
    private readonly Dictionary<ulong, PlayerItemSnapshot[]> preservedBuilds =
        new Dictionary<ulong, PlayerItemSnapshot[]>();
    private readonly Dictionary<ulong, int> preservedFirepower = new Dictionary<ulong, int>();




    // Start is called before the first frame update
    void Awake()
    {
        if(networkManager == null) 
        networkManager = GetComponent<NetworkManager>();
        DontDestroyOnLoad(this);
        BuildMinimalMenu();
    }

    void Start()
    {
        networkManager.NetworkConfig.ConnectionApproval = true;

        networkManager.OnClientConnectedCallback +=
            HandleConnected;

        networkManager.OnClientDisconnectCallback +=
            HandleDisconnected;
        

        ShowMainMenu();
    }

    private void RegisterSceneEvents()
    {
        var sceneManager = networkManager.SceneManager;

        if (sceneManager == null)
        {
            Debug.LogError("SceneManager 未初始化");
            return;
        }

        sceneManager.OnLoadEventCompleted -= OnSceneLoaded;
        sceneManager.OnLoadEventCompleted += OnSceneLoaded;
    }


    void OnDestroy()
    {
        if (networkManager == null)
            return;

        networkManager.OnClientConnectedCallback -=
            HandleConnected;

        networkManager.OnClientDisconnectCallback -=
            HandleDisconnected;

        if (networkManager.SceneManager != null)
        {
            networkManager.SceneManager.OnLoadEventCompleted -=
                OnSceneLoaded;
        }
    }
    public void SelectArena()
    {
        
        if (phase != RoomPhase.Lobby)
            return;

        selectedMode = GameMode.Arena;
        UpdateModeText();
    }

    public void SelectRunning()
    {
        if (phase != RoomPhase.Lobby)
            return;

        selectedMode = GameMode.Running;
        UpdateModeText();
    }


    public void CreateRoom()
    {
        soloRunning = false;
        StartHostRoom();
    }

    public void StartSoloRunning()
    {
        if(phase != RoomPhase.Menu) return;
        selectedMode = GameMode.Running;
        soloRunning = true;
        if(StartHostRoom()) StartMatch();
    }

    public void StartSoloBoss()
    {
        if(phase != RoomPhase.Menu) return;
        selectedMode = GameMode.Arena;
        soloRunning = true;
        preservedBuilds.Clear(); preservedFirepower.Clear();
        if(StartHostRoom()) StartMatch();
    }

    bool StartHostRoom()
    {
        if(networkManager.IsListening) return false;
        preservedBuilds.Clear();preservedFirepower.Clear();
        phase = RoomPhase.Lobby;
        networkManager.ConnectionApprovalCallback = ApprovalCheck;
        if (!networkManager.StartHost())
        {
            phase = RoomPhase.Menu;
            ShowMainMenu();
            return false;
        }
        ShowLobby();UpdateLobbyUI();
        RegisterSceneEvents();
        return true;
    }

    public void JoinRoom()
    {
        if(networkManager.IsListening) return;
        soloRunning = false;
        phase = RoomPhase.Lobby;
        if (!networkManager.StartClient())
        {
            phase = RoomPhase.Menu;
            ShowMainMenu();
            return;
        }
        ShowLobby();
        SetStatus("正在连接...");
        RegisterSceneEvents();
    }

    public void LeaveRoom()
    {
        phase = RoomPhase.Menu;
        if (networkManager.IsListening)
            networkManager.Shutdown();
        ShowMainMenu();
    }

    private void ApprovalCheck(NetworkManager.ConnectionApprovalRequest request,
                                NetworkManager.ConnectionApprovalResponse response)
    {
        int maxPlayers = soloRunning ? 1 : 4;
        bool allowed = phase == RoomPhase.Lobby && networkManager.ConnectedClientsIds.Count < maxPlayers;
        response.Approved = allowed;

        response.CreatePlayerObject = false;
        response.Pending = false;
        if(!allowed) response.Reason = "房间已满";
    }

    public void StartMatch()
    {
        if(!networkManager.IsServer || phase != RoomPhase.Lobby) return;
        int count = networkManager.ConnectedClientsIds.Count;
        bool enoughPlayers = count >= (soloRunning ? 1 : 2);
        if (!enoughPlayers)
        {
            SetStatus("人数不足");
            return;
        }
        string sceneName = selectedMode == GameMode.Arena ? arenaScene : runningScene;
        phase = RoomPhase.Loading;
        SetStatus("正在加载地图");
        SceneEventProgressStatus result = networkManager.SceneManager.LoadScene(sceneName,LoadSceneMode.Single);

        if(result != SceneEventProgressStatus.Started)
        {
            phase = RoomPhase.Lobby;
            SetStatus("加载失败");
        }
    }

    public bool TransitionToBossArena()
    {
        if (!networkManager.IsServer || phase != RoomPhase.Playing)
            return false;

        CapturePlayerBuilds();
        selectedMode = GameMode.Arena;
        phase = RoomPhase.Loading;
        SceneEventProgressStatus result = networkManager.SceneManager.LoadScene(
            arenaScene,
            LoadSceneMode.Single);

        if (result == SceneEventProgressStatus.Started)
            return true;

        phase = RoomPhase.Playing;
        Debug.LogError($"Boss Arena load failed: {result}");
        return false;
    }

    public void RetryBossEncounter()
    {
        if(!networkManager.IsServer||phase!=RoomPhase.Playing||selectedMode!=GameMode.Arena)return;
        CapturePlayerBuilds();
        phase=RoomPhase.Loading;
        if(networkManager.SceneManager.LoadScene(arenaScene,LoadSceneMode.Single)!=SceneEventProgressStatus.Started)
            phase=RoomPhase.Playing;
    }

    public void RetryRunning()
    {
        if(!networkManager.IsServer||phase!=RoomPhase.Playing||selectedMode!=GameMode.Running)return;
        preservedBuilds.Clear();preservedFirepower.Clear();phase=RoomPhase.Loading;
        if(networkManager.SceneManager.LoadScene(runningScene,LoadSceneMode.Single)!=SceneEventProgressStatus.Started)
            phase=RoomPhase.Playing;
    }

    private void OnSceneLoaded(
        string sceneName,
        LoadSceneMode loadMode,
        List<ulong> clientsCompleted,
        List<ulong> clientsTimedout
    )
    {
        bool isArena = sceneName == arenaScene;
        bool isRunning = sceneName == runningScene;

        if (!isArena && !isRunning)
            return;

        if (clientsTimedout.Count > 0)
        {
            Debug.LogError("有客户端加载地图超时");
            return;
        }

        selectedMode = isArena
            ? GameMode.Arena
            : GameMode.Running;

        phase = RoomPhase.Playing;

        if (networkUIRoot != null)
            networkUIRoot.SetActive(false);

        if (!networkManager.IsServer)
            return;

        if (!SpawnPlayers())
        {
            Debug.LogError("玩家生成失败，无法开始游戏");
            return;
        }

        if (isArena)
            RestorePlayerBuildsForArena();

        if (isRunning)
        {
            GameManager runManager = GameManager.instance;

            if (runManager == null)
            {
                Debug.LogError("RunGameplay 缺少 GameManager");
                return;
            }

            if (soloRunning || networkManager.ConnectedClientsIds.Count==1)
                runManager.StartSinglePlayerRun();
            else
                runManager.StartMultiplayerRun();
        }
    }

    private bool SpawnPlayers()
    {
        MatchSpawnPoints spawns =
            FindFirstObjectByType<MatchSpawnPoints>();

        if (spawns == null)
        {
            Debug.LogError("场景没有 MatchSpawnPoints");
            return false;
        }

        List<ulong> clientIds =
            new List<ulong>(networkManager.ConnectedClientsIds);

        clientIds.Sort();

        if (spawns.Count < clientIds.Count)
        {
            Debug.LogError("出生点数量不足");
            return false;
        }

        NetworkObject prefab =
            selectedMode == GameMode.Arena
                ? arenaPlayerPrefab
                : runnerPlayerPrefab;

        if (prefab == null)
        {
            Debug.LogError("玩家 Prefab 尚未配置");
            return false;
        }

        // A mode pawn is scene-local. Explicitly remove a stale PlayerObject
        // before replacing it so a RunLoop -> Arena transition cannot leave a
        // hidden Runner updating in the new scene.
        foreach (ulong clientId in clientIds)
        {
            NetworkObject existing = networkManager.ConnectedClients[clientId].PlayerObject;
            if (existing != null && existing.IsSpawned)
            {
                existing.Despawn(true);
            }
        }

        for (int i = 0; i < clientIds.Count; i++)
        {
            if (!spawns.TryGetSpawn(
                i,
                out Vector3 position,
                out Quaternion rotation))
            {
                return false;
            }

            ulong clientId = clientIds[i];

            // Legacy Arena spawn_1 is outside the runtime arena wall. Use
            // compact, centered co-op spawns inside the playable bounds.
            if (selectedMode == GameMode.Arena)
            {
                position = new Vector3((i - (clientIds.Count - 1) * .5f) * 3f,0,-6f);
                rotation = Quaternion.identity;
            }

            NetworkObject player =
                Instantiate(prefab, position, rotation);

            player.SpawnAsPlayerObject(clientId, true);

            Debug.Log(
                $"Spawn Player: Client={clientId}, " +
                $"Mode={selectedMode}, Position={position}");
        }

        return true;
    }

    private void CapturePlayerBuilds()
    {
        preservedBuilds.Clear();
        preservedFirepower.Clear();
        foreach (KeyValuePair<ulong, NetworkClient> pair in networkManager.ConnectedClients)
        {
            NetworkObject playerObject = pair.Value.PlayerObject;
            ItemManager itemManager = playerObject != null
                ? playerObject.GetComponent<ItemManager>()
                : null;
            preservedBuilds[pair.Key] = itemManager != null
                ? itemManager.CaptureBuildSnapshot()
                : System.Array.Empty<PlayerItemSnapshot>();
            CrowdManager runnerStats = playerObject != null ? playerObject.GetComponent<CrowdManager>() : null;
            ArenaPlayerNetwork arenaStats=playerObject!=null?playerObject.GetComponent<ArenaPlayerNetwork>():null;
            preservedFirepower[pair.Key] = runnerStats != null ? runnerStats.Firepower : arenaStats!=null?arenaStats.Firepower:20;
        }
    }

    private void RestorePlayerBuildsForArena()
    {
        foreach (KeyValuePair<ulong, NetworkClient> pair in networkManager.ConnectedClients)
        {
            NetworkObject playerObject = pair.Value.PlayerObject;
            if (playerObject == null)
                continue;

            if (preservedFirepower.TryGetValue(pair.Key, out int firepower))
                playerObject.GetComponent<ArenaPlayerNetwork>()?.SetRunFirepowerServer(firepower);

            ItemManager itemManager = playerObject.GetComponent<ItemManager>();
            if (itemManager == null)
            {
                Debug.LogError($"Arena player {pair.Key} is missing ItemManager.");
                continue;
            }

            itemManager.SetGameplayModeServer(GameplayMode.BossArena);
            if (!preservedBuilds.TryGetValue(pair.Key, out PlayerItemSnapshot[] build))
            {
                GrantBossPracticeBuild(itemManager,pair.Key);
                continue;
            }

            for (int index = 0; index < build.Length; index++)
                itemManager.TryRestoreServer(build[index]);
        }
    }

    private void GrantBossPracticeBuild(ItemManager items,ulong clientId)
    {
        // Direct practice has a viable build. A tower transition always keeps
        // exactly the player's earned equipment, even when that build is empty.
        int[][] teamBuilds={new[]{1302,1323},new[]{1301,1322},new[]{1002,1324},new[]{1303,1325}};
        var ids=new List<ulong>(networkManager.ConnectedClientsIds);ids.Sort();
        int[] loadout=ids.Count==1?new[]{1302,1301,1322}:teamBuilds[Mathf.Max(0,ids.IndexOf(clientId))%4];
        foreach(int id in loadout)
        {
            var definition=RuntimeItemCatalog.Default?.Find(id);
            if(definition!=null)items.TryAcquireServer(definition,ItemAcquisitionSource.Debug);
        }
    }


    void HandleConnected(ulong clientId)
    {
        UpdateLobbyUI();
    }

    void HandleDisconnected(ulong clientId)
    {
        if (!networkManager.IsServer)
        {
            phase = RoomPhase.Menu;
            ShowMainMenu();
            return;
        }

        UpdateLobbyUI();
    }

    void ShowMainMenu()
    {
        networkUIRoot.SetActive(true);
        mainMenuPanel.SetActive(true);
        lobbyPanel.SetActive(false);
        UpdateModeText();
    }

    void ShowLobby()
    {
        networkUIRoot.SetActive(true);
        mainMenuPanel.SetActive(false);
        lobbyPanel.SetActive(true);
        UpdateLobbyUI();
    }

    void UpdateModeText()
    {
        modeText.text = selectedMode == GameMode.Arena
                ? "首领练习"
                : "跑酷旅程";
    }

    void UpdateLobbyUI()
    {
        
        if (phase != RoomPhase.Lobby)
            return;

        if (!networkManager.IsHost)
        {
            SetStatus("等待房主开始游戏...");
            return;
        }

        int count = networkManager.ConnectedClientsIds.Count;

        int maxPlayers = soloRunning ? 1 : 4;

        if (playerCountText != null)
            playerCountText.text = $"{count} / {maxPlayers}";

        bool canStart = count >= (soloRunning ? 1 : 2);

        if (startMatchButton != null)
        {
            startMatchButton.interactable = canStart;
            startMatchButton.gameObject.SetActive(!soloRunning);
        }

        SetStatus(canStart
            ? "人数已满足，可以开始游戏"
            : "等待其他玩家加入...");
    }

    void SetStatus(string message)
    {
        if (statusText != null)
            statusText.text = message;
    }

    private void BuildMinimalMenu()
    {
        if (networkUIRoot != null)
        {
            Canvas legacyCanvas = networkUIRoot.GetComponentInParent<Canvas>();
            if (legacyCanvas != null) legacyCanvas.gameObject.SetActive(false);
            else networkUIRoot.SetActive(false);
        }
        networkUIRoot = MinimalUI.Canvas(transform,"SessionCanvas",20);
        var background = MinimalUI.Box(networkUIRoot.transform,"MenuPaper",MinimalUI.Paper,true);
        var eyebrow = MinimalUI.Text(background,"Eyebrow","R U N N I N G",16,MinimalUI.Muted);
        MinimalUI.Place(eyebrow.rectTransform,new Vector2(.15f,.76f),new Vector2(.85f,.82f));
        var title = MinimalUI.Text(background,"Title","开始一段行程",42);
        MinimalUI.Place(title.rectTransform,new Vector2(.15f,.62f),new Vector2(.90f,.76f));
        var subtitle = MinimalUI.Text(background,"Subtitle","单人出发，或与队友一起选择路线。",19,MinimalUI.Muted);
        MinimalUI.Place(subtitle.rectTransform,new Vector2(.15f,.55f),new Vector2(.90f,.63f));
        mainMenuPanel = new GameObject("MainMenu",typeof(RectTransform));
        mainMenuPanel.transform.SetParent(background,false);
        MinimalUI.Stretch((RectTransform)mainMenuPanel.transform);
        MenuButton(mainMenuPanel.transform,"Solo","独自出发",.15f,StartSoloRunning,true,.16f);
        MenuButton(mainMenuPanel.transform,"BossPractice","首领练习",.33f,StartSoloBoss,false,.16f);
        MenuButton(mainMenuPanel.transform,"Create","创建房间",.51f,CreateRoom,false,.16f);
        MenuButton(mainMenuPanel.transform,"Join","加入房间",.69f,JoinRoom,false,.16f);
        lobbyPanel = new GameObject("Lobby",typeof(RectTransform));
        lobbyPanel.transform.SetParent(background,false);
        MinimalUI.Stretch((RectTransform)lobbyPanel.transform);
        modeText = MinimalUI.Text(lobbyPanel.transform,"Mode","",19);
        MinimalUI.Place(modeText.rectTransform,new Vector2(.15f,.43f),new Vector2(.51f,.5f));
        playerCountText = MinimalUI.Text(lobbyPanel.transform,"Players","",19,MinimalUI.Muted,TextAlignmentOptions.MidlineRight);
        MinimalUI.Place(playerCountText.rectTransform,new Vector2(.59f,.43f),new Vector2(.85f,.5f));
        startMatchButton = MenuButton(lobbyPanel.transform,"Start","开始行程",.15f,StartMatch,true);
        MenuButton(lobbyPanel.transform,"RunningMode","跑酷旅程",.39f,SelectRunning);
        MenuButton(lobbyPanel.transform,"ArenaMode","首领练习",.63f,SelectArena);
        var leave = MinimalUI.Button(lobbyPanel.transform,"Leave","返回",LeaveRoom);
        MinimalUI.Place((RectTransform)leave.transform,new Vector2(.15f,.17f),new Vector2(.24f,.22f));
        statusText = MinimalUI.Text(background,"Status","",16,MinimalUI.Muted);
        MinimalUI.Place(statusText.rectTransform,new Vector2(.15f,.24f),new Vector2(.85f,.30f));
        MinimalUI.Rule(background,.13f);
        var footer = MinimalUI.Text(background,"Footer","单人  /  2–4 人联机",15,MinimalUI.Muted);
        MinimalUI.Place(footer.rectTransform,new Vector2(.15f,.05f),new Vector2(.85f,.12f));
    }

    private static Button MenuButton(Transform parent,string name,string label,float x,UnityEngine.Events.UnityAction action,bool primary=false,float width=.22f)
    {
        var button = MinimalUI.Button(parent,name,label,action,primary);
        MinimalUI.Place((RectTransform)button.transform,new Vector2(x,.33f),new Vector2(x+width,.41f));
        return button;
    }


    // Update is called once per frame
    void Update()
    {
        
    }
}
