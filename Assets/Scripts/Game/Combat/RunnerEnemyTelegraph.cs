using UnityEngine;

/// <summary>Ground guides show the locked firing paths, not a homing attack.</summary>
public sealed class RunnerEnemyTelegraph : MonoBehaviour
{
    private static Material material;
    private readonly LineRenderer[] lines = new LineRenderer[3];
    public void Show(bool visible, Vector3 origin, Vector3 direction, EnemyArchetype kind, float remaining)
    {
        int count = kind == EnemyArchetype.Strafer ? 3 : 1;
        for (int i = 0; i < lines.Length; i++)
        {
            bool enabled = visible && i < count;
            if (enabled && lines[i] == null) lines[i] = CreateLine(i);
            if (lines[i] == null) continue;
            lines[i].enabled = enabled;
            if (!enabled) continue;
            Vector3 path = RunnerEnemyAttackPattern.ShotDirection(kind, direction, i);
            Vector3 start = origin; start.y = .055f;
            lines[i].SetPosition(0, start); lines[i].SetPosition(1, start + path * 23);
            float alpha = Mathf.Lerp(.45f, .18f, Mathf.Clamp01(remaining));
            lines[i].startColor = new Color(.83f, .37f, .25f, alpha);
            lines[i].endColor = new Color(.83f, .37f, .25f, alpha * .6f);
        }
    }
    private LineRenderer CreateLine(int index)
    {
        if (material == null) material = new Material(Shader.Find("Sprites/Default"));
        var root = new GameObject("LockedShotGuide_" + index); root.transform.SetParent(transform, false);
        var line = root.AddComponent<LineRenderer>(); line.sharedMaterial = material;
        line.positionCount = 2; line.startWidth = .07f; line.endWidth = .035f;
        line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; line.receiveShadows = false;
        return line;
    }
}
