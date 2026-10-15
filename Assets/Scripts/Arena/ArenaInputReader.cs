using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(PlayerInput))]
public class ArenaInputReader : MonoBehaviour
{
    private PlayerInput playerInput;
    private InputAction move;
    private InputAction jump;
    private InputAction look;
    private InputAction sprint;

    void Awake()
    {
        playerInput = GetComponent<PlayerInput>();
    }

    

}
