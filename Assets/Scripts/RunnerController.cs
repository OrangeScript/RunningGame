using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(CharacterController))]
public class RunnerController : MonoBehaviour
{

    [Header("Movement")]
    [SerializeField] private float forwardSpeed = 8f;
    [SerializeField] private float horizontalSpeed = 7f;

    [Header("Boundary")]
    [SerializeField] private float xLimit = 4f;

    private CharacterController controller;
    private float moveInput;

    private bool forwardMovementEnabled = true;

    void Awake()
    {
        controller = GetComponent<CharacterController>();
    }

    // Start is called before the first frame update
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        HandleMovement();
    }


    void OnMove(InputValue value)
    {
        moveInput = value.Get<float>();
    }
    void HandleMovement()
    {
        float currentForwardSpeed =
            forwardMovementEnabled
                ? forwardSpeed
                : 0f;


        // 玩家这一帧本来想横向移动多少
        float horizontalDelta =
            moveInput *
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
                currentForwardSpeed *
                Time.deltaTime
            );


        controller.Move(
            displacement
        );
    }

    internal void SetForwardMovementEnabled(bool enableForwardMovement)
    {
        forwardMovementEnabled = enableForwardMovement;
    }
}
