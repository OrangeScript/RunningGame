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
    [SerializeField] private float xLimit = 4f;

    private CharacterController controller;
    private PlayerInput playerInput;
    private float serverMoveInput;


    void Awake()
    {
        controller = GetComponent<CharacterController>();
        playerInput = GetComponent<PlayerInput>();

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
        }


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
        }
    }

    public override void OnNetworkDespawn()
    {
        playerInput.enabled = false;
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
        bool forwardEnabled = GameManager.instance.state == RunState.Running;
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
                -xLimit,
                xLimit
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

}
