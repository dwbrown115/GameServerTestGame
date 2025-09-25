using System.Collections.Generic;
using UnityEngine;

namespace Game.Procederal.Api
{
    /// Manages a set of Whip arcs and can spawn another arc at runtime with priority rules.
    /// Attach to the Whip root created by ProcederalItemGenerator.BuildWhip.
    public class WhipArcSet : MonoBehaviour
    {
        public ProcederalItemGenerator generator;
        public Transform owner;
        public Transform target;
        public bool debugLogs = false;

        // Track existing child whip directions for priority decisions
        private readonly HashSet<string> _dirs = new HashSet<string>();

        private void Awake()
        {
            RefreshDirs();
        }

        public void RefreshDirs()
        {
            _dirs.Clear();
            foreach (var w in GetComponentsInChildren<Mechanics.Neuteral.WhipMechanic>(true))
            {
                if (!string.IsNullOrEmpty(w.direction))
                    _dirs.Add(w.direction.Trim().ToLowerInvariant());
            }
        }

        /// Spawns one more whip child following priority: opposite cardinal of any existing, then up/down, then the opposite of whichever it picks there.
        public void SpawnAnotherWhip()
        {
            if (generator == null)
            {
                if (debugLogs)
                    Debug.Log("[WhipArcSet] No generator set.", this);
                return;
            }
            // Determine candidate directions by priority
            var existing = GetAnyExistingCardinal();
            string primary = Opposite(existing ?? "right");
            string secondaryA = "up";
            string secondaryB = "down";
            // If primary already exists, try secondaries
            string chosen = null;
            if (!_dirs.Contains(primary))
                chosen = primary;
            else if (!_dirs.Contains(secondaryA))
                chosen = secondaryA;
            else if (!_dirs.Contains(secondaryB))
                chosen = secondaryB;
            else
            {
                // As fallback, pick the opposite of any available to keep spacing
                chosen = Opposite(existing ?? "right");
            }
            if (debugLogs)
                Debug.Log($"[WhipArcSet] Spawning extra whip dir={chosen}", this);

            var go = new GameObject("Whip_Extra");
            go.transform.SetParent(transform, false);
            go.transform.localPosition = Vector3.zero;

            // Merge JSON settings and override direction
            var settings = new List<(string key, object val)>();
            var json = ProcederalItemGenerator.LoadAndMergeForExternal("Whip");
            foreach (var kv in json)
                settings.Add((kv.Key, kv.Value));
            settings.Add(("direction", chosen));
            settings.Add(("excludeOwner", true));
            settings.Add(("requireMobTag", true));
            settings.Add(("debugLogs", debugLogs || generator.debugLogs));

            generator.AddMechanicByName(go, "Whip", settings.ToArray());
            generator.InitializeMechanics(go, owner != null ? owner : generator.transform, target);

            _dirs.Add(chosen);
        }

        private string GetAnyExistingCardinal()
        {
            // Preference order: right, left, up, down
            if (_dirs.Contains("right"))
                return "right";
            if (_dirs.Contains("left"))
                return "left";
            if (_dirs.Contains("up"))
                return "up";
            if (_dirs.Contains("down"))
                return "down";
            return null;
        }

        private static string Opposite(string d)
        {
            switch ((d ?? "right").Trim().ToLowerInvariant())
            {
                case "right":
                    return "left";
                case "left":
                    return "right";
                case "up":
                    return "down";
                case "down":
                    return "up";
                default:
                    return "left";
            }
        }
    }
}
