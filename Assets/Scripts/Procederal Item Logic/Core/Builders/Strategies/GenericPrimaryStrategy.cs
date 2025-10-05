using System.Collections.Generic;
using Game.Procederal.Api; // for MechanicReflection
using UnityEngine;

namespace Game.Procederal.Core.Builders.Strategies
{
    /// Single-behavior dispatcher strategy. Each primary declares (or inherits via modifier override) exactly one spawnBehavior.
    /// Supported behaviors: interval, sequence, orbit, static (default). Legacy fields spawnOnInterval/numberOfItemsToSpawn supported for backward compatibility.
    public class GenericPrimaryStrategy : IPrimaryStrategy
    {
        public Game.Procederal.MechanicKind Kind => Game.Procederal.MechanicKind.Projectile;

        public void Build(
            Game.Procederal.ProcederalItemGenerator gen,
            GameObject root,
            Game.Procederal.ItemInstruction instruction,
            Game.Procederal.ItemParams p,
            List<GameObject> subItems
        )
        {
            string primaryName = instruction.primary;
            var json = gen.LoadAndMergeJsonSettings(primaryName);

            // Generic fields
            bool spawnOnInterval = false;
            Game.Procederal.Core.Config.ReadIf(json, "spawnOnInterval", ref spawnOnInterval);
            int legacyNumberToSpawn = 1;
            Game.Procederal.Core.Config.ReadIf(
                json,
                "numberOfItemsToSpawn",
                ref legacyNumberToSpawn
            ); // legacy
            float interval = 0.5f;
            Game.Procederal.Core.Config.ReadIf(json, "interval", ref interval);
            float spawnRadius = 0f;
            Game.Procederal.Core.Config.ReadIf(json, "radius", ref spawnRadius);
            float lifetime = -1f;
            Game.Procederal.Core.Config.ReadIf(json, "lifetime", ref lifetime);
            bool immediateFirstBurst = false;
            Game.Procederal.Core.Config.ReadIf(
                json,
                "immediateFirstBurst",
                ref immediateFirstBurst
            );

            // Primary behavior
            string primarySpawnBehavior = null;
            if (
                json != null
                && json.TryGetValue("spawnBehavior", out var sbRaw)
                && sbRaw is string sbStr
            )
                primarySpawnBehavior = sbStr.Trim();

            // Modifier precedence (override chain) + capture childrenToSpawn override last-wins
            string effectiveBehavior = primarySpawnBehavior;
            bool overrideApplied = false;
            int? overrideChildrenToSpawn = null;
            if (instruction.secondary != null)
            {
                foreach (var mod in instruction.secondary)
                {
                    if (string.IsNullOrWhiteSpace(mod))
                        continue;
                    var modSettings = gen.LoadAndMergeJsonSettings(mod);
                    if (modSettings == null)
                        continue;
                    // capture childrenToSpawn from modifier if present (last wins)
                    if (modSettings.TryGetValue("childrenToSpawn", out var ctsRaw))
                    {
                        int parsed = 0;
                        if (ctsRaw is int ci)
                            parsed = ci;
                        else if (ctsRaw is float cf)
                            parsed = Mathf.RoundToInt(cf);
                        else if (ctsRaw is string cs && int.TryParse(cs, out var pis))
                            parsed = pis;
                        if (parsed > 0)
                            overrideChildrenToSpawn = parsed;
                    }
                    if (
                        modSettings.TryGetValue("spawnBehaviorOverride", out var oRaw)
                        && oRaw is string oStr
                        && !string.IsNullOrWhiteSpace(oStr)
                    )
                    {
                        effectiveBehavior = oStr.Trim();
                        overrideApplied = true;
                        continue; // allow later overrides to win (last wins)
                    }
                    if (overrideApplied)
                        continue; // skip plain spawnBehavior if an override already won
                    if (
                        modSettings.TryGetValue("spawnBehavior", out var mRaw)
                        && mRaw is string mStr
                        && !string.IsNullOrWhiteSpace(mStr)
                    )
                        effectiveBehavior = mStr.Trim();
                }
            }
            string spawnBehavior = string.IsNullOrWhiteSpace(effectiveBehavior)
                ? null
                : effectiveBehavior.ToLowerInvariant();

            // Visual hints / payload extraction
            string spriteType = null;
            if (json != null && json.TryGetValue("spriteType", out var stRaw) && stRaw is string st)
                spriteType = st.Trim();
            string customSpritePath = null;
            if (
                json != null
                && json.TryGetValue("customSpritePath", out var cpRaw)
                && cpRaw is string cp
            )
                customSpritePath = cp.Trim();
            Color spriteColor = Color.white;
            if (json != null && json.TryGetValue("spriteColor", out var scRaw))
            {
                if (!Game.Procederal.Core.Config.TryParseColor(scRaw, out spriteColor))
                    spriteColor = Color.white;
            }

            var payload = new List<(string key, object val)>();
            if (json != null)
            {
                foreach (var kv in json)
                {
                    var k = kv.Key;
                    if (string.IsNullOrWhiteSpace(k))
                        continue;
                    if (k.Equals("spawnOnInterval", System.StringComparison.OrdinalIgnoreCase))
                        continue;
                    if (k.Equals("interval", System.StringComparison.OrdinalIgnoreCase))
                        continue;
                    if (k.Equals("numberOfItemsToSpawn", System.StringComparison.OrdinalIgnoreCase))
                        continue;
                    if (k.Equals("NumberOfItemsToSpawn", System.StringComparison.OrdinalIgnoreCase))
                        continue;
                    if (k.Equals("childrenToSpawn", System.StringComparison.OrdinalIgnoreCase))
                        continue;
                    if (k.Equals("radius", System.StringComparison.OrdinalIgnoreCase))
                        continue;
                    if (k.Equals("lifetime", System.StringComparison.OrdinalIgnoreCase))
                        continue;
                    if (k.Equals("spawnBehavior", System.StringComparison.OrdinalIgnoreCase))
                        continue;
                    if (k.Equals("immediateFirstBurst", System.StringComparison.OrdinalIgnoreCase))
                        continue;
                    payload.Add((k, kv.Value));
                }
            }

            // childrenToSpawn resolution: primary setting -> legacy -> modifier override -> fallback 1
            int childrenToSpawn = 0;
            Game.Procederal.Core.Config.ReadIf(json, "childrenToSpawn", ref childrenToSpawn);
            if (childrenToSpawn <= 0)
                childrenToSpawn = legacyNumberToSpawn > 0 ? legacyNumberToSpawn : 1;
            if (overrideChildrenToSpawn.HasValue && overrideChildrenToSpawn.Value > 0)
                childrenToSpawn = overrideChildrenToSpawn.Value;
            // Final clamp
            childrenToSpawn = Mathf.Max(1, childrenToSpawn);

            // Map legacy spawnOnInterval with missing explicit behavior
            if (string.IsNullOrEmpty(spawnBehavior) && spawnOnInterval)
                spawnBehavior = "interval";
            // Orbit behavior deprecated: treat any 'orbit' token as static (children created directly; OrbitMechanic should be attached via data if desired)
            if (spawnBehavior == "orbit")
                spawnBehavior = "static";

            switch (spawnBehavior)
            {
                case "interval":
                    BuildInterval(
                        gen,
                        root,
                        instruction,
                        p,
                        primaryName,
                        childrenToSpawn,
                        interval,
                        spawnRadius,
                        lifetime,
                        immediateFirstBurst,
                        spriteType,
                        customSpritePath,
                        spriteColor,
                        payload
                    );
                    return;
                case "sequence":
                    BuildSequence(
                        gen,
                        root,
                        instruction,
                        primaryName,
                        childrenToSpawn,
                        lifetime,
                        spriteType,
                        customSpritePath,
                        spriteColor,
                        payload,
                        json
                    );
                    return;
                case "static":
                case null:
                case "":
                    break; // treat as static
                default:
                    break; // unknown -> static
            }

            // Static spawn helper (sequence tagging off by default)
            var created = Game.Procederal.Api.SpawnHelpers.SpawnNumberOfChildren(
                gen,
                root.transform,
                primaryName,
                childrenToSpawn,
                payload,
                tagSequenceIndex: false,
                injectDirectionFromResolverForProjectile: string.Equals(
                    primaryName,
                    "Projectile",
                    System.StringComparison.OrdinalIgnoreCase
                )
            );
            // Attach secondary modifiers (e.g., Lock, Orbit, Bounce, etc.) to each child so their effects (stun, orbit spacing) are active.
            ApplySecondaryModifiers(created, gen, instruction, primaryName);
            // Now that OrbitMechanics (if any) may have been added, distribute angles evenly.
            DistributeOrbitIfPresent(created, gen);
            subItems.AddRange(created);
        }

        private void DistributeOrbitIfPresent(
            List<GameObject> children,
            Game.Procederal.ProcederalItemGenerator gen
        )
        {
            if (children == null || children.Count <= 1)
                return;
            // Collect orbit mechanics present
            var orbits = new List<Mechanics.Neuteral.OrbitMechanic>();
            foreach (var c in children)
            {
                if (c == null)
                    continue;
                var om = c.GetComponent<Mechanics.Neuteral.OrbitMechanic>();
                if (om != null)
                    orbits.Add(om);
            }
            int n = orbits.Count;
            if (n <= 1)
                return;
            // Use each component's radius (assume they are identical; take first)
            for (int i = 0; i < n; i++)
            {
                float angle = (360f * i) / n;
                orbits[i].SetAngleDeg(angle, repositionNow: true);
            }
        }

        private void ApplySecondaryModifiers(
            List<GameObject> children,
            Game.Procederal.ProcederalItemGenerator gen,
            Game.Procederal.ItemInstruction instruction,
            string primaryName
        )
        {
            if (children == null || children.Count == 0 || instruction == null)
                return;
            if (instruction.secondary == null || instruction.secondary.Count == 0)
                return;
            foreach (var mod in instruction.secondary)
            {
                if (string.IsNullOrWhiteSpace(mod))
                    continue;
                // Skip if same as primary
                if (string.Equals(mod, primaryName, System.StringComparison.OrdinalIgnoreCase))
                    continue;
                // Resolve mechanic type
                if (
                    !MechanicsRegistry.Instance.TryGetPath(mod, out var path)
                    || string.IsNullOrWhiteSpace(path)
                )
                    continue;
                var type = MechanicReflection.ResolveTypeFromMechanicPath(path);
                if (type == null)
                    continue;
                foreach (var child in children)
                {
                    if (child == null)
                        continue;
                    // Avoid duplicate component
                    if (child.GetComponent(type) != null)
                        continue;
                    // Let AddMechanicByName merge JSON defaults + overrides
                    gen.AddMechanicByName(
                        child,
                        mod,
                        System.Array.Empty<(string key, object val)>()
                    );
                    gen.InitializeMechanics(
                        child,
                        gen.owner != null ? gen.owner : gen.transform,
                        gen.target
                    );
                }
            }
        }

        private void BuildSequence(
            Game.Procederal.ProcederalItemGenerator gen,
            GameObject root,
            Game.Procederal.ItemInstruction instruction,
            string primaryName,
            int count,
            float lifetime,
            string spriteType,
            string customSpritePath,
            Color spriteColor,
            List<(string key, object val)> payload,
            Dictionary<string, object> json
        )
        {
            var seq =
                root.GetComponent<Game.Procederal.Api.SequenceSpawnBehavior>()
                ?? root.AddComponent<Game.Procederal.Api.SequenceSpawnBehavior>();
            seq.generator = gen;
            seq.owner = gen.owner != null ? gen.owner : gen.transform;
            seq.sequenceCount = Mathf.Max(1, count);
            float spacing = 0.05f;
            if (json != null && json.TryGetValue("intervalBetween", out var ibRaw))
            {
                if (ibRaw is float fib)
                    spacing = Mathf.Max(0f, fib);
                else if (ibRaw is int iib)
                    spacing = Mathf.Max(0f, iib);
                else if (ibRaw is string sib && float.TryParse(sib, out var psib))
                    spacing = Mathf.Max(0f, psib);
            }
            seq.spacingSeconds = spacing;
            seq.payloadMechanicName = primaryName;
            seq.lifetime = lifetime;
            seq.excludeOwner = true;
            seq.requireMobTag = true;
            seq.spriteType = spriteType;
            seq.customSpritePath = customSpritePath;
            seq.spriteColor = spriteColor;
            if (json != null && json.TryGetValue("speed", out var spdRaw))
            {
                if (spdRaw is float fs)
                    seq.travelSpeed = fs;
                else if (spdRaw is int ispd)
                    seq.travelSpeed = ispd;
            }
            if (json != null && json.TryGetValue("damage", out var dmgRaw))
            {
                if (dmgRaw is int idmg)
                    seq.damage = idmg;
                else if (dmgRaw is float fdmg)
                    seq.damage = Mathf.RoundToInt(fdmg);
            }
            seq.SetExtraPayloadSettings(payload.ToArray());
            seq.debugLogs = gen.debugLogs;
            if (gen.autoApplyCompatibleModifiers)
            {
                foreach (var kind in gen.GetModifiersToApply(instruction))
                {
                    if (kind == Game.Procederal.MechanicKind.Orbit)
                        continue;
                    seq.AddModifierSpec(kind.ToString());
                }
            }
            seq.BeginSequence();
        }

        // BuildOrbit removed (deprecated)

        private void BuildInterval(
            Game.Procederal.ProcederalItemGenerator gen,
            GameObject root,
            Game.Procederal.ItemInstruction instruction,
            Game.Procederal.ItemParams p,
            string primaryName,
            int countPerInterval,
            float interval,
            float spawnRadius,
            float lifetime,
            bool immediateFirstBurst,
            string spriteType,
            string customSpritePath,
            Color spriteColor,
            List<(string key, object val)> payload
        )
        {
            var existing = root.GetComponent<Game.Procederal.Api.GenericIntervalSpawner>();
            var spawner =
                existing != null
                    ? existing
                    : root.AddComponent<Game.Procederal.Api.GenericIntervalSpawner>();
            spawner.generator = gen;
            spawner.owner = gen.owner != null ? gen.owner : gen.transform;
            spawner.interval = Mathf.Max(0.01f, interval);
            spawner.countPerInterval = Mathf.Max(1, countPerInterval);
            spawner.spawnRadius = Mathf.Max(0f, spawnRadius);
            spawner.lifetime = lifetime;
            spawner.payloadMechanicName = primaryName;
            spawner.immediateFirstBurst = immediateFirstBurst;
            if (string.Equals(primaryName, "Projectile", System.StringComparison.OrdinalIgnoreCase))
            {
                int dIdx = payload.FindIndex(kv =>
                    string.Equals(kv.key, "direction", System.StringComparison.OrdinalIgnoreCase)
                );
                if (dIdx >= 0)
                    payload.RemoveAt(dIdx);
                payload.Add(("directionFromResolver", "vector2"));
            }
            spawner.SetPayloadSettings(payload.ToArray());
            if (!string.IsNullOrEmpty(spriteType))
                spawner.spriteType = spriteType;
            if (!string.IsNullOrEmpty(customSpritePath))
                spawner.customSpritePath = customSpritePath;
            spawner.spriteColor = spriteColor;
            if (gen.autoApplyCompatibleModifiers)
            {
                spawner.ClearModifierSpecs();
                gen.ForwardModifiersToSpawner(spawner, instruction, p);
            }
        }
    }
}
