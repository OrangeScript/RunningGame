using System;
using System.Collections;
using Unity.Netcode;
using UnityEngine;

namespace Running.Game.RunMap
{
    /// <summary>
    /// Small scene-facing facade that turns the map generator/coordinator into a
    /// usable run flow. Add it (and RunMapNetworkCoordinator) to the same
    /// scene NetworkObject as the run director, or call Ensure during Awake.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class RunMapRuntimeFlow : MonoBehaviour
    {
        [SerializeField] private RunMapNetworkCoordinator coordinator;
        [SerializeField] private bool initializeAutomatically = true;
        [SerializeField] private int runSeed = 20261108;
        [SerializeField, Min(1f)] private float voteDurationSeconds = 90f;
        [SerializeField] private RunMapGenerationSettings generationSettings =
            new RunMapGenerationSettings();

        private Coroutine startupRoutine;

        /// <summary>Raised on every peer after the authoritative vote selects a node.</summary>
        public event Action<RunMapNodeSnapshot> NodeResolved;

        public RunMapNetworkCoordinator Coordinator => coordinator;
        public RunMapNodeSnapshot CurrentNode { get; private set; }

        /// <summary>
        /// Safe one-line setup when called from a scene NetworkBehaviour.Awake.
        /// Calling it on every peer before NGO spawns the scene object preserves
        /// the same NetworkBehaviour layout on host and clients.
        /// </summary>
        public static RunMapRuntimeFlow Ensure(GameObject networkRoot)
        {
            if (networkRoot == null)
                throw new ArgumentNullException(nameof(networkRoot));

            RunMapNetworkCoordinator mapCoordinator =
                networkRoot.GetComponent<RunMapNetworkCoordinator>();
            if (mapCoordinator == null)
                mapCoordinator = networkRoot.AddComponent<RunMapNetworkCoordinator>();

            RunMapRuntimeFlow flow = networkRoot.GetComponent<RunMapRuntimeFlow>();
            if (flow == null)
                flow = networkRoot.AddComponent<RunMapRuntimeFlow>();

            flow.coordinator = mapCoordinator;

            RunMapRuntimeUI ui = networkRoot.GetComponent<RunMapRuntimeUI>();
            if (ui == null)
                ui = networkRoot.AddComponent<RunMapRuntimeUI>();
            ui.SetCoordinator(mapCoordinator);

            return flow;
        }

        private void Awake()
        {
            if (coordinator == null)
                coordinator = GetComponent<RunMapNetworkCoordinator>();
        }

        private void OnEnable()
        {
            Bind();
            if (initializeAutomatically && startupRoutine == null)
                startupRoutine = StartCoroutine(InitializeWhenNetworkReady());
        }

        private void OnDisable()
        {
            if (startupRoutine != null)
            {
                StopCoroutine(startupRoutine);
                startupRoutine = null;
            }

            Unbind();
        }

        public bool StartRun(int seed)
        {
            if (coordinator == null || !coordinator.IsServer || !coordinator.IsSpawned)
                return false;

            runSeed = seed;
            bool initialized = coordinator.InitializeRun(runSeed, generationSettings);
            return initialized && coordinator.BeginVoteForReachableNodes(voteDurationSeconds);
        }

        /// <summary>
        /// Call after the selected combat/event/reward node has completed. The
        /// server then opens the next public branch vote.
        /// </summary>
        public bool CompleteCurrentNodeAndOpenVote()
        {
            return coordinator != null && coordinator.IsServer &&
                   coordinator.BeginVoteForReachableNodes(voteDurationSeconds);
        }

        private IEnumerator InitializeWhenNetworkReady()
        {
            while (coordinator == null || !coordinator.IsSpawned)
                yield return null;

            startupRoutine = null;
            if (coordinator.IsServer && coordinator.MapSnapshot == null)
                StartRun(runSeed);
        }

        private void Bind()
        {
            if (coordinator == null)
                return;

            coordinator.CurrentNodeChanged -= HandleNodeResolved;
            coordinator.CurrentNodeChanged += HandleNodeResolved;
        }

        private void Unbind()
        {
            if (coordinator != null)
                coordinator.CurrentNodeChanged -= HandleNodeResolved;
        }

        private void HandleNodeResolved(RunMapNodeSnapshot node)
        {
            CurrentNode = node?.Clone();
            if (CurrentNode != null)
                NodeResolved?.Invoke(CurrentNode.Clone());
        }
    }
}
