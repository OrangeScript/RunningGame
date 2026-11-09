using UnityEngine;

/// <summary>Stable lane framing: lateral input moves the avatar, not the road.</summary>
public class CameraFollow : MonoBehaviour
{
    [SerializeField] private Transform target;
    private RunnerController runner;
    private Vector3 velocity;
    internal void SetTarget(Transform newTarget)
    {
        target = newTarget;
        runner = target != null ? target.GetComponent<RunnerController>() : null;
        Follow(true);
    }
    private void LateUpdate() => Follow(false);
    private void Follow(bool snap)
    {
        if (target == null) return;
        Vector3 center = new Vector3(runner != null ? runner.LaneCenterX : target.position.x,0,target.position.z);
        Vector3 desired = center + new Vector3(0,18,-14);
        bool teleported = (transform.position-desired).sqrMagnitude > 900;
        transform.position = snap || teleported ? desired : Vector3.SmoothDamp(transform.position,desired,ref velocity,.14f);
        transform.rotation = Quaternion.LookRotation(center + Vector3.forward*7 - transform.position);
        var camera = GetComponent<Camera>();
        if (camera != null)
        {
            camera.fieldOfView = 52;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(.83f,.86f,.82f);
        }
    }
}
