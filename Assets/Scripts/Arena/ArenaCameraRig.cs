using UnityEngine;

[DisallowMultipleComponent]
public class ArenaCameraRig : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Camera outputCamera;
    [SerializeField] private Transform target;

    [Header("Top-down View")]
    [SerializeField, Min(0.1f)] private float distance = 16f;
    [SerializeField, Range(35f, 89f)] private float pitch = 68f;
    [SerializeField] private float yaw;
    [SerializeField] private Vector3 targetOffset = new Vector3(0f, 0.75f, 0f);

    [Header("Damping")]
    [SerializeField, Min(0f)] private float positionDamping = 12f;
    [SerializeField, Min(0f)] private float rotationDamping = 16f;
    [SerializeField] private bool snapOnBind = true;

    public Camera OutputCamera => outputCamera;
    public Transform Target => target;

    private void Awake()
    {
        ResolveCamera();
    }

    private void LateUpdate()
    {
        if (target == null)
        {
            TryBindLocalOwner();
            if (target == null)
            {
                return;
            }
        }

        UpdatePose(false);
    }

    public void Bind(Transform localOwner)
    {
        if (localOwner == null || target == localOwner)
        {
            return;
        }

        target = localOwner;
        ResolveCamera();
        if (snapOnBind)
        {
            UpdatePose(true);
        }
    }

    public void Unbind(Transform expectedTarget = null)
    {
        if (expectedTarget != null && target != expectedTarget)
        {
            return;
        }

        target = null;
    }

    public bool TryBindLocalOwner()
    {
        ArenaPlayerNetwork[] players = FindObjectsByType<ArenaPlayerNetwork>(
            FindObjectsInactive.Exclude,
            FindObjectsSortMode.None);

        foreach (ArenaPlayerNetwork player in players)
        {
            if (player != null && player.IsSpawned && player.IsOwner)
            {
                Bind(player.transform);
                return true;
            }
        }

        return false;
    }

    private void UpdatePose(bool snap)
    {
        // A shared bullet-hell arena needs a readable view of every edge and
        // the Boss. Following an individual player cropped the encounter.
        Vector3 focusPoint = new Vector3(0,.75f,0);
        pitch = 78f;
        distance = 32f;
        if (outputCamera != null)
        {
            outputCamera.orthographic = true;
            outputCamera.orthographicSize = Mathf.Max(15f,21f/Mathf.Max(.5f,outputCamera.aspect));
            outputCamera.clearFlags = CameraClearFlags.SolidColor;
            outputCamera.backgroundColor = new Color(.83f,.86f,.82f);
        }
        Quaternion orbit = Quaternion.Euler(pitch, yaw, 0f);
        Vector3 desiredPosition = focusPoint + orbit * (Vector3.back * distance);
        Quaternion desiredRotation = Quaternion.LookRotation(
            focusPoint - desiredPosition,
            Vector3.up);

        if (snap)
        {
            transform.SetPositionAndRotation(desiredPosition, desiredRotation);
            return;
        }

        float positionT = DampFactor(positionDamping, Time.unscaledDeltaTime);
        float rotationT = DampFactor(rotationDamping, Time.unscaledDeltaTime);
        transform.position = Vector3.Lerp(transform.position, desiredPosition, positionT);
        transform.rotation = Quaternion.Slerp(transform.rotation, desiredRotation, rotationT);
    }

    private void ResolveCamera()
    {
        if (outputCamera == null)
        {
            outputCamera = GetComponent<Camera>();
        }

        if (outputCamera == null)
        {
            outputCamera = Camera.main;
        }
    }

    private static float DampFactor(float damping, float deltaTime)
    {
        if (damping <= 0f)
        {
            return 1f;
        }

        return 1f - Mathf.Exp(-damping * Mathf.Max(0f, deltaTime));
    }

    private void OnValidate()
    {
        distance = Mathf.Max(0.1f, distance);
        pitch = Mathf.Clamp(pitch, 35f, 89f);
        positionDamping = Mathf.Max(0f, positionDamping);
        rotationDamping = Mathf.Max(0f, rotationDamping);
    }
}
