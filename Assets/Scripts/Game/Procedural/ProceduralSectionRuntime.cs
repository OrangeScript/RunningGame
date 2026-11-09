using Unity.Netcode;
using UnityEngine;

/// <summary>
/// Single integration point used by SectionManager after a section's root
/// NetworkObject has spawned.
/// </summary>
public static class ProceduralSectionRuntime
{
    public static void ConfigureServer(
        LevelSection section,
        int seed,
        int laneId,
        float difficulty)
    {
        if (section == null || NetworkManager.Singleton == null || !NetworkManager.Singleton.IsServer)
            return;

        LaneScope rootScope = section.GetComponent<LaneScope>();
        if (rootScope == null)
            rootScope = section.gameObject.AddComponent<LaneScope>();
        rootScope.Configure(laneId);

        Gate[] gates = section.GetComponentsInChildren<Gate>(true);
        for (int pairIndex = 0; pairIndex < gates.Length; pairIndex += 2)
        {
            int pairSeed = ProceduralRandom.Derive(seed, pairIndex, laneId);
            GateOfferGenerator.GeneratePair(pairSeed, difficulty, out GateOffer left, out GateOffer right);
            gates[pairIndex].ConfigureServer(left, laneId);

            if (pairIndex + 1 < gates.Length)
                gates[pairIndex + 1].ConfigureServer(right, laneId);
        }

        CombatEncounter encounter = section.GetComponentInChildren<CombatEncounter>(true);
        if (encounter != null)
            encounter.ConfigureServer(ProceduralRandom.Derive(seed, 701, laneId), laneId, difficulty);

        int obstacleSeed = ProceduralRandom.Derive(seed, 1907, laneId);
        if (gates.Length > 0)
        {
            gates[0].BuildObstacleFieldServer(obstacleSeed, laneId, difficulty);
        }
        else if (encounter != null)
        {
            encounter.BuildObstacleFieldServer(obstacleSeed, laneId, difficulty);
        }
        else
        {
            // Run-only sections need SectionManager to mirror BuildClientVisuals
            // through its own ClientRpc. Server collision is still authoritative.
            ProceduralObstacleField.Build(section.transform, obstacleSeed, laneId, difficulty, true);
        }
    }

    public static void BuildClientVisuals(
        LevelSection section,
        int seed,
        int laneId,
        float difficulty)
    {
        if (section == null)
            return;

        int obstacleSeed = ProceduralRandom.Derive(seed, 1907, laneId);
        ProceduralObstacleField.Build(section.transform, obstacleSeed, laneId, difficulty, false);
    }
}
