using System;
using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(CharacterController))]
public class RunnerController : NetworkBehaviour
{

    [Header("Movement")]
    [SerializeField] private float forwardSpeed = 8f;
    [SerializeField] private float horizontalSpeed = 7f;

    [Header("Boundary")]
    [SerializeField] private float xLimit = RunSectionPresentation.MovementHalfWidth;

    private CharacterController controller;
    private PlayerInput playerInput;
    private CrowdManager crowd;
    private float serverMoveInput;

    private readonly NetworkVariable<PlayerRunPhase> playerPhase =
        new NetworkVariable<PlayerRunPhase>(
            PlayerRunPhase.Lobby,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);

    private readonly NetworkVariable<int> laneId =
        new NetworkVariable<int>(0,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);

    private readonly NetworkVariable<float> laneCenterX =
        new NetworkVariable<float>(0f,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);

    private readonly NetworkVariable<float> laneHalfWidth =
        new NetworkVariable<float>(RunSectionPresentation.MovementHalfWidth,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);

    public PlayerRunPhase Phase => playerPhase.Value;
    public int LaneId => laneId.Value;
    public float LaneCenterX => laneCenterX.Value;
    public float MovementHalfWidth => laneHalfWidth.Value;


    void Awake()
    {
        controller = GetComponent<CharacterController>();
        playerInput = GetComponent<PlayerInput>();
        crowd = GetComponent<CrowdManager>();

        playerInput.enabled = false;
        controller.enabled = false;

    }

    public override void OnNetworkSpawn()
    {
        playerInput.enabled = IsOwner;
        controller.enabled = IsServer;
        if (IsServer)
        {
            // 防止Host和Client出生完全重叠
            transform.position +=
                Vector3.right *
                (float)OwnerClientId *
                0.15f;

            laneCenterX.Value = transform.position.x;
            laneHalfWidth.Value = xLimit;
            playerPhase.Value = PlayerRunPhase.Lobby;
        }

        if (crowd != null)
            crowd.PopulationDepleted += HandlePopulationDepleted;


        if (IsOwner)
        {
            CameraFollow cameraFollow =
                Camera.main != null
                    ? Camera.main
                        .GetComponent<CameraFollow>()
                    : null;


            if (cameraFollow != null)
            {
                cameraFollow.SetTarget(
                    transform
                );
            }


            PopulationUI populationUI =
                FindFirstObjectByType<
                    PopulationUI
                >();


            if (populationUI != null)
            {
                populationUI.Bind(
                    GetComponent<CrowdManager>()
                );
            }

            RunnerRuntimeHUD.Ensure(
                this,
                crowd,
                GetComponent<ItemManager>());
        }
    }

    public override void OnNetworkDespawn()
    {
        playerInput.enabled = false;
        if (crowd != null)
            crowd.PopulationDepleted -= HandlePopulationDepleted;
    }
    // Start is called before the first frame update
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        if(!IsServer) return;
        if(!controller.enabled) return;
        HandleMovement();
    }


    void OnMove(InputValue value)
    {
        if(!IsOwner) return;
        float moveInput = value.Get<float>();
        SubmitMoveInputServerRpc(moveInput);
    }

    [ServerRpc]
    private void SubmitMoveInputServerRpc(float input)
    {
        serverMoveInput = Mathf.Clamp(input,-1f,1f);
    }


    void HandleMovement()
    {
        PlayerRunPhase currentPhase = playerPhase.Value;
        if (currentPhase == PlayerRunPhase.Lobby ||
            currentPhase == PlayerRunPhase.RewardChoice ||
            currentPhase == PlayerRunPhase.WaitingAtExit ||
            currentPhase == PlayerRunPhase.Eliminated)
        {
            return;
        }

        bool forwardEnabled = currentPhase == PlayerRunPhase.Running;
        float zSpeed = forwardEnabled? forwardSpeed:0f;


        // 玩家这一帧本来想横向移动多少
        float horizontalDelta =
            serverMoveInput *
            horizontalSpeed *
            Time.deltaTime;


        // 预计到达的位置
        float targetX =
            transform.position.x +
            horizontalDelta;


        // 先把目标位置限制在道路内
        targetX =
            Mathf.Clamp(
                targetX,
                laneCenterX.Value - laneHalfWidth.Value,
                laneCenterX.Value + laneHalfWidth.Value
            );


        // 最终这一帧实际允许移动多少
        float finalHorizontalDelta =
            targetX -
            transform.position.x;


        Vector3 displacement =
            new Vector3(
                finalHorizontalDelta,
                -2f * Time.deltaTime,
                zSpeed *
                Time.deltaTime
            );


        controller.Move(
            displacement
        );
    }

    public void ConfigureLaneServer(int newLaneId, float centerX, float halfWidth)
    {
        if (!IsServer)
            return;

        laneId.Value = Mathf.Max(0, newLaneId);
        laneCenterX.Value = centerX;
        laneHalfWidth.Value = Mathf.Max(0.5f, halfWidth);
    }

    public void TeleportToLaneServer(
        int newLaneId,
        Vector3 position,
        float halfWidth)
    {
        if (!IsServer)
            return;

        bool wasEnabled = controller != null && controller.enabled;
        if (wasEnabled)
            controller.enabled = false;

        transform.position = position;
        ConfigureLaneServer(newLaneId, position.x, halfWidth);

        if (wasEnabled)
            controller.enabled = true;
    }

    public void BeginRunServer()
    {
        SetPhaseServer(PlayerRunPhase.Running);
    }

    public void EnterCombatServer()
    {
        SetPhaseServer(PlayerRunPhase.Combat);
    }

    public void EnterRewardChoiceServer()
    {
        SetPhaseServer(PlayerRunPhase.RewardChoice);
    }

    public void ResumeRunningServer()
    {
        SetPhaseServer(PlayerRunPhase.Running);
    }

    public void WaitAtExitServer()
    {
        SetPhaseServer(PlayerRunPhase.WaitingAtExit);
    }

    private void SetPhaseServer(PlayerRunPhase phase)
    {
        if (IsServer)
            playerPhase.Value = phase;
    }

    private void HandlePopulationDepleted()
    {
        if (!IsServer)
            return;

        playerPhase.Value = PlayerRunPhase.Eliminated;
        GameManager.instance?.NotifyRunnerEliminated(this);
    }

}
