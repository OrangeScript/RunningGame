using UnityEngine;

/// <summary>Server-side spatial scope that prevents cross-lane targeting.</summary>
public sealed class LaneScope : MonoBehaviour
{
    [SerializeField] private int laneId = -1;

    public int LaneId => laneId;

    public void Configure(int value)
    {
        laneId = value;
    }

    public static int Resolve(Component component)
    {
        if (component == null)
            return -1;

        LaneScope scope = component.GetComponentInParent<LaneScope>();
        if (scope != null)
            return scope.LaneId;

        RunnerController runner = component.GetComponentInParent<RunnerController>();
        return runner != null ? runner.LaneId : -1;
    }
}
