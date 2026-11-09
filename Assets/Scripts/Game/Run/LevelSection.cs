using UnityEngine;

public enum SectionType
{
    Run,
    Gate,
    Combat,
    Elite,
    Boss,
    Finish
}

/// <summary>
/// A spawnable piece of a running lane. Authoring data lives on the prefab;
/// deterministic runtime metadata is assigned by <see cref="SectionManager"/>.
/// </summary>
public class LevelSection : MonoBehaviour
{
    [Header("Section")]
    [SerializeField]
    private SectionType sectionType;

    [SerializeField]
    private Transform exitPoint;
    public SectionType Type => sectionType;
    public Transform ExitPoint => exitPoint;

    public bool HasRuntimePlan { get; private set; }
    public int LaneIndex { get; private set; } = -1;
    public int SequenceIndex { get; private set; } = -1;
    public int ContentSeed { get; private set; }
    public string DefinitionId { get; private set; } = string.Empty;
    public Vector3 PlannedStartPosition { get; private set; }
    public Quaternion PlannedStartRotation { get; private set; } = Quaternion.identity;

    private void Awake()
    {
        if (Application.isPlaying) RunSectionPresentation.Build(this);
    }

    internal void ConfigureRuntime(
        int laneIndex,
        int sequenceIndex,
        int contentSeed,
        string definitionId,
        Vector3 startPosition,
        Quaternion startRotation)
    {
        HasRuntimePlan = true;
        LaneIndex = laneIndex;
        SequenceIndex = sequenceIndex;
        ContentSeed = contentSeed;
        DefinitionId = definitionId ?? string.Empty;
        PlannedStartPosition = startPosition;
        PlannedStartRotation = startRotation;
    }

    public bool TryGetExitPose(out Vector3 position, out Quaternion rotation)
    {
        if (exitPoint == null)
        {
            position = transform.position;
            rotation = transform.rotation;
            return false;
        }

        position = exitPoint.position;
        rotation = exitPoint.rotation;
        return true;
    }
}
