using System.Collections.Generic;
using Game.Procederal.Core;
using UnityEngine;

namespace Game.Procederal.Core.Builders.Modifiers
{
    public class OrbitModifierStrategy : IModifierStrategy, IBatchModifierStrategy
    {
        private static readonly List<GameObject> _single = new List<GameObject>(1);

        public Game.Procederal.MechanicKind Kind => Game.Procederal.MechanicKind.Orbit;

        public void Apply(
            Game.Procederal.ProcederalItemGenerator generator,
            GameObject target,
            Game.Procederal.ItemParams parameters
        )
        {
            if (generator == null || target == null)
                return;

            _single.Clear();
            _single.Add(target);
            ApplyToGroup(generator, _single, parameters);
            _single.Clear();
        }

        public void ApplyToGroup(
            Game.Procederal.ProcederalItemGenerator generator,
            List<GameObject> targets,
            Game.Procederal.ItemParams parameters
        )
        {
            if (generator == null || targets == null || targets.Count == 0)
                return;

            var orbitJson =
                generator.LoadAndMergeJsonSettings("Orbit")
                ?? new Dictionary<string, object>(System.StringComparer.OrdinalIgnoreCase);

            var effectiveParams = parameters ?? new Game.Procederal.ItemParams();

            static bool HasAnyKey(Dictionary<string, object> dict, params string[] keys)
            {
                if (dict == null || keys == null)
                    return false;
                foreach (var key in keys)
                {
                    if (string.IsNullOrWhiteSpace(key))
                        continue;
                    if (dict.ContainsKey(key))
                        return true;
                }
                return false;
            }

            float radius = effectiveParams.orbitRadius;
            if (HasAnyKey(orbitJson, "radius"))
            {
                radius = MechanicSettingNormalizer.Radius(orbitJson, "radius", radius);
                radius = Mathf.Max(0f, radius);
            }
            else
            {
                radius = Mathf.Max(0f, radius);
            }

            float baseSpeed =
                effectiveParams.orbitSpeedDeg > 0f ? effectiveParams.orbitSpeedDeg : 90f;
            float angularSpeed = baseSpeed;
            if (HasAnyKey(orbitJson, "speed", "angularSpeedDeg"))
            {
                angularSpeed = MechanicSettingNormalizer.Float(
                    orbitJson,
                    baseSpeed,
                    "speed",
                    "angularSpeedDeg"
                );
                if (Mathf.Approximately(angularSpeed, 0f))
                    angularSpeed = baseSpeed;
            }

            float startAngleBase = effectiveParams.startAngleDeg;
            if (HasAnyKey(orbitJson, "startAngleDeg"))
            {
                startAngleBase = MechanicSettingNormalizer.Float(
                    orbitJson,
                    "startAngleDeg",
                    startAngleBase
                );
            }

            float pathRotationBase = effectiveParams.orbitPathRotationBaseDeg;
            if (
                HasAnyKey(
                    orbitJson,
                    "OrbitPathRotationBaseDeg",
                    "orbitPathRotationBaseDeg",
                    "pathRotationBaseDeg",
                    "PathRotationDeg"
                )
            )
            {
                pathRotationBase = MechanicSettingNormalizer.Float(
                    orbitJson,
                    pathRotationBase,
                    "OrbitPathRotationBaseDeg",
                    "orbitPathRotationBaseDeg",
                    "pathRotationBaseDeg",
                    "PathRotationDeg"
                );
            }

            float pathRotationStep = effectiveParams.orbitPathRotationStepDeg;
            if (
                HasAnyKey(
                    orbitJson,
                    "OrbitPathRotationStepDeg",
                    "orbitPathRotationStepDeg",
                    "pathRotationStepDeg"
                )
            )
            {
                pathRotationStep = MechanicSettingNormalizer.Float(
                    orbitJson,
                    pathRotationStep,
                    "OrbitPathRotationStepDeg",
                    "orbitPathRotationStepDeg",
                    "pathRotationStepDeg"
                );
            }

            string behaviorOverridePath = effectiveParams.childBehavior.orbitPath;
            bool hasBehaviorOverride = !string.IsNullOrWhiteSpace(behaviorOverridePath);
            string desiredPath = effectiveParams.childBehavior.ResolveOrbitPathOrDefault();
            if (!hasBehaviorOverride && HasAnyKey(orbitJson, "pathId", "orbitPath", "OrbitPath"))
            {
                desiredPath = MechanicSettingNormalizer.String(
                    orbitJson,
                    "pathId",
                    MechanicSettingNormalizer.String(
                        orbitJson,
                        "orbitPath",
                        MechanicSettingNormalizer.String(orbitJson, "OrbitPath", desiredPath)
                    )
                );
            }

            effectiveParams.orbitRadius = radius;
            effectiveParams.orbitSpeedDeg = angularSpeed;
            effectiveParams.startAngleDeg = startAngleBase;
            effectiveParams.orbitPathRotationBaseDeg = pathRotationBase;
            effectiveParams.orbitPathRotationStepDeg = pathRotationStep;
            if (!string.IsNullOrWhiteSpace(desiredPath))
                effectiveParams.childBehavior.orbitPath = desiredPath;

            bool hasExplicitDestroy = false;
            bool destroyOnHit = false;
            if (orbitJson != null && orbitJson.TryGetValue("destroyOnHit", out var destroySetting))
            {
                if (destroySetting is bool b)
                {
                    destroyOnHit = b;
                    hasExplicitDestroy = true;
                }
                else if (destroySetting is string s && bool.TryParse(s, out var parsed))
                {
                    destroyOnHit = parsed;
                    hasExplicitDestroy = true;
                }
            }

            bool debugLogs = effectiveParams.debugLogs || generator.debugLogs;
            var eligible = new List<GameObject>(targets.Count);
            foreach (var go in targets)
            {
                if (go == null)
                    continue;

                if (generator.HasMechanic(go, "Beam"))
                {
                    generator.Log("Skipping incompatible modifier 'Orbit' on Beam.");
                    continue;
                }
                if (generator.HasMechanic(go, "Whip"))
                {
                    generator.Log("Skipping incompatible modifier 'Orbit' on Whip.");
                    continue;
                }
                if (generator.HasMechanic(go, "RipplePrimary"))
                {
                    generator.Log("Skipping incompatible modifier 'Orbit' on RipplePrimary.");
                    continue;
                }
                if (generator.HasMechanic(go, "Strike"))
                {
                    generator.Log("Skipping incompatible modifier 'Orbit' on Strike.");
                    continue;
                }

                eligible.Add(go);
            }

            if (eligible.Count == 0)
                return;

            float angleStep = eligible.Count > 1 ? 360f / eligible.Count : 0f;

            for (int i = 0; i < eligible.Count; i++)
            {
                var go = eligible[i];
                if (go == null)
                    continue;

                generator.SetExistingMechanicSetting(go, "Projectile", "disableSelfSpeed", true);

                float pathRotation = pathRotationBase + pathRotationStep * i;
                float angle = startAngleBase + angleStep * i - pathRotation;

                var settings = new List<(string key, object val)>
                {
                    ("radius", radius),
                    ("angularSpeedDeg", angularSpeed),
                    ("speed", angularSpeed),
                    ("startAngleDeg", angle),
                    ("debugLogs", debugLogs),
                };

                if (!string.IsNullOrWhiteSpace(desiredPath))
                {
                    settings.Add(("pathId", desiredPath));
                    settings.Add(("orbitPath", desiredPath));
                    settings.Add(("OrbitPath", desiredPath));
                }

                settings.Add(("PathRotationDeg", pathRotation));

                generator.AddMechanicByName(go, "Orbit", settings.ToArray());

                if (hasExplicitDestroy)
                {
                    generator.SetExistingMechanicSetting(
                        go,
                        "Projectile",
                        "destroyOnHit",
                        destroyOnHit
                    );
                }
            }
        }
    }
}
