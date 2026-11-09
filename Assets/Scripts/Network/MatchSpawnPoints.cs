using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class MatchSpawnPoints : MonoBehaviour
{
    [SerializeField]
    private Transform[] spawnPoints;

    [Header("Generated fallback")]
    [SerializeField, Min(1)] private int generatedCapacity = 4;
    [SerializeField, Min(0.1f)] private float generatedSpacing = 12f;
    [SerializeField] private Vector3 generatedAxis = Vector3.right;

    public int Count => Mathf.Max(spawnPoints?.Length ?? 0, generatedCapacity);

    public bool TryGetSpawn(
        int index,
        out Vector3 position,
        out Quaternion rotation)
    {
        position = Vector3.zero;
        rotation = Quaternion.identity;

        if (index < 0 || index >= Count)
        {
            return false;
        }

        if (spawnPoints != null &&
            index < spawnPoints.Length &&
            spawnPoints[index] != null)
        {
            position = spawnPoints[index].position;
            rotation = spawnPoints[index].rotation;
            return true;
        }

        Transform anchor = FindAnchor();
        Vector3 axis = generatedAxis.sqrMagnitude > 0.001f
            ? generatedAxis.normalized
            : Vector3.right;

        position = anchor.position + axis * (generatedSpacing * index);
        rotation = anchor.rotation;

        return true;
    }

    private Transform FindAnchor()
    {
        if (spawnPoints != null)
        {
            for (int i = 0; i < spawnPoints.Length; i++)
            {
                if (spawnPoints[i] != null)
                    return spawnPoints[i];
            }
        }

        return transform;
    }
}
