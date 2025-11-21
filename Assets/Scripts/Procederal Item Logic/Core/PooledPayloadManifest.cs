using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Game.Procederal.Core
{
    /// <summary>
    /// Lightweight manifest attached to pooled roots so we can reuse full hierarchies
    /// without destroying children/components. Captures a fingerprint and a cached list
    /// of resettable components for fast reset on return to pool.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PooledPayloadManifest : MonoBehaviour
    {
        // Deterministic fingerprint (pool key) describing what this hierarchy contains.
        public string Fingerprint { get; private set; } = string.Empty;

        private List<IPooledPayloadResettable> _resettableComponents =
            new List<IPooledPayloadResettable>();

        /// Capture current hierarchy as a pooled payload for the provided fingerprint.
        public void Capture(string fingerprint)
        {
            Fingerprint = string.IsNullOrWhiteSpace(fingerprint) ? string.Empty : fingerprint;
            _resettableComponents.Clear();
            var found = GetComponentsInChildren<IPooledPayloadResettable>(includeInactive: true);
            if (found != null && found.Length > 0)
                _resettableComponents.AddRange(found);
        }

        /// Returns true if this manifest matches the requested fingerprint.
        public bool Matches(string fingerprint)
        {
            if (string.IsNullOrWhiteSpace(Fingerprint) && string.IsNullOrWhiteSpace(fingerprint))
                return true;
            return string.Equals(Fingerprint ?? string.Empty, fingerprint ?? string.Empty);
        }

        /// Reset the cached components to a neutral state prior to parking in the pool.
        public void ResetForPool()
        {
            for (int i = 0; i < _resettableComponents.Count; i++)
            {
                var c = _resettableComponents[i];
                if (c == null)
                    continue;
                c.ResetForPool();
            }
        }

        /// Re-activate the payload (basic reparenting/name/enable).
        public void Reactivate(Transform parent, string displayName)
        {
            if (parent != null)
                transform.SetParent(parent, worldPositionStays: false);
            else
                transform.SetParent(null, false);
            if (!string.IsNullOrWhiteSpace(displayName))
                gameObject.name = displayName;
            // leave local transform as-is (builder controls positioning)
            gameObject.SetActive(true);
        }
    }
}
