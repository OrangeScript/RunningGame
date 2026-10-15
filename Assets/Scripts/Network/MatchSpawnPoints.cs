using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class MatchSpawnPoints : MonoBehaviour
{
    [SerializeField]
    private Transform[] spawnPoints;

    public int Count => spawnPoints?.Length ?? 0;

    public bool TryGetSpawn(
        int index,
        out Vector3 position,
        out Quaternion rotation)
    {
        position = Vector3.zero;
        rotation = Quaternion.identity;

        if (spawnPoints == null ||
            index < 0 ||
            index >= spawnPoints.Length ||
            spawnPoints[index] == null)
        {
            return false;
        }

        position = spawnPoints[index].position;
        rotation = spawnPoints[index].rotation;

        return true;
    }
}
