using UnityEngine;
using UnityEngine.InputSystem;

[DisallowMultipleComponent]
[RequireComponent(typeof(PlayerInput))]
public class ArenaInputReader : MonoBehaviour
{
    [Header("Actions")]
    [SerializeField] private string actionMapName = "Arena";
    [SerializeField] private string moveActionName = "Move";
    [SerializeField] private string lookActionName = "Look";
    [SerializeField] private string fireActionName = "Jump";

    [Header("Aiming")]
    [SerializeField] private Transform aimOrigin;
    [SerializeField] private Camera aimCamera;
    [SerializeField, Range(0f, 0.95f)] private float stickDeadZone = 0.2f;

    private PlayerInput playerInput;
    private InputAction moveAction;
    private InputAction lookAction;
    private InputAction fireAction;
    private Vector2 lastAim = Vector2.up;
    private bool ownerInputEnabled;
    private bool pointerWasLastAimDevice = true;

    public bool OwnerInputEnabled => ownerInputEnabled;
    public Vector2 LastAim => lastAim;

    private void Awake()
    {
        playerInput = GetComponent<PlayerInput>();
        if (aimOrigin == null)
        {
            aimOrigin = transform;
        }

        CacheActions();
        SetPlayerInputActive(false);
    }

    private void OnEnable()
    {
        if (ownerInputEnabled)
        {
            SetPlayerInputActive(true);
        }
    }

    private void OnDisable()
    {
        if (playerInput != null)
        {
            playerInput.DeactivateInput();
        }
    }

    public void SetOwnerInputEnabled(bool enabledForOwner)
    {
        ownerInputEnabled = enabledForOwner;
        SetPlayerInputActive(enabledForOwner && isActiveAndEnabled);

        if (enabledForOwner)
        {
            CacheActions();
        }
    }

    public void SetAimCamera(Camera cameraToUse)
    {
        aimCamera = cameraToUse;
    }

    public bool TryCapture(ushort sequence, out ArenaInputFrame frame)
    {
        frame = default;
        if (!ownerInputEnabled || playerInput == null || !playerInput.enabled)
        {
            return false;
        }

        if (moveAction == null || lookAction == null || fireAction == null)
        {
            CacheActions();
        }

        Vector2 move = moveAction != null
            ? moveAction.ReadValue<Vector2>()
            : Vector2.zero;

        UpdateAim();

        bool fireHeld = (fireAction != null && fireAction.IsPressed()) ||
                        (Mouse.current != null && Mouse.current.leftButton.isPressed);
        frame = ArenaProtocol.Pack(sequence, move, lastAim, fireHeld);
        if (Keyboard.current != null && (Keyboard.current.leftShiftKey.isPressed || Keyboard.current.rightShiftKey.isPressed))
            frame.Buttons |= ArenaInputButtons.Focus;
        return true;
    }

    private void UpdateAim()
    {
        Vector2 rawLook = lookAction != null
            ? lookAction.ReadValue<Vector2>()
            : Vector2.zero;

        InputDevice activeDevice = lookAction != null && lookAction.activeControl != null
            ? lookAction.activeControl.device
            : null;

        if (activeDevice != null)
        {
            pointerWasLastAimDevice = activeDevice is Pointer;
        }

        if (pointerWasLastAimDevice && TryReadPointerAim(out Vector2 pointerAim))
        {
            lastAim = pointerAim;
            return;
        }

        float deadZoneSquared = stickDeadZone * stickDeadZone;
        if (rawLook.sqrMagnitude > deadZoneSquared && ArenaProtocol.IsFinite(rawLook))
        {
            lastAim = rawLook.normalized;
        }
    }

    private bool TryReadPointerAim(out Vector2 aim)
    {
        aim = lastAim;
        Pointer pointer = Pointer.current;
        if (pointer == null)
        {
            return false;
        }

        Camera cameraToUse = aimCamera != null ? aimCamera : Camera.main;
        if (cameraToUse == null)
        {
            return false;
        }

        Transform origin = aimOrigin != null ? aimOrigin : transform;
        Plane aimPlane = new Plane(Vector3.up, origin.position);
        Ray ray = cameraToUse.ScreenPointToRay(pointer.position.ReadValue());
        if (!aimPlane.Raycast(ray, out float distance))
        {
            return false;
        }

        Vector3 worldPoint = ray.GetPoint(distance);
        Vector3 difference = worldPoint - origin.position;
        Vector2 planar = new Vector2(difference.x, difference.z);
        if (!ArenaProtocol.IsFinite(planar) ||
            planar.sqrMagnitude < ArenaProtocol.MinimumAimMagnitude * ArenaProtocol.MinimumAimMagnitude)
        {
            return false;
        }

        aim = planar.normalized;
        return true;
    }

    private void CacheActions()
    {
        if (playerInput == null || playerInput.actions == null)
        {
            moveAction = null;
            lookAction = null;
            fireAction = null;
            return;
        }

        moveAction = FindAction(moveActionName);
        lookAction = FindAction(lookActionName);
        fireAction = FindAction(fireActionName);
    }

    private InputAction FindAction(string actionName)
    {
        if (string.IsNullOrWhiteSpace(actionName) || playerInput == null || playerInput.actions == null)
        {
            return null;
        }

        if (!string.IsNullOrWhiteSpace(actionMapName))
        {
            InputAction qualified = playerInput.actions.FindAction(
                actionMapName + "/" + actionName,
                false);

            if (qualified != null)
            {
                return qualified;
            }
        }

        return playerInput.actions.FindAction(actionName, false);
    }

    private void SetPlayerInputActive(bool active)
    {
        if (playerInput == null)
        {
            return;
        }

        if (active)
        {
            if (!playerInput.enabled)
            {
                playerInput.enabled = true;
            }

            playerInput.ActivateInput();
        }
        else
        {
            playerInput.DeactivateInput();
            playerInput.enabled = false;
        }
    }
}
