using TMPro;
using UnityEngine;

/// <summary>Shared lane dimensions and scene-local art, identical on every peer.</summary>
public static class RunSectionPresentation
{
    public const float RoadWidth = 16f;
    public const float MovementHalfWidth = 7.25f;
    public const float LaneSpacing = 22f;
    public static void Build(LevelSection section)
    {
        if (section.transform.Find("LaneDetails") != null) return;
        foreach (var filter in section.GetComponentsInChildren<MeshFilter>(true))
        {
            if (filter.sharedMesh == null || filter.GetComponentInParent<Gate>() != null) continue;
            if (filter.sharedMesh.bounds.size.y < .05f && filter.sharedMesh.bounds.size.x > 1f)
            {
                var scale = filter.transform.localScale;
                scale.x = RoadWidth / filter.sharedMesh.bounds.size.x;
                filter.transform.localScale = scale;
                Paint(filter.GetComponent<Renderer>(), new Color(.68f,.72f,.68f));
            }
            // Legacy sections contain unseeded placeholder cubes alongside
            // the procedural field. Disable every authored copy, including
            // numbered duplicates, together with its collider.
            else if (filter.name.StartsWith("Cube",System.StringComparison.Ordinal))
                filter.gameObject.SetActive(false);
        }

        var gates = section.GetComponentsInChildren<Gate>(true);
        for (int i = 0; i < gates.Length; i++)
        {
            Gate gate = gates[i];
            gate.transform.localPosition = new Vector3(i % 2 == 0 ? -4 : 4,0,13);
            var trigger = gate.GetComponent<BoxCollider>();
            if (trigger != null) { trigger.size = new Vector3(7.7f,3,1); trigger.center = new Vector3(0,1.5f,0); }
            foreach (Transform child in gate.transform)
            {
                if (child.GetComponent<TMP_Text>() != null) continue;
                var position = child.localPosition; position.x *= 1.65f; child.localPosition = position;
                var scale = child.localScale;
                if (scale.x > 1) { scale.x *= 1.65f; child.localScale = scale; }
                // Gate frames are visual markers. The logical gate trigger is
                // the only gate collision; posts cannot snag the player.
                foreach (var col in child.GetComponentsInChildren<Collider>()) col.enabled = false;
            }
        }
        foreach (var encounter in section.GetComponentsInChildren<CombatEncounter>(true))
        {
            var trigger = encounter.GetComponent<BoxCollider>();
            if (trigger != null) { var size = trigger.size; size.x = RoadWidth; size.y = 4; trigger.size = size; trigger.center = Vector3.up; }
        }

        var details = new GameObject("LaneDetails").transform;
        details.SetParent(section.transform,false);
        for (int side = -1; side <= 1; side += 2)
        {
            Detail(details,"Edge",new Vector3(side*7.9f,.025f,10),new Vector3(.09f,.05f,20),new Color(.88f,.89f,.83f));
            Detail(details,"Shoulder",new Vector3(side*8.25f,-.08f,10),new Vector3(.4f,.12f,20),new Color(.43f,.49f,.44f));
        }
    }
    private static void Detail(Transform parent,string name,Vector3 position,Vector3 scale,Color color)
    {
        var part = GameObject.CreatePrimitive(PrimitiveType.Cube);
        part.name = name; part.transform.SetParent(parent,false);
        part.transform.localPosition = position; part.transform.localScale = scale;
        part.GetComponent<Collider>().enabled = false;
        Paint(part.GetComponent<Renderer>(),color);
    }
    public static void Paint(Renderer renderer,Color color)
    {
        if (renderer == null) return;
        var block = new MaterialPropertyBlock();
        renderer.GetPropertyBlock(block); block.SetColor("_BaseColor",color); block.SetColor("_Color",color);
        block.SetTexture("_BaseMap",Texture2D.whiteTexture);
        block.SetTexture("_MainTex",Texture2D.whiteTexture);
        renderer.SetPropertyBlock(block);
    }
}
