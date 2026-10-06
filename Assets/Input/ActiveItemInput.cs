using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public class ActiveItemInput : MonoBehaviour
{
    [Header("References")]

    [SerializeField]
    private PlayerInput playerInput;

    [SerializeField]
    private ItemManager itemManager;


    private InputAction activeSkill1;
    private InputAction activeSkill2;


    private void Awake()
    {
        if (playerInput == null)
        {
            playerInput =
                GetComponent<PlayerInput>();
        }

        if (itemManager == null)
        {
            itemManager =
                GetComponent<ItemManager>();
        }


        activeSkill1 =
            playerInput.actions[
                "ActiveSkill1"
            ];

        activeSkill2 =
            playerInput.actions[
                "ActiveSkill2"
            ];
    }


    private void OnEnable()
    {
        activeSkill1.performed +=
            HandleActiveSkill1;

        activeSkill2.performed +=
            HandleActiveSkill2;
    }


    private void OnDisable()
    {
        activeSkill1.performed -=
            HandleActiveSkill1;

        activeSkill2.performed -=
            HandleActiveSkill2;
    }


    private void HandleActiveSkill1(
        InputAction.CallbackContext context
    )
    {
        if (itemManager == null)
            return;


        itemManager
                .RequestActivateActiveItem(0);


    }


    private void HandleActiveSkill2(
        InputAction.CallbackContext context
    )
    {
        if (itemManager == null)
            return;


        
            itemManager
                .RequestActivateActiveItem(1);


   
    }
}
