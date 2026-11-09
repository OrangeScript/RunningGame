using UnityEngine;

[DisallowMultipleComponent]
public class ArenaAnimatorDriver : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Animator animator;
    [SerializeField] private Transform directionSpace;

    [Header("Parameters")]
    [SerializeField] private string speedParameter = "Speed";
    [SerializeField] private string moveXParameter = "MoveX";
    [SerializeField] private string moveYParameter = "MoveY";
    [SerializeField] private string movingParameter = "IsMoving";
    [SerializeField, Min(0f)] private float dampTime = 0.08f;
    [SerializeField, Min(0f)] private float movingThreshold = 0.05f;

    private int speedHash;
    private int moveXHash;
    private int moveYHash;
    private int movingHash;
    private bool hasSpeed;
    private bool hasMoveX;
    private bool hasMoveY;
    private bool hasMoving;
    private float targetSpeed;
    private Vector2 targetLocalMove;

    private void Awake()
    {
        if (animator == null)
        {
            animator = GetComponentInChildren<Animator>();
        }

        if (directionSpace == null)
        {
            directionSpace = transform;
        }

        CacheParameters();
    }

    private void Update()
    {
        if (animator == null)
        {
            return;
        }

        float deltaTime = Time.deltaTime;
        if (hasSpeed)
        {
            animator.SetFloat(speedHash, targetSpeed, dampTime, deltaTime);
        }

        if (hasMoveX)
        {
            animator.SetFloat(moveXHash, targetLocalMove.x, dampTime, deltaTime);
        }

        if (hasMoveY)
        {
            animator.SetFloat(moveYHash, targetLocalMove.y, dampTime, deltaTime);
        }

        if (hasMoving)
        {
            animator.SetBool(movingHash, targetSpeed > movingThreshold);
        }
    }

    public void SetMotion(Vector3 worldVelocity, float referenceSpeed)
    {
        if (!ArenaProtocol.IsFinite(worldVelocity))
        {
            worldVelocity = Vector3.zero;
        }

        worldVelocity.y = 0f;
        float safeReferenceSpeed = Mathf.Max(0.001f, referenceSpeed);
        targetSpeed = Mathf.Clamp01(worldVelocity.magnitude / safeReferenceSpeed);

        Transform space = directionSpace != null ? directionSpace : transform;
        Vector3 localVelocity = space.InverseTransformDirection(worldVelocity);
        targetLocalMove = new Vector2(
            Mathf.Clamp(localVelocity.x / safeReferenceSpeed, -1f, 1f),
            Mathf.Clamp(localVelocity.z / safeReferenceSpeed, -1f, 1f));
    }

    public void ResetMotion()
    {
        targetSpeed = 0f;
        targetLocalMove = Vector2.zero;
    }

    private void CacheParameters()
    {
        speedHash = Animator.StringToHash(speedParameter);
        moveXHash = Animator.StringToHash(moveXParameter);
        moveYHash = Animator.StringToHash(moveYParameter);
        movingHash = Animator.StringToHash(movingParameter);

        hasSpeed = HasParameter(speedParameter, AnimatorControllerParameterType.Float);
        hasMoveX = HasParameter(moveXParameter, AnimatorControllerParameterType.Float);
        hasMoveY = HasParameter(moveYParameter, AnimatorControllerParameterType.Float);
        hasMoving = HasParameter(movingParameter, AnimatorControllerParameterType.Bool);
    }

    private bool HasParameter(string parameterName, AnimatorControllerParameterType expectedType)
    {
        if (animator == null || string.IsNullOrWhiteSpace(parameterName))
        {
            return false;
        }

        foreach (AnimatorControllerParameter parameter in animator.parameters)
        {
            if (parameter.type == expectedType && parameter.name == parameterName)
            {
                return true;
            }
        }

        return false;
    }

    private void OnValidate()
    {
        dampTime = Mathf.Max(0f, dampTime);
        movingThreshold = Mathf.Max(0f, movingThreshold);
    }
}
