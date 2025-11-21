using System;
using System.Collections.Generic;
using System.Globalization;
using Game.Procederal.Api; // for MechanicReflection
using Game.Procederal.Core;
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
                        switch (ctsRaw)
                        {
                            case int ci:
                                parsed = ci;
                                break;
                            case long cl when cl <= int.MaxValue && cl >= int.MinValue:
                                parsed = (int)cl;
                                break;
                            case float cf:
                                parsed = Mathf.RoundToInt(cf);
                                break;
                            case double cd:
                                parsed = (int)Math.Round(cd, MidpointRounding.AwayFromZero);
                                break;
                            case decimal cm:
                                parsed = (int)Math.Round(cm, MidpointRounding.AwayFromZero);
                                break;
                            case string cs
                                when int.TryParse(
                                    cs,
                                    NumberStyles.Integer,
                                    CultureInfo.InvariantCulture,
                                    out var pis
                                ):
                                parsed = pis;
                                break;
                        }
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
            DistributeOrbitIfPresent(created);
            subItems.AddRange(created);
        }

        private void DistributeOrbitIfPresent(List<GameObject> children)
        {
            if (children == null || children.Count <= 1)
                return;

            var orbits = new List<Mechanics.Neuteral.OrbitMechanic>();
            for (int i = 0; i < children.Count; i++)
            {
                var child = children[i];
                if (child == null)
                    continue;

                var orbit = child.GetComponent<Mechanics.Neuteral.OrbitMechanic>();
                if (orbit != null)
                    orbits.Add(orbit);
            }

            int count = orbits.Count;
            if (count <= 1)
                return;

            for (int i = 0; i < count; i++)
            {
                float angle = (360f * i) / count;
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
                    !MechanicApplier.TryResolveType(
                        MechanicsRegistry.Instance,
                        mod,
                        out var type,
                        out _,
                        out _
                    )
                )
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
                        gen.ResolveTargetOrDefault()
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
            // Determine per-child spacing within a sequence
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

            // Auto-loop cadence
            float repeatInterval = 0f;
            bool immediateFirstBurst = false;
            if (json != null)
            {
                if (json.TryGetValue("interval", out var iRaw))
                {
                    if (iRaw is float fi)
                        repeatInterval = fi;
                    else if (iRaw is int ii)
                        repeatInterval = ii;
                    else if (iRaw is string si && float.TryParse(si, out var psi))
                        repeatInterval = psi;
                }
                if (json.TryGetValue("immediateFirstBurst", out var ifbRaw))
                {
                    if (ifbRaw is bool b)
                        immediateFirstBurst = b;
                    else if (ifbRaw is string sb && bool.TryParse(sb, out var pb))
                        immediateFirstBurst = pb;
                }
            }

            // Multi-batch: number of simultaneous batch anchors (clamped to max and hard-capped at 4)
            int batchesAtOnce = 1;
            int maxBatchesAtOnce = 4;
            if (json != null)
            {
                Game.Procederal.Core.Config.ReadIf(json, "batchesAtOnce", ref batchesAtOnce);
                Game.Procederal.Core.Config.ReadIf(json, "maxBatchesAtOnce", ref maxBatchesAtOnce);
            }
            int hardCap = 4;
            int cap = Mathf.Max(1, Mathf.Min(hardCap, Mathf.Max(1, maxBatchesAtOnce)));
            int batchCount = Mathf.Clamp(batchesAtOnce, 1, cap);

            // Determine anchor radius from JSON (prefer explicit radius, else outerRadius)
            Transform owner = gen.owner != null ? gen.owner : gen.transform;
            float batchRadius = 0f;
            if (json != null)
            {
                if (json.TryGetValue("radius", out var rRaw))
                {
                    if (rRaw is float fr)
                        batchRadius = fr;
                    else if (rRaw is int ir)
                        batchRadius = ir;
                    else if (rRaw is string sr && float.TryParse(sr, out var pr))
                        batchRadius = pr;
                }
                if (
                    Mathf.Approximately(batchRadius, 0f)
                    && json.TryGetValue("outerRadius", out var orRaw)
                )
                {
                    if (orRaw is float forr)
                        batchRadius = forr;
                    else if (orRaw is int iorr)
                        batchRadius = iorr;
                    else if (orRaw is string sor && float.TryParse(sor, out var por))
                        batchRadius = por;
                }
            }
            batchRadius = Mathf.Max(0f, batchRadius);

            // Build star/polygon ordering based on the maximum batch count so anchor slots stay consistent
            var starOrder = BuildStarOrder(cap);
            var anchorParent = new GameObject($"{primaryName}_SequenceAnchors");
            anchorParent.layer = root.layer;
            anchorParent.transform.SetParent(root.transform, worldPositionStays: false);
            anchorParent.transform.localPosition = Vector3.zero;

            for (int bi = 0; bi < batchCount; bi++)
            {
                int vertexIndex = starOrder[bi % starOrder.Count];
                float angleDeg = (360f * vertexIndex) / Mathf.Max(1, cap);
                float rad = angleDeg * Mathf.Deg2Rad;
                Vector3 localOffset = new Vector3(Mathf.Cos(rad), Mathf.Sin(rad), 0f) * batchRadius;

                var anchor = new GameObject($"{primaryName}_SeqAnchor_{bi}");
                anchor.layer = root.layer;
                anchor.transform.SetParent(anchorParent.transform, worldPositionStays: false);
                anchor.transform.localPosition = localOffset;

                var seq = anchor.AddComponent<Game.Procederal.Api.SequenceSpawnBehavior>();
                seq.generator = gen;
                seq.owner = owner;
                seq.sequenceCount = Mathf.Max(1, count);
                seq.spacingSeconds = spacing;
                seq.payloadMechanicName = primaryName;
                seq.lifetime = lifetime;
                seq.excludeOwner = true;
                seq.requireMobTag = true;
                seq.spriteType = spriteType;
                seq.customSpritePath = customSpritePath;
                seq.spriteColor = spriteColor;
                // Spawn at anchor position rather than owner's
                seq.useOwnerPositionForSpawn = false;
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

                if (repeatInterval > 0f)
                {
                    var repeater =
                        anchor.AddComponent<Game.Procederal.Api.SequenceIntervalRepeater>();
                    repeater.sequence = seq;
                    repeater.interval = Mathf.Max(0.01f, repeatInterval);
                    repeater.immediateFirstBurst = immediateFirstBurst;
                    repeater.active = true;
                    repeater.debugLogs = gen.debugLogs;
                }
                else
                {
                    seq.BeginSequence();
                }
            }

            static System.Collections.Generic.List<int> BuildStarOrder(int sides)
            {
                var order = new System.Collections.Generic.List<int>(Mathf.Max(1, sides));
                if (sides <= 0)
                {
                    order.Add(0);
                    return order;
                }

                var used = new bool[sides];
                int half = sides / 2;
                int attempts = 0;
                while (order.Count < sides && attempts < sides * 4)
                {
                    int idx = attempts / 2;
                    bool even = (attempts % 2) == 0;
                    int candidate = even ? idx : (idx + half) % sides;
                    candidate = ((candidate % sides) + sides) % sides;
                    if (!used[candidate])
                    {
                        used[candidate] = true;
                        order.Add(candidate);
                    }
                    else
                    {
                        for (int offset = 1; offset < sides; offset++)
                        {
                            int alt = (candidate + offset) % sides;
                            if (!used[alt])
                            {
                                used[alt] = true;
                                order.Add(alt);
                                break;
                            }
                        }
                    }
                    attempts++;
                }

                if (order.Count == 0)
                {
                    for (int i = 0; i < sides; i++)
                        order.Add(i);
                }

                return order;
            }
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
