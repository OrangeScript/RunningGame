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
            var director=GameManager.instance!=null?GameManager.instance.GetComponent<RunLootDirector>():null;
            var random=new ProceduralRandom(ProceduralRandom.Derive(pairSeed,991));
            bool addItem=(director!=null&&!director.HasGateOffer(laneId))||random.Chance(.72f);
            int power=director!=null?director.LaneFirepower(laneId):20;
            int leftPower=Gate.PreviewPower(left,power),rightPower=Gate.PreviewPower(right,power);
            int rewardSide=leftPower==rightPower?random.Range(0,2):leftPower<rightPower?0:1;
            if(left.Operation==GateOperation.Subtract||left.Operation==GateOperation.Divide){addItem=true;rewardSide=0;}
            if(right.Operation==GateOperation.Subtract||right.Operation==GateOperation.Divide){addItem=true;rewardSide=1;}
            var item=addItem?director?.ChooseGateItem(laneId,pairSeed,difficulty>=1.6f?ItemRarity.Rare:ItemRarity.Uncommon):null;
            gates[pairIndex].ConfigureServer(left, laneId,rewardSide==0&&item!=null?item.NetworkId:0);

            if (pairIndex + 1 < gates.Length)
                gates[pairIndex + 1].ConfigureServer(right, laneId,rewardSide==1&&item!=null?item.NetworkId:0);
        }

        CombatEncounter encounter = section.GetComponentInChildren<CombatEncounter>(true);

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
        if(encounter!=null)
            encounter.ConfigureServer(ProceduralRandom.Derive(seed,701,laneId),laneId,difficulty);
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
