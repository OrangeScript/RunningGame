using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using Unity.Netcode;
using UnityEditor.VersionControl;
using UnityEngine;
using UnityEngine.UI;

public class NetworkLauncher : MonoBehaviour
{

    [Header("Panels")]

    [SerializeField]
    private GameObject networkUIRoot;

    [SerializeField]
    private GameObject mainMenuPanel;

    [SerializeField]
    private GameObject lobbyPanel;


    [Header("Lobby UI")]

    [SerializeField]
    private TMP_Text playerCountText;

    [SerializeField]
    private TMP_Text statusText;

    [SerializeField]
    private Button startGameButton;

    [SerializeField]
    private NetworkManager
        networkManager;


    private GameManager
        gameManager;


    private bool
        singlePlayerPending;


    private Coroutine
        bindRoutine;

    void Awake()
    {
        
        ShowMainMenu();
    }
    void OnDestroy()
    {
        UnbindGameManager();
        networkManager.OnClientDisconnectCallback -= HandleClientDisconnected;
    }

    void Start()
    {
        networkManager = FindFirstObjectByType<NetworkManager>();
        if (networkManager == null)
        {
            Debug.LogError(
                "找不到 NetworkManager"
            );

            return;
        }
        //client 连接时候的效果呢？
        networkManager.NetworkConfig.ConnectionApproval = true;
        networkManager.OnClientConnectedCallback += HandleClientDisconnected;
        
    }
    private void HandleClientDisconnected(ulong obj)
    {
        if (
            networkManager != null &&
            !networkManager.IsServer
        )
        {
            string reason =
                networkManager
                    .DisconnectReason;


            UnbindGameManager();


            ShowMainMenu();


            if (
                !string.IsNullOrEmpty(
                    reason
                )
            )
            {
                ShowStatus(
                    reason
                );
            }
        }
    }

    public void StartSinglePlayer()
    {
        singlePlayerPending = true;
        ConfigureHost();
        bool success = networkManager.StartHost();
        if (success)
        {
            ShowLobby("正在进入单机...");
            StartBinding();
        }
    }

    public void CreateRoom()
    {
        singlePlayerPending = false;
        ConfigureHost();
        bool success = networkManager.StartHost();
        if (success)
        {
            ShowLobby("等待其他玩家加入...");
            StartBinding();
        }
    }

    private void ShowLobby(string v)
    {
        networkUIRoot.SetActive(true);
        mainMenuPanel.SetActive(false);
        lobbyPanel.SetActive(true);
        ShowStatus(v);
    }

    private void ShowStatus(string v)
    {
        statusText.text = v;
    }

    public void JoinRoom()
    {
        singlePlayerPending = false;
        if (networkManager.StartClient())
        {
            ShowLobby("正在连接房间..");
            StartBinding();
        }
    }


    public void StartGame()
    {
        if (!networkManager.IsHost)
        {
            return;
        }
        gameManager.StartMultiplayerRun();    
    }

    public void LeaveRoom()
    {
        singlePlayerPending = false;
        UnbindGameManager();
        networkManager.Shutdown();
        ShowMainMenu();
    }


    private void ConfigureHost()
    {
        networkManager.ConnectionApprovalCallback = ApprovalCheck;
    }

    private void ApprovalCheck(NetworkManager.ConnectionApprovalRequest request, NetworkManager.ConnectionApprovalResponse response)
    {
        int maxPlayers = 4;
        bool roomFull = networkManager.ConnectedClientsIds.Count >= maxPlayers;

        bool gameStarted = GameManager.instance.state != RunState.Lobby;
        bool approved = !roomFull && !gameStarted;


        response.Approved = approved;
        response.CreatePlayerObject = approved;
        response.PlayerPrefabHash = null;
        response.Position = Vector3.zero;
        response.Rotation = Quaternion.identity;
        response.Pending = false;
        if (roomFull)
        {
            response.Reason =
                "房间已满";
        }
        else if (gameStarted)
        {
            response.Reason =
                "游戏已经开始";
        }
    }


    private void StartBinding()
    {
        if(bindRoutine != null)
        {
            StopCoroutine(bindRoutine);
        }
        bindRoutine = StartCoroutine(BindWhenReady());
    }

    private IEnumerator BindWhenReady()
    {
        while (
            networkManager != null &&
            networkManager.IsListening
        )
        {
            if (
                GameManager.instance != null &&
                GameManager.instance.IsSpawned
            )
            {
                break;
            }


            yield return null;
        }


        if (
            networkManager == null ||
            !networkManager.IsListening
        )
        {
            yield break;
        }


        BindGameManager(
            GameManager.instance
        );


        // 单机模式：
        // Host建立成功以后直接开始
        if (
            singlePlayerPending &&
            networkManager.IsHost
        )
        {
            singlePlayerPending =
                false;


            gameManager
                .StartSinglePlayerRun();
        }
    }

    private void BindGameManager(GameManager newGameManager)
    {
        UnbindGameManager();


        gameManager =
            newGameManager;


        if (gameManager == null)
            return;


        gameManager
            .PlayerCountChanged +=
            HandlePlayerCountChanged;


        gameManager
            .StateChanged +=
            HandleStateChanged;


        HandlePlayerCountChanged(
            gameManager
                .ConnectedPlayerCount
        );


        HandleStateChanged(
            gameManager.state
        );
    }

    private void UnbindGameManager()
    {
        if (gameManager == null)
            return;


        gameManager
            .PlayerCountChanged -=
            HandlePlayerCountChanged;


        gameManager
            .StateChanged -=
            HandleStateChanged;


        gameManager =
            null;
    }

    private void HandleStateChanged(RunState state)
    {
        if (
            state ==
            RunState.Lobby
        )
        {
            if (networkUIRoot != null)
            {
                networkUIRoot
                    .SetActive(true);
            }


            if (mainMenuPanel != null)
            {
                mainMenuPanel
                    .SetActive(false);
            }


            if (lobbyPanel != null)
            {
                lobbyPanel
                    .SetActive(true);
            }


            return;
        }


        // 真正进入游戏以后
        // 关闭整个“网络菜单”
        if (networkUIRoot != null)
        {
            networkUIRoot
                .SetActive(false);
                lobbyPanel.SetActive(false);
            Debug.Log("关闭");
        }
    }

    private void ShowMainMenu()
    {
        networkUIRoot.SetActive(true);
        mainMenuPanel.SetActive(true);
        lobbyPanel.SetActive(false);
    }

    private void HandlePlayerCountChanged(int count)
    {
        if(playerCountText != null)
        {
            playerCountText.text = $"{count} / {gameManager.MaxPlayers}";
        }
        bool isHost =
            networkManager != null &&
            networkManager.IsHost;


        if (startGameButton != null)
        {
            // 只有房主能看到“开始游戏”
            startGameButton.gameObject
                .SetActive(
                    isHost
                );


            startGameButton.interactable =
                isHost &&
                count >=
                    gameManager
                        .MinMultiplayerPlayers;
        }


        if (statusText == null)
            return;


        if (isHost)
        {
            if (
                count <
                gameManager
                    .MinMultiplayerPlayers
            )
            {
                statusText.text =
                    $"等待其他玩家加入...\n" +
                    $"至少需要 " +
                    $"{gameManager.MinMultiplayerPlayers} 人";
            }
            else
            {
                statusText.text =
                    "人数已满足，可以开始游戏";
            }
        }
        else
        {
            statusText.text =
                "等待房主开始游戏...";
        }
    }

}
