using System.Collections.Generic;
using UnityEngine;

namespace Game.Procederal.Api
{
    /// Drives IMechanic.Tick for a registered subtree.
    /// ProcederalItemGenerator attaches this to the root it creates and calls RegisterTree.
    [DisallowMultipleComponent]
    public class MechanicRunner : MonoBehaviour
    {
        [Header("Debug")]
        public bool debugLogs = false;

        private readonly List<IMechanic> _mechanics = new();

        // Track registered components to avoid duplicates across incremental registrations
        private readonly HashSet<UnityEngine.Object> _registered = new();

        /// Register all IMechanic implementations under the given root (inclusive) for ticking.
        public void RegisterTree(Transform root)
        {
            if (root == null)
                root = transform;

            var behaviours = root.GetComponentsInChildren<MonoBehaviour>(true);
            foreach (var b in behaviours)
            {
                if (b is IMechanic mech)
                {
                    var uo = b as UnityEngine.Object;
                    if (uo != null && !_registered.Contains(uo))
                    {
                        _mechanics.Add(mech);
                        _registered.Add(uo);
                        if (debugLogs)
                            Debug.Log(
                                $"[MechanicRunner] Registered {b.GetType().Name} on {b.gameObject.name}",
                                this
                            );
                    }
                }
            }
        }

        private void Update()
        {
            float dt = Time.deltaTime;
            for (int i = 0; i < _mechanics.Count; )
            {
                // UnityEngine.Object null semantics: cast to Object to detect destroyed components
                var unityObj = _mechanics[i] as UnityEngine.Object;
                if (unityObj == null)
                {
                    // Component has been destroyed; remove from list
                    var removed = _mechanics[i];
                    _mechanics.RemoveAt(i);
                    // Best-effort remove from registry as well
                    if (removed is MonoBehaviour mb)
                    {
                        var uo = mb as UnityEngine.Object;
                        if (uo != null)
                            _registered.Remove(uo);
                    }
                    continue;
                }
                _mechanics[i].Tick(dt);
                i++;
            }
        }
    }
}
