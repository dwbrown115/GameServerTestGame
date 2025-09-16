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

        /// Register all IMechanic implementations under the given root (inclusive) for ticking.
        public void RegisterTree(Transform root)
        {
            _mechanics.Clear();
            if (root == null)
                root = transform;

            var behaviours = root.GetComponentsInChildren<MonoBehaviour>(true);
            foreach (var b in behaviours)
            {
                if (b is IMechanic mech)
                {
                    _mechanics.Add(mech);
                    if (debugLogs)
                        Debug.Log(
                            $"[MechanicRunner] Registered {b.GetType().Name} on {b.gameObject.name}",
                            this
                        );
                }
            }
        }

        private void Update()
        {
            float dt = Time.deltaTime;
            for (int i = 0; i < _mechanics.Count; i++)
            {
                _mechanics[i].Tick(dt);
            }
        }
    }
}
