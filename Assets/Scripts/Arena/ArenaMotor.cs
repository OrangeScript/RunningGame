using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(CharacterController))]
public class ArenaMotor : MonoBehaviour
{
    [Header("Movement")]
    [SerializeField, Min(0f)] private float maxSpeed = 7f;
    [SerializeField, Min(0f)] private float acceleration = 35f;
    [SerializeField, Min(0f)] private float deceleration = 45f;
    [SerializeField, Min(0f)] private float turnSpeedDegrees = 720f;
    [SerializeField] private float gravity = -20f;
    [SerializeField] private float groundedVerticalSpeed = -2f;

    [Header("World XZ Bounds")]
    [SerializeField] private Vector2 arenaCenter;
    [SerializeField] private Vector2 arenaHalfExtents = new Vector2(18f, 12f);
    [SerializeField, Min(0f)] private float boundaryPadding = 0.1f;

    private CharacterController characterController;
    private Vector3 planarVelocity;
    private float verticalVelocity;
    private bool simulationEnabled;

    public Vector3 PlanarVelocity => planarVelocity;
    public float MaxSpeed => maxSpeed;
    public bool SimulationEnabled => simulationEnabled;

    private void Awake()
    {
        characterController = GetComponent<CharacterController>();
        SetSimulationEnabled(false);
    }

    public void SetSimulationEnabled(bool enabledForServer)
    {
        simulationEnabled = enabledForServer;
        if (characterController == null)
        {
            characterController = GetComponent<CharacterController>();
        }

        if (characterController != null)
        {
            characterController.enabled = enabledForServer;
        }

        if (!enabledForServer)
        {
            planarVelocity = Vector3.zero;
            verticalVelocity = 0f;
        }
    }

    public void SetBounds(Vector2 center, Vector2 halfExtents)
    {
        arenaCenter = center;
        arenaHalfExtents = new Vector2(
            Mathf.Max(0f, halfExtents.x),
            Mathf.Max(0f, halfExtents.y));
    }

    public void Simulate(Vector2 moveInput, Vector2 aimInput, float deltaTime)
    {
        if (!simulationEnabled || deltaTime <= 0f)
        {
            return;
        }

        if (characterController == null)
        {
            characterController = GetComponent<CharacterController>();
        }

        if (characterController == null || !characterController.enabled)
        {
            return;
        }

        moveInput = ArenaProtocol.ClampToUnitCircle(moveInput);
        Vector3 desiredVelocity = new Vector3(moveInput.x, 0f, moveInput.y) * maxSpeed;
        float rate = desiredVelocity.sqrMagnitude > planarVelocity.sqrMagnitude
            ? acceleration
            : deceleration;

        planarVelocity = Vector3.MoveTowards(
            planarVelocity,
            desiredVelocity,
            Mathf.Max(0f, rate) * deltaTime);

        if (characterController.isGrounded && verticalVelocity < 0f)
        {
            verticalVelocity = groundedVerticalSpeed;
        }
        else
        {
            verticalVelocity += gravity * deltaTime;
        }

        Vector3 displacement = planarVelocity * deltaTime;
        displacement.y = verticalVelocity * deltaTime;
        displacement = ClampDisplacementToBounds(displacement);
        characterController.Move(displacement);

        RotateTowardsAim(aimInput, deltaTime);
    }

    public void Stop()
    {
        planarVelocity = Vector3.zero;
    }

    private Vector3 ClampDisplacementToBounds(Vector3 displacement)
    {
        Vector3 current = transform.position;
        Vector3 target = current + displacement;
        float controllerRadius = characterController != null
            ? Mathf.Max(0f, characterController.radius)
            : 0f;

        float inset = controllerRadius + Mathf.Max(0f, boundaryPadding);
        float minX = arenaCenter.x - Mathf.Max(0f, arenaHalfExtents.x - inset);
        float maxX = arenaCenter.x + Mathf.Max(0f, arenaHalfExtents.x - inset);
        float minZ = arenaCenter.y - Mathf.Max(0f, arenaHalfExtents.y - inset);
        float maxZ = arenaCenter.y + Mathf.Max(0f, arenaHalfExtents.y - inset);

        target.x = Mathf.Clamp(target.x, minX, maxX);
        target.z = Mathf.Clamp(target.z, minZ, maxZ);
        displacement.x = target.x - current.x;
        displacement.z = target.z - current.z;

        if (Mathf.Abs(displacement.x) < Mathf.Abs(planarVelocity.x * Time.deltaTime))
        {
            planarVelocity.x = 0f;
        }

        if (Mathf.Abs(displacement.z) < Mathf.Abs(planarVelocity.z * Time.deltaTime))
        {
            planarVelocity.z = 0f;
        }

        return displacement;
    }

    private void RotateTowardsAim(Vector2 aimInput, float deltaTime)
    {
        if (!ArenaProtocol.IsFinite(aimInput) ||
            aimInput.sqrMagnitude < ArenaProtocol.MinimumAimMagnitude * ArenaProtocol.MinimumAimMagnitude)
        {
            return;
        }

        Vector3 forward = new Vector3(aimInput.x, 0f, aimInput.y).normalized;
        Quaternion targetRotation = Quaternion.LookRotation(forward, Vector3.up);
        transform.rotation = Quaternion.RotateTowards(
            transform.rotation,
            targetRotation,
            Mathf.Max(0f, turnSpeedDegrees) * deltaTime);
    }

    private void OnValidate()
    {
        arenaHalfExtents.x = Mathf.Max(0f, arenaHalfExtents.x);
        arenaHalfExtents.y = Mathf.Max(0f, arenaHalfExtents.y);
    }
}
