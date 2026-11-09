using System;
using UnityEngine;

namespace Running.Game.Run.Generation
{
    [CreateAssetMenu(
        fileName = "SectionDefinition",
        menuName = "Running/Run/Section Definition")]
    public sealed class SectionDefinition : ScriptableObject
    {
        [Tooltip("Stable catalog key used by plans and save/replay data. Asset name is used when empty.")]
        [SerializeField]
        private string definitionId;

        [SerializeField]
        private global::SectionType sectionType;

        [SerializeField]
        private global::LevelSection prefab;

        [Min(0f)]
        [SerializeField]
        private float baseWeight = 1f;

        public string DefinitionId =>
            string.IsNullOrWhiteSpace(definitionId) ? name : definitionId.Trim();

        public global::SectionType Type => sectionType;
        public global::LevelSection Prefab => prefab;
        public float BaseWeight => Mathf.Max(0f, baseWeight);

        public bool IsPrefabTypeConsistent =>
            prefab == null || prefab.Type == sectionType;

#if UNITY_EDITOR
        private void OnValidate()
        {
            baseWeight = Mathf.Max(0f, baseWeight);

            // A LevelSection prefab already owns the authoritative type. Keeping
            // the definition aligned prevents a plan from lying about its prefab.
            if (prefab != null)
            {
                sectionType = prefab.Type;
            }
        }
#endif
    }

    [Serializable]
    public sealed class WeightedSectionEntry
    {
        [SerializeField]
        private bool enabled = true;

        [SerializeField]
        private SectionDefinition definition;

        [Min(0f)]
        [SerializeField]
        private float weightMultiplier = 1f;

        public bool Enabled => enabled;
        public SectionDefinition Definition => definition;
        public float WeightMultiplier => Mathf.Max(0f, weightMultiplier);
    }

    /// <summary>
    /// Validated, immutable input consumed by the deterministic planner.
    /// </summary>
    public sealed class SectionCandidate
    {
        public SectionCandidate(
            string definitionId,
            global::SectionType type,
            global::LevelSection prefab,
            float weight)
        {
            DefinitionId = definitionId ?? string.Empty;
            Type = type;
            Prefab = prefab;
            Weight = Math.Max(0f, weight);
        }

        public string DefinitionId { get; }
        public global::SectionType Type { get; }
        public global::LevelSection Prefab { get; }
        public float Weight { get; }
    }
}
