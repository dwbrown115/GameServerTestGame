using System.Collections.Generic;
using UnityEngine;

namespace Game.Procederal.Api
{
    /// <summary>
    /// Generic spawn helper utilities. Consolidates repeated static spawn loops so build strategies
    /// can focus on behavior resolution instead of object boilerplate.
    /// </summary>
    public static class SpawnHelpers
    {
        /// <summary>
        /// Spawns a fixed number of child GameObjects under <paramref name="parent"/> each with the specified
        /// primary mechanic (payloadMechanicName). Applies additional payload settings, optional sequence index
        /// tagging, and initializes mechanics through the generator.
        /// </summary>
        /// <param name="gen">Central item generator (used for AddMechanicByName & InitializeMechanics).</param>
        /// <param name="parent">Transform to parent children under.</param>
        /// <param name="payloadMechanicName">Primary mechanic name to attach to each child.</param>
        /// <param name="count">Number of children (clamped to >=1).</param>
        /// <param name="basePayload">Optional base payload settings (will be copied per child).</param>
        /// <param name="tagSequenceIndex">When true adds SequenceIndexTag with index/total to each child.</param>
        /// <param name="injectDirectionFromResolverForProjectile">If true and payloadMechanicName == Projectile, ensures directionFromResolver placeholder is present (removes literal direction if found).</param>
        /// <param name="ownerOverride">Optional owner transform passed to InitializeMechanics; falls back to gen.owner.</param>
        /// <param name="targetOverride">Optional target transform passed to InitializeMechanics; falls back to gen.target.</param>
        public static List<GameObject> SpawnNumberOfChildren(
            ProcederalItemGenerator gen,
            Transform parent,
            string payloadMechanicName,
            int count,
            IEnumerable<(string key, object val)> basePayload = null,
            bool tagSequenceIndex = false,
            bool injectDirectionFromResolverForProjectile = false,
            Transform ownerOverride = null,
            Transform targetOverride = null
        )
        {
            var result = new List<GameObject>();
            if (gen == null || parent == null || string.IsNullOrWhiteSpace(payloadMechanicName))
                return result;
            count = Mathf.Max(1, count);

            // Pre-build a list so we can clone quickly per child
            var payloadList = new List<(string key, object val)>();
            if (basePayload != null)
                payloadList.AddRange(basePayload);

            bool isProjectile = string.Equals(
                payloadMechanicName,
                "Projectile",
                System.StringComparison.OrdinalIgnoreCase
            );
            if (injectDirectionFromResolverForProjectile && isProjectile)
            {
                // Remove any existing 'direction' literal so mechanic resolves dynamically
                int idx = payloadList.FindIndex(kv =>
                    string.Equals(kv.key, "direction", System.StringComparison.OrdinalIgnoreCase)
                );
                if (idx >= 0)
                    payloadList.RemoveAt(idx);
                // Avoid duplicate resolver key
                if (
                    !payloadList.Exists(kv =>
                        string.Equals(
                            kv.key,
                            "directionFromResolver",
                            System.StringComparison.OrdinalIgnoreCase
                        )
                    )
                )
                {
                    payloadList.Add(("directionFromResolver", "vector2"));
                }
            }

            for (int i = 0; i < count; i++)
            {
                var child = new GameObject($"{payloadMechanicName}_{i}");
                child.transform.SetParent(parent, false);
                child.transform.localPosition = Vector3.zero;

                // Copy payload per child to avoid reference issues if later mutated
                var perChild = new List<(string key, object val)>(payloadList);
                gen.AddMechanicByName(child, payloadMechanicName, perChild.ToArray());
                gen.InitializeMechanics(
                    child,
                    ownerOverride != null ? ownerOverride : gen.owner,
                    targetOverride != null ? targetOverride : gen.target
                );

                if (tagSequenceIndex)
                {
                    var tag = child.AddComponent<SequenceIndexTag>();
                    tag.index = i;
                    tag.total = count;
                }

                result.Add(child);
            }

            return result;
        }
    }
}
