using System.Collections.Generic;
using UnityEngine;

namespace Game.Procederal.Api
{
    /// <summary>
    /// Helper to evenly distribute orbiting children around their parent. Retained while OrbitCircular refactor is in progress.
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

                var mechanic = child.GetComponent<Mechanics.Neuteral.OrbitMechanic>();
                if (mechanic != null)
                    orbits.Add(mechanic);
            }

            int count = orbits.Count;
            if (count <= 1)
                return;

            float baseWorldAngle = 0f;
            bool baseInitialized = false;
            for (int i = 0; i < count; i++)
            {
                var orbit = orbits[i];
                if (orbit == null)
                    continue;

                if (!baseInitialized)
                {
                    baseWorldAngle = orbit.startAngleDeg + orbit.PathRotationDeg;
                    baseInitialized = true;
                }

                float worldAngle = baseWorldAngle + (360f * i) / count;
                float internalAngle = worldAngle - orbit.PathRotationDeg;
                orbit.SetAngleDeg(internalAngle, repositionNow: true);
            }
        }
    }
}
