using System.Collections;
using System.Collections.Generic;
using TMPro;
using Unity.Netcode;
using Unity.VisualScripting.Dependencies.Sqlite;
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

    private GameMode selectedMode = GameMode.Arena;
    RoomPhase phase = RoomPhase.Menu;
    bool soloRunning;




    // Start is called before the first frame update
    void Awake()
    {
        if(networkManager == null) 
        networkManager = GetComponent<NetworkManager>();
        DontDestroyOnLoad(this);
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
        if (phase != RoomPhase.Menu)
            return;

        selectedMode = GameMode.Arena;
        UpdateModeText();
    }

    public void SelectRunning()
    {
        if (phase != RoomPhase.Menu)
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

    bool StartHostRoom()
    {
        if(networkManager.IsListening) return false;
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
        int maxPlayers = soloRunning ? 1 : selectedMode == GameMode.Arena ? 2 : 4;
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
        bool enoughPlayers = selectedMode == GameMode.Arena ? count == 2 : count >= (soloRunning ? 1:2);
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

        if (isRunning)
        {
            GameManager runManager = GameManager.instance;

            if (runManager == null)
            {
                Debug.LogError("RunGameplay 缺少 GameManager");
                return;
            }

            if (soloRunning)
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

            NetworkObject player =
                Instantiate(prefab, position, rotation);

            player.SpawnAsPlayerObject(clientId);

            Debug.Log(
                $"Spawn Player: Client={clientId}, " +
                $"Mode={selectedMode}, Position={position}");
        }

        return true;
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
                ? "Arena 枪战"
                : "数字跑酷";
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

        int maxPlayers = soloRunning
            ? 1
            : selectedMode == GameMode.Arena ? 2 : 4;

        if (playerCountText != null)
            playerCountText.text = $"{count} / {maxPlayers}";

        bool canStart = selectedMode == GameMode.Arena
            ? count == 2
            : count >= (soloRunning ? 1 : 2);

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


    // Update is called once per frame
    void Update()
    {
        
    }
}
