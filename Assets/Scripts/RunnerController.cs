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

        Vector3 movement = new Vector3(moveInput* horizontalSpeed,-2f,forwardSpeed);

        controller.Move(movement*Time.deltaTime);

        Vector3 position = transform.position;

        position.x = Mathf.Clamp(position.x,-xLimit,xLimit);
        transform.position = position;
    }

}
