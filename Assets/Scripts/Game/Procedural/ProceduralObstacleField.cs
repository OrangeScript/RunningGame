using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

public enum ProceduralObstacleKind : byte
{
    Block,
    MovingBlock,
    Spinner
}

/// <summary>
/// Builds deterministic obstacle geometry from a seed. Call this once on the
/// server with physics enabled and once on every remote client for visuals.
/// </summary>
public static class ProceduralObstacleField
{
    private const string ContainerName = "__ProceduralObstacles";

    public static void Build(
        Transform sectionRoot,
        int seed,
        int laneId,
        float difficulty,
        bool serverPhysics,
        int minimumCount = 1,
        int maximumCount = 3)
    {
        if (sectionRoot == null)
            return;

        Clear(sectionRoot);

        var containerObject = new GameObject(ContainerName);
        Transform container = containerObject.transform;
        container.SetParent(sectionRoot, false);

        var random = new ProceduralRandom(seed);
        int difficultyBonus = Mathf.FloorToInt(Mathf.Max(0f, difficulty - 1f));
        int count = random.Range(
            Mathf.Max(0, minimumCount),
            Mathf.Max(minimumCount + 1, maximumCount + difficultyBonus + 1));

        var occupiedZ = new List<float>(count);
        for (int index = 0; index < count; index++)
        {
            float z = FindSeparatedZ(ref random, occupiedZ, 4f, 17f, 3f);
            LevelSection section = sectionRoot.GetComponent<LevelSection>();
            if (section != null && section.Type == SectionType.Gate && z > 9f)
                z = 4f + index * 2.1f;
            occupiedZ.Add(z);

            ProceduralObstacleKind kind = (ProceduralObstacleKind)random.Range(0, 3);
            CreateObstacle(
                container,
                ref random,
                kind,
                laneId,
                difficulty,
                serverPhysics,
                z,
                index);
        }
    }

    public static void Clear(Transform sectionRoot)
    {
        if (sectionRoot == null)
            return;

        Transform existing = sectionRoot.Find(ContainerName);
        if (existing == null)
            return;

        if (Application.isPlaying)
            Object.Destroy(existing.gameObject);
        else
            Object.DestroyImmediate(existing.gameObject);
    }

    private static void CreateObstacle(
        Transform parent,
        ref ProceduralRandom random,
        ProceduralObstacleKind kind,
        int laneId,
        float difficulty,
        bool serverPhysics,
        float z,
        int index)
    {
        GameObject obstacle = GameObject.CreatePrimitive(PrimitiveType.Cube);
        obstacle.name = $"Obstacle_{index:00}_{kind}";
        obstacle.transform.SetParent(parent, false);

        float x = random.Range(-5.3f, 5.3f);
        Vector3 scale;
        switch (kind)
        {
            case ProceduralObstacleKind.MovingBlock:
                scale = new Vector3(random.Range(1.1f, 2.1f), random.Range(1.1f, 2f), 1.1f);
                break;
            case ProceduralObstacleKind.Spinner:
                x = 0f;
                scale = new Vector3(random.Range(4.5f, 7f), 0.45f, 0.5f);
                break;
            default:
                scale = new Vector3(random.Range(1f, 2.5f), random.Range(1.2f, 2.8f), random.Range(0.8f, 1.6f));
                break;
        }

        obstacle.transform.localPosition = new Vector3(x, scale.y * 0.5f, z);
        obstacle.transform.localScale = scale;

        Renderer renderer = obstacle.GetComponent<Renderer>();
        if (renderer != null)
        {
            // A stable colour language makes the random field readable at
            // running speed: red blocks stop you, cyan blocks sweep sideways,
            // and magenta bars spin and damage the crowd.
            renderer.material.color = kind switch
            {
                ProceduralObstacleKind.MovingBlock => new Color(.55f,.62f,.58f),
                ProceduralObstacleKind.Spinner => new Color(.69f,.50f,.36f),
                _ => new Color(.64f,.43f,.35f)
            };
        }

        Collider collider = obstacle.GetComponent<Collider>();
        if (collider != null)
        {
            collider.enabled = true;
            collider.isTrigger = kind != ProceduralObstacleKind.Block;
        }

        if (kind != ProceduralObstacleKind.Block)
        {
            ProceduralObstacleMotion motion = obstacle.AddComponent<ProceduralObstacleMotion>();
            motion.Configure(
                kind,
                random.Range(0.5f, 2.5f),
                random.Range(1.0f, 1.8f),
                random.Range(55f, 115f));

            if (serverPhysics)
            {
                ProceduralObstacleHazard hazard = obstacle.AddComponent<ProceduralObstacleHazard>();
                hazard.Configure(laneId, Mathf.Max(1, Mathf.CeilToInt(difficulty)));
            }
        }
    }

    private static float FindSeparatedZ(
        ref ProceduralRandom random,
        List<float> occupied,
        float minimum,
        float maximum,
        float separation)
    {
        for (int attempt = 0; attempt < 12; attempt++)
        {
            float candidate = random.Range(minimum, maximum);
            bool valid = true;
            for (int i = 0; i < occupied.Count; i++)
            {
                if (Mathf.Abs(candidate - occupied[i]) < separation)
                {
                    valid = false;
                    break;
                }
            }

            if (valid)
                return candidate;
        }

        return minimum + occupied.Count * separation;
    }
}

public sealed class ProceduralObstacleMotion : MonoBehaviour
{
    private ProceduralObstacleKind kind;
    private float phase;
    private float amplitude;
    private float angularSpeed;
    private Vector3 origin;

    public void Configure(
        ProceduralObstacleKind obstacleKind,
        float phaseOffset,
        float movementAmplitude,
        float rotationSpeed)
    {
        kind = obstacleKind;
        phase = phaseOffset;
        amplitude = movementAmplitude;
        angularSpeed = rotationSpeed;
        origin = transform.localPosition;
    }

    private void Update()
    {
        double synchronizedTime = NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening
            ? NetworkManager.Singleton.ServerTime.Time
            : Time.timeAsDouble;
        float time = (float)synchronizedTime + phase;

        if (kind == ProceduralObstacleKind.MovingBlock)
        {
            Vector3 position = origin;
            position.x = Mathf.Clamp(origin.x + Mathf.Sin(time * 1.25f) * amplitude, -6.2f, 6.2f);
            transform.localPosition = position;
        }
        else if (kind == ProceduralObstacleKind.Spinner)
        {
            transform.localRotation = Quaternion.Euler(0f, time * angularSpeed, 0f);
        }
    }
}

public sealed class ProceduralObstacleHazard : MonoBehaviour
{
    private int laneId;
    private int damage;
    private readonly HashSet<ulong> recentlyHit = new HashSet<ulong>();

    public void Configure(int configuredLaneId, int populationDamage)
    {
        laneId = configuredLaneId;
        damage = Mathf.Max(1, populationDamage);
    }

    private void OnTriggerEnter(Collider other)
    {
        NetworkManager manager = NetworkManager.Singleton;
        if (manager == null || !manager.IsServer)
            return;

        RunnerController runner = other.GetComponentInParent<RunnerController>();
        CrowdManager crowd = other.GetComponentInParent<CrowdManager>();
        if (runner == null || crowd == null || (laneId >= 0 && runner.LaneId != laneId))
            return;

        if (recentlyHit.Add(crowd.OwnerClientId))
            crowd.DamageHealth(damage * 8);
    }

    private void OnTriggerExit(Collider other)
    {
        CrowdManager crowd = other.GetComponentInParent<CrowdManager>();
        if (crowd != null)
            recentlyHit.Remove(crowd.OwnerClientId);
    }
}
