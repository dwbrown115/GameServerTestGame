using System.Collections.Generic;
using UnityEngine;

namespace Game.Procederal.Api
{
    /// <summary>
    /// Central helper to evenly distribute OrbitMechanic angles among sibling children sharing the same parent.
    /// Call after adding or removing an orbiting projectile. Uses each OrbitMechanic's current radius/ angular speed.
    /// </summary>
    public static class OrbitDistribution
    {
        public static void Redistribute(Transform parent)
        {
            if (parent == null)
                return;
            var orbits = new List<Mechanics.Neuteral.OrbitMechanic>();
            foreach (Transform child in parent)
            {
                if (child == null)
                    continue;
                var om = child.GetComponent<Mechanics.Neuteral.OrbitMechanic>();
                if (om != null)
                    orbits.Add(om);
            }
            int n = orbits.Count;
            if (n <= 1)
                return; // nothing to space or only one
            // Optionally preserve the first one's current angle as base; simpler: start at 0
            float baseAngle = 0f;
            for (int i = 0; i < n; i++)
            {
                float angle = baseAngle + (360f * i / n);
                orbits[i].SetAngleDeg(angle, repositionNow: true);
            }
        }
    }
}
