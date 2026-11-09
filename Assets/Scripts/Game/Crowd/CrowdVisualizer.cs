using UnityEngine;

public enum CrowdVisualMode { Normal, Giant }

/// <summary>
/// Compatibility facade for existing items. One visible body shares the root
/// CharacterController footprint. Power and heavy-weapon items never clone or
/// enlarge that body; their identity is expressed by the weapon instead.
/// </summary>
public class CrowdVisualizer : MonoBehaviour
{
    [SerializeField] private CrowdManager crowdManager;
    [SerializeField] private Transform crowdRoot;
    [SerializeField] private GameObject unitPrefab;
    private Transform avatar;
    public CrowdVisualMode Mode { get; private set; }
    public int VisualUnitCount => avatar != null ? 1 : 0;

    private void Awake()
    {
        if (crowdRoot == null) crowdRoot = transform.Find("CrowdRoot");
        if (crowdRoot != null) crowdRoot.gameObject.SetActive(false);
        avatar = transform.Find("Capsule");
        if (avatar == null)
        {
            var body = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            body.name = "Capsule"; avatar = body.transform;
            avatar.SetParent(transform,false);
        }
        avatar.gameObject.SetActive(true);
        avatar.localPosition = Vector3.up;
        avatar.localScale = new Vector3(.8f,1,.8f);
        foreach (var collider in avatar.GetComponentsInChildren<Collider>()) collider.enabled = false;
        var renderer = avatar.GetComponent<Renderer>();
        if (renderer != null)
        {
            var properties = new MaterialPropertyBlock();
            properties.SetColor("_BaseColor", MinimalUI.Accent); properties.SetColor("_Color",MinimalUI.Accent);
            renderer.SetPropertyBlock(properties);
        }
    }
    public void SetMode(CrowdVisualMode mode) => Mode = mode;
    public void SetGiantMode(bool enabled) => SetMode(enabled ? CrowdVisualMode.Giant : CrowdVisualMode.Normal);
    public Transform GetUnitTransform(int index) => avatar;
    public void RefreshVisuals() { if (avatar != null) avatar.gameObject.SetActive(true); }
}
