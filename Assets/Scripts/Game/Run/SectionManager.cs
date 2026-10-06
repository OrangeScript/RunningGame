using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

public class SectionManager : NetworkBehaviour
{
    [Header("Start")]

    [SerializeField]
    private Transform startPoint;


    [Header("Finite Run")]

    [SerializeField]
    private LevelSection[] sectionPrefabs;

    private readonly List<LevelSection>
        spawnedSections =
            new List<LevelSection>();

    public override void OnNetworkSpawn()
    {
        if(!IsServer) return;
        BuildFiniteRun();
    }

    public void BuildFiniteRun()
    {
        ClearSections();


        Vector3 nextPosition =
            startPoint != null
                ? startPoint.position
                : transform.position;


        Quaternion nextRotation =
            startPoint != null
                ? startPoint.rotation
                : transform.rotation;


        foreach (
            LevelSection sectionPrefab
            in sectionPrefabs
        )
        {
            if (sectionPrefab == null)
                continue;


            LevelSection section =
                Instantiate(
                    sectionPrefab,
                    nextPosition,
                    nextRotation
                );
            NetworkObject networkObject = section.GetComponent<NetworkObject>();
            networkObject.Spawn(true);


            spawnedSections.Add(
                section
            );


            if (
                section.ExitPoint ==
                null
            )
            {
                Debug.LogError(
                    $"{section.name} 没有 ExitPoint"
                );

                break;
            }


            nextPosition =
                section.ExitPoint.position;


            nextRotation =
                section.ExitPoint.rotation;
        }
    }


    private void ClearSections()
    {
        foreach (
            LevelSection section
            in spawnedSections
        )
        {
            if (section != null )
            {
                NetworkObject networkObject = section.
                    GetComponent<NetworkObject>();
                networkObject.Despawn(true);
            }
        }


        spawnedSections.Clear();
    }

    // S
}
