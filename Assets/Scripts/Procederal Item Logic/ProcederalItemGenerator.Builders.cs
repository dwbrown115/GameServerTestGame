using System.Collections.Generic;
using Game.Procederal.Api;
using Game.Procederal.Core;
using UnityEngine;

namespace Game.Procederal
{
    public partial class ProcederalItemGenerator
    {
        private Transform ResolveOwner()
        {
            return owner != null ? owner : transform;
        }

        private static Transform FindNearestMob(Transform from)
        {
            var mobs = GameObject.FindGameObjectsWithTag("Mob");
            if (mobs == null || mobs.Length == 0)
                return null;
            float best = float.MaxValue;
            Transform bestT = null;
            foreach (var go in mobs)
            {
                if (go == null)
                    continue;
                float d2 = (go.transform.position - from.position).sqrMagnitude;
                if (d2 < best)
                {
                    best = d2;
                    bestT = go.transform;
                }
            }
            return bestT;
        }

        private static GameObject CreateChild(string name, Transform parent)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = Vector3.zero;
            return go;
        }

        internal void BuildRipple(
            GameObject root,
            ItemInstruction instruction,
            ItemParams p,
            List<GameObject> subItems
        )
        {
            var ownerT = ResolveOwner();
            var json = LoadAndMergeJsonSettings("Ripple");

            float startRadius = 1f;
            if (json.TryGetValue("startRadius", out var sr))
            {
                if (sr is float fsr)
                    startRadius = Mathf.Max(0f, fsr);
                else if (sr is int isr)
                    startRadius = Mathf.Max(0f, isr);
            }
            float endDiameter = 8f;
            if (json.TryGetValue("endDiameter", out var ed))
            {
                if (ed is float fed)
                    endDiameter = Mathf.Max(0.01f, fed);
                else if (ed is int ied)
                    endDiameter = Mathf.Max(0.01f, ied);
            }
            float growDuration = 1.5f;
            if (json.TryGetValue("growDuration", out var gd))
            {
                if (gd is float fgd)
                    growDuration = Mathf.Max(0.01f, fgd);
                else if (gd is int igd)
                    growDuration = Mathf.Max(0.01f, igd);
            }
            float edgeThickness = 0.2f;
            if (json.TryGetValue("edgeThickness", out var et))
            {
                if (et is float fet)
                    edgeThickness = Mathf.Max(0.01f, fet);
                else if (et is int iet)
                    edgeThickness = Mathf.Max(0.01f, iet);
            }
            int damage = 5;
            if (json.TryGetValue("damage", out var dmg))
            {
                if (dmg is int id)
                    damage = id;
                else if (dmg is float fd)
                    damage = Mathf.RoundToInt(fd);
            }
            bool showViz = true;
            if (json.TryGetValue("showVisualization", out var sv))
            {
                if (sv is bool b)
                    showViz = b;
                else if (sv is string s && bool.TryParse(s, out var pb))
                    showViz = pb;
            }
            Color vizColor = Color.cyan;
            if (json.TryGetValue("spriteColor", out var sc))
            {
                Color c;
                if (Config.TryParseColor(sc, out c))
                    vizColor = c;
            }
            bool spawnOnInterval = false;
            if (json.TryGetValue("spawnOnInterval", out var soi))
            {
                if (soi is bool b)
                    spawnOnInterval = b;
                else if (soi is string ss && bool.TryParse(ss, out var pb))
                    spawnOnInterval = pb;
            }
            int numberToSpawn = 1;
            if (
                json.TryGetValue("numberOfItemsToSpawn", out var nts)
                || json.TryGetValue("NumberOfItemsToSpawn", out nts)
            )
            {
                if (nts is int ni)
                    numberToSpawn = Mathf.Max(1, ni);
                else if (nts is float nf)
                    numberToSpawn = Mathf.Max(1, Mathf.RoundToInt(nf));
                else if (nts is string ns && int.TryParse(ns, out var nsi))
                    numberToSpawn = Mathf.Max(1, nsi);
            }
            float interval = 0.5f;
            if (json.TryGetValue("interval", out var iv))
            {
                if (iv is float fiv)
                    interval = Mathf.Max(0.01f, fiv);
                else if (iv is int iiv)
                    interval = Mathf.Max(0.01f, iiv);
                else if (iv is string siv && float.TryParse(siv, out var pf))
                    interval = Mathf.Max(0.01f, pf);
            }

            if (spawnOnInterval)
            {
                var spawner = root.AddComponent<RippleIntervalSpawner>();
                spawner.generator = this;
                spawner.owner = ownerT;
                spawner.interval = interval;
                spawner.countPerInterval = Mathf.Max(
                    1,
                    generatorChildrenCount > 0 ? generatorChildrenCount : numberToSpawn
                );
                spawner.startRadius = startRadius;
                spawner.endDiameter = endDiameter;
                spawner.growDuration = growDuration;
                spawner.edgeThickness = edgeThickness;
                spawner.damage = damage;
                spawner.excludeOwner = true;
                spawner.requireMobTag = true;
                spawner.showVisualization = showViz;
                spawner.vizColor = vizColor;
                spawner.debugLogs = p.debugLogs || debugLogs;

                if (autoApplyCompatibleModifiers)
                    ForwardModifiersToSpawner(spawner, instruction, p);
                return;
            }

            var ripple = CreateChild("Ripple", root.transform);
            AddMechanicByName(
                ripple,
                "Ripple",
                new (string key, object val)[]
                {
                    ("startRadius", startRadius),
                    ("endDiameter", endDiameter),
                    ("growDuration", growDuration),
                    ("edgeThickness", edgeThickness),
                    ("damage", damage),
                    ("excludeOwner", true),
                    ("requireMobTag", true),
                    ("showVisualization", showViz),
                    ("vizColor", vizColor),
                    ("debugLogs", p.debugLogs || debugLogs),
                }
            );
            InitializeMechanics(ripple, owner, target);
            subItems.Add(ripple);
        }

        internal void BuildSwordSlash(
            GameObject root,
            ItemInstruction instruction,
            ItemParams p,
            List<GameObject> subItems
        )
        {
            var ownerT = ResolveOwner();
            var json = LoadAndMergeJsonSettings("SwordSlash");
            int seriesCount = 3;
            float intervalBetween = 0.08f;
            bool spawnOnInterval = false;
            float seriesInterval = 0.8f;
            float outerRadius = 1.5f;
            float width = 0.5f;
            float arcLen = 120f;
            bool edgeOnly = true;
            float edgeThickness = 0.2f;
            int damage = 8;
            float speed = 12f;
            Color color = Color.white;

            Config.ReadIf(json, "seriesCount", ref seriesCount);
            Config.ReadIf(json, "intervalBetween", ref intervalBetween);
            Config.ReadIf(json, "spawnOnInterval", ref spawnOnInterval);
            Config.ReadIf(json, "interval", ref seriesInterval);
            Config.ReadIf(json, "outerRadius", ref outerRadius);
            Config.ReadIf(json, "width", ref width);
            Config.ReadIf(json, "arcLengthDeg", ref arcLen);
            Config.ReadIf(json, "edgeOnly", ref edgeOnly);
            Config.ReadIf(json, "edgeThickness", ref edgeThickness);
            Config.ReadIf(json, "damage", ref damage);
            Config.ReadIf(json, "speed", ref speed);
            if (json.TryGetValue("spriteColor", out var sc))
                Config.TryParseColor(sc, out color);

            if (spawnOnInterval)
            {
                var spawner = root.AddComponent<SwordSlashIntervalSpawner>();
                spawner.generator = this;
                spawner.owner = ownerT;
                spawner.interval = Mathf.Max(0.01f, seriesInterval);
                spawner.seriesCount = Mathf.Max(1, seriesCount);
                spawner.intervalBetween = Mathf.Max(0f, intervalBetween);
                spawner.outerRadius = Mathf.Max(0.0001f, outerRadius);
                spawner.width = Mathf.Max(0.0001f, width);
                spawner.arcLengthDeg = Mathf.Clamp(arcLen, 1f, 359f);
                spawner.edgeOnly = edgeOnly;
                spawner.edgeThickness = Mathf.Max(0.0001f, edgeThickness);
                spawner.vizColor = color;
                spawner.damage = Mathf.Max(0, damage);
                spawner.speed = Mathf.Max(0.01f, speed);
                spawner.excludeOwner = true;
                spawner.requireMobTag = true;
                spawner.aimAtNearestEnemy = instruction.HasSecondary(MechanicKind.Track);
                spawner.debugLogs = p.debugLogs || debugLogs;

                if (autoApplyCompatibleModifiers)
                    ForwardModifiersToSpawner(spawner, instruction, p);
                return;
            }

            Vector2 dir = Vector2.right;
            var ownerRb = ownerT != null ? ownerT.GetComponent<Rigidbody2D>() : null;
            if (ownerRb != null && ownerRb.linearVelocity.sqrMagnitude > 0.01f)
                dir = ownerRb.linearVelocity.normalized;

            if (instruction.HasSecondary(MechanicKind.Track) && ownerT != null)
            {
                var nearest = FindNearestMob(ownerT);
                if (nearest != null)
                {
                    Vector2 to = (Vector2)(nearest.position - ownerT.position);
                    if (to.sqrMagnitude > 1e-6f)
                        dir = to.normalized;
                }
            }

            int count = Mathf.Max(1, seriesCount);
            float spacing = Mathf.Max(0f, intervalBetween) * Mathf.Max(0.01f, speed);
            for (int i = 0; i < count; i++)
            {
                Vector3 spawnPos =
                    ownerT != null
                        ? ownerT.position - (Vector3)(dir * spacing * i)
                        : root.transform.position;
                var shell = new SpawnHelpers.PayloadShellOptions
                {
                    parent = root.transform,
                    position = spawnPos,
                    layer = ownerT != null ? ownerT.gameObject.layer : root.layer,
                    spriteType = null,
                    customSpritePath = null,
                    spriteColor = color,
                    createCollider = false,
                    colliderRadius = 0.5f,
                    createRigidBody = true,
                    bodyType = RigidbodyType2D.Dynamic,
                    freezeRotation = true,
                    addAutoDestroy = true,
                    lifetimeSeconds = 4f,
                };

                var slash = SpawnHelpers.CreatePayloadShell($"SwordSlash_{i}", shell);
                if (slash.transform.parent != root.transform)
                    slash.transform.SetParent(root.transform, worldPositionStays: true);

                AddMechanicByName(
                    slash,
                    "SwordSlash",
                    new (string key, object val)[]
                    {
                        ("outerRadius", outerRadius),
                        ("width", width),
                        ("arcLengthDeg", arcLen),
                        ("edgeOnly", edgeOnly),
                        ("edgeThickness", edgeThickness),
                        ("showVisualization", true),
                        ("vizColor", color),
                    }
                );

                slash.transform.right = dir;

                AddMechanicByName(
                    slash,
                    "Projectile",
                    new (string key, object val)[]
                    {
                        ("direction", dir),
                        ("speed", speed),
                        ("damage", damage),
                        ("requireMobTag", true),
                        ("excludeOwner", true),
                        ("destroyOnHit", true),
                        ("disableSelfSpeed", true),
                    }
                );

                AddMechanicByName(
                    slash,
                    "ChildMovementMechanic",
                    new (string key, object val)[]
                    {
                        ("direction", dir),
                        ("speed", speed),
                        ("disableSelfSpeed", false),
                    }
                );

                var rb = slash.GetComponent<Rigidbody2D>();
                if (rb != null)
                    rb.linearVelocity = dir * speed;

                InitializeMechanics(slash, owner, target);

                subItems.Add(slash);
            }
        }

        internal void BuildWhip(
            GameObject root,
            ItemInstruction instruction,
            ItemParams p,
            List<GameObject> subItems
        )
        {
            var ownerT = ResolveOwner();
            int desired = p != null ? p.subItemCount : 1;
            if (generatorChildrenCount > 0)
                desired = generatorChildrenCount;
            int count = Mathf.Clamp(Mathf.Max(1, desired), 1, 4);
            string[] dirs = { "right", "up", "left", "down" };

            var set = root.AddComponent<WhipArcSet>();
            set.generator = this;
            set.owner = ownerT;
            set.target = target;
            set.debugLogs = p.debugLogs || debugLogs;

            for (int i = 0; i < count; i++)
            {
                var whip = CreateChild($"Whip_{i}", root.transform);
                var json = LoadAndMergeJsonSettings("Whip");
                var settings = new List<(string key, object val)>();
                if (json.TryGetValue("outerRadius", out var or))
                    settings.Add(("outerRadius", or));
                if (json.TryGetValue("width", out var w))
                    settings.Add(("width", w));
                if (json.TryGetValue("arcLengthDeg", out var ad))
                    settings.Add(("arcLengthDeg", ad));
                if (json.TryGetValue("drawDuration", out var dd))
                    settings.Add(("drawDuration", dd));
                if (json.TryGetValue("interval", out var iv))
                    settings.Add(("interval", iv));
                if (json.TryGetValue("damagePerInterval", out var dmg))
                    settings.Add(("damagePerInterval", dmg));
                if (json.TryGetValue("showVisualization", out var sv))
                    settings.Add(("showVisualization", sv));
                if (json.TryGetValue("spriteColor", out var sc))
                    settings.Add(("vizColor", sc));

                settings.Add(("direction", dirs[i % dirs.Length]));
                settings.Add(("excludeOwner", true));
                settings.Add(("requireMobTag", true));
                settings.Add(("debugLogs", p.debugLogs || debugLogs));

                AddMechanicByName(whip, "Whip", settings.ToArray());
                InitializeMechanics(whip, owner, target);
                subItems.Add(whip);
            }

            set.RefreshDirs();
        }

        internal void BuildProjectileSet(
            GameObject root,
            ItemInstruction instruction,
            ItemParams p,
            List<GameObject> subItems
        )
        {
            var ownerT = ResolveOwner();
            int desired = p != null ? p.subItemCount : 1;
            if (generatorChildrenCount > 0)
                desired = generatorChildrenCount;
            int count = Mathf.Max(1, desired);
            float baseAngle = p.startAngleDeg;
            bool wantOrbit = instruction.HasSecondary(MechanicKind.Orbit);

            var projectileJson = LoadAndMergeJsonSettings("Projectile");
            string spriteType = null;
            if (projectileJson.TryGetValue("spriteType", out var st))
                spriteType = (st as string)?.Trim();
            if (string.IsNullOrEmpty(spriteType))
                spriteType = "circle";
            string customPath = null;
            if (projectileJson.TryGetValue("customSpritePath", out var cp))
                customPath = (cp as string)?.Trim();
            Color projColor = Color.white;
            if (projectileJson.TryGetValue("spriteColor", out var sc))
            {
                if (!Config.TryParseColor(sc, out projColor))
                    projColor = Color.white;
            }

            string spawnBehavior = null;
            if (projectileJson.TryGetValue("spawnBehavior", out var sbRaw))
                spawnBehavior = (sbRaw as string)?.Trim();
            string spawnBehaviorNorm = string.IsNullOrEmpty(spawnBehavior)
                ? null
                : spawnBehavior.ToLowerInvariant();

            bool spawnOnInterval = false;
            if (projectileJson.TryGetValue("spawnOnInterval", out var soi))
            {
                if (soi is bool b)
                    spawnOnInterval = b;
                else if (soi is string ss && bool.TryParse(ss, out var pb))
                    spawnOnInterval = pb;
            }
            int numberToSpawn = 1;
            if (
                projectileJson.TryGetValue("numberOfItemsToSpawn", out var nts)
                || projectileJson.TryGetValue("NumberOfItemsToSpawn", out nts)
            )
            {
                if (nts is int ni)
                    numberToSpawn = Mathf.Max(1, ni);
                else if (nts is float nf)
                    numberToSpawn = Mathf.Max(1, Mathf.RoundToInt(nf));
                else if (nts is string ns && int.TryParse(ns, out var nsi))
                    numberToSpawn = Mathf.Max(1, nsi);
            }
            float spawnInterval = 0.5f;
            if (projectileJson.TryGetValue("interval", out var iv))
            {
                if (iv is float fiv)
                    spawnInterval = Mathf.Max(0.01f, fiv);
                else if (iv is int iiv)
                    spawnInterval = Mathf.Max(0.01f, iiv);
                else if (iv is string siv && float.TryParse(siv, out var pf))
                    spawnInterval = Mathf.Max(0.01f, pf);
            }
            float spawnRadius = 0f;
            if (projectileJson.TryGetValue("radius", out var rv))
            {
                if (rv is float fr)
                    spawnRadius = Mathf.Max(0f, fr);
                else if (rv is int ir)
                    spawnRadius = Mathf.Max(0f, ir);
                else if (rv is string sr && float.TryParse(sr, out var pr))
                    spawnRadius = Mathf.Max(0f, pr);
            }
            float lifetime = -1f;
            if (projectileJson.TryGetValue("lifetime", out var lt))
            {
                if (lt is float flt)
                    lifetime = Mathf.Max(0f, flt);
                else if (lt is int ilt)
                    lifetime = Mathf.Max(0f, ilt);
                else if (lt is string slt && float.TryParse(slt, out var plt))
                    lifetime = Mathf.Max(0f, plt);
            }
            float projSpeed = -1f;
            if (projectileJson.TryGetValue("speed", out var spd))
            {
                if (spd is float fs)
                    projSpeed = fs;
                else if (spd is int ispd)
                    projSpeed = ispd;
                else if (spd is string sspd && float.TryParse(sspd, out var pfs))
                    projSpeed = pfs;
            }

            if (wantOrbit)
            {
                var orbitOverrides = LoadKvpArrayForMechanic("Orbit", "MechanicOverrides");
                if (orbitOverrides.TryGetValue("spawnOnInterval", out var oSoI))
                {
                    bool orbitSaysSpawn = false;
                    if (oSoI is bool ob)
                        orbitSaysSpawn = ob;
                    else if (oSoI is string os && bool.TryParse(os, out var pob))
                        orbitSaysSpawn = pob;
                    if (!orbitSaysSpawn)
                        spawnOnInterval = false;
                }
            }

            if (spawnOnInterval)
            {
                var spawner = root.AddComponent<IntervalSpawner>();
                spawner.generator = this;
                spawner.owner = ownerT;
                spawner.interval = spawnInterval;
                int intervalCount =
                    generatorChildrenCount > 0 ? generatorChildrenCount : numberToSpawn;
                spawner.countPerInterval = Mathf.Max(1, intervalCount);
                spawner.spawnRadius = Mathf.Max(0f, spawnRadius);
                spawner.lifetime = lifetime;
                spawner.spriteType = spriteType;
                spawner.customSpritePath = customPath;
                spawner.spriteColor = projColor;
                spawner.excludeOwner = true;
                spawner.requireMobTag = true;
                spawner.destroyOnHit = p.projectileDestroyOnHit;
                spawner.damage = p.projectileDamage;
                spawner.projectileSpeed = projSpeed > 0f ? projSpeed : -1f;
                spawner.debugLogs = p.debugLogs || debugLogs;
                if (!string.IsNullOrEmpty(spawnBehaviorNorm))
                {
                    if (spawnBehaviorNorm == "chaos")
                    {
                        var chaos =
                            root.GetComponent<ChaosSpawnPosition>()
                            ?? root.AddComponent<ChaosSpawnPosition>();
                        spawner.spawnResolverBehaviour = chaos;
                    }
                    else if (spawnBehaviorNorm == "neuteral" || spawnBehaviorNorm == "neutral")
                    {
                        var neu =
                            root.GetComponent<NeutralSpawnPositon>()
                            ?? root.AddComponent<NeutralSpawnPositon>();
                        spawner.spawnResolverBehaviour = neu;
                    }
                }
                if (autoApplyCompatibleModifiers)
                    ForwardModifiersToSpawner(spawner, instruction, p);
                return;
            }

            for (int i = 0; i < count; i++)
            {
                var spawnPos = ownerT != null ? ownerT.position : root.transform.position;
                var shell = new SpawnHelpers.PayloadShellOptions
                {
                    parent = root.transform,
                    position = spawnPos,
                    layer = ownerT != null ? ownerT.gameObject.layer : root.layer,
                    spriteType = spriteType,
                    customSpritePath = customPath,
                    spriteColor = projColor,
                    createCollider = true,
                    colliderRadius = 0.5f,
                    createRigidBody = true,
                    bodyType = RigidbodyType2D.Kinematic,
                    freezeRotation = false,
                    addAutoDestroy = lifetime > 0f,
                    lifetimeSeconds = lifetime > 0f ? lifetime : 0f,
                };

                var projectile = SpawnHelpers.CreatePayloadShell($"Projectile_{i}", shell);
                projectile.transform.localScale =
                    Vector3.one * Mathf.Max(0.0001f, p.projectileSize);

                AddMechanicByName(
                    projectile,
                    "Projectile",
                    new (string key, object val)[]
                    {
                        ("damage", p.projectileDamage),
                        ("destroyOnHit", p.projectileDestroyOnHit),
                        ("excludeOwner", true),
                        ("requireMobTag", true),
                        ("debugLogs", p.debugLogs || debugLogs),
                        ("disableSelfSpeed", wantOrbit),
                    }
                );

                if (wantOrbit)
                {
                    AddMechanicByName(
                        projectile,
                        "Orbit",
                        new (string key, object val)[]
                        {
                            ("radius", p.orbitRadius),
                            ("angularSpeedDeg", p.orbitSpeedDeg > 0 ? p.orbitSpeedDeg : 90f),
                            ("debugLogs", p.debugLogs || debugLogs),
                        }
                    );

                    float angle = baseAngle + (360f * i / count);
                    PlaceAtPolar(projectile.transform, angle, p.orbitRadius);
                }

                InitializeMechanics(projectile, owner, target);
                subItems.Add(projectile);
            }
        }

        internal void BuildAura(
            GameObject root,
            ItemInstruction instruction,
            ItemParams p,
            List<GameObject> subItems
        )
        {
            var aura = CreateChild("Aura", root.transform);
            var auraJson = LoadAndMergeJsonSettings("Aura");

            float jRadius = 2f;
            if (auraJson.TryGetValue("radius", out var arv))
            {
                if (arv is float fr)
                    jRadius = fr;
                else if (arv is int ir)
                    jRadius = ir;
                else if (arv is string sr && float.TryParse(sr, out var rpf))
                    jRadius = rpf;
            }
            float jInterval = 0.5f;
            if (auraJson.TryGetValue("interval", out var aiv))
            {
                if (aiv is float fiv)
                    jInterval = fiv;
                else if (aiv is int iiv)
                    jInterval = iiv;
                else if (aiv is string siv && float.TryParse(siv, out var ipf))
                    jInterval = ipf;
            }
            int jDamage = 1;
            if (auraJson.TryGetValue("damagePerInterval", out var adv))
            {
                if (adv is int ai)
                    jDamage = ai;
                else if (adv is float af)
                    jDamage = Mathf.RoundToInt(af);
                else if (adv is string asv && int.TryParse(asv, out var ai2))
                    jDamage = ai2;
            }
            bool jShowViz = true;
            if (auraJson.TryGetValue("showVisualization", out var sv))
            {
                if (sv is bool sb)
                    jShowViz = sb;
                else if (sv is string ss && bool.TryParse(ss, out var sbb))
                    jShowViz = sbb;
            }
            Color auraVizColor = new Color(0f, 0f, 0f, 0.5f);
            if (auraJson.TryGetValue("spriteColor", out var asc))
            {
                Color c;
                if (Config.TryParseColor(asc, out c))
                    auraVizColor = c;
            }

            var auraSettings = new List<(string key, object val)>
            {
                ("radius", jRadius),
                ("interval", Mathf.Max(0.01f, jInterval)),
                ("damagePerInterval", Mathf.Max(0, jDamage)),
                ("showVisualization", jShowViz),
                ("vizColor", auraVizColor),
                ("debugLogs", p.debugLogs || debugLogs),
            };

            AddMechanicByName(aura, "Aura", auraSettings.ToArray());
            aura.transform.localScale = Vector3.one;
            InitializeMechanics(aura, owner, target);
            subItems.Add(aura);
        }

        internal void BuildBeam(
            GameObject root,
            ItemInstruction instruction,
            ItemParams p,
            List<GameObject> subItems
        )
        {
            var ownerT = ResolveOwner();
            var beamJson = LoadAndMergeJsonSettings("Beam");

            Color vizColor = Color.white;
            if (beamJson.TryGetValue("spriteColor", out var sc))
            {
                Color c;
                if (Config.TryParseColor(sc, out c))
                    vizColor = c;
            }

            var beamSettings = new List<(string key, object val)>();
            if (beamJson.TryGetValue("maxDistance", out var md))
                beamSettings.Add(("maxDistance", md));
            if (beamJson.TryGetValue("speed", out var sp))
                beamSettings.Add(("speed", sp));
            if (beamJson.TryGetValue("extendSpeed", out var es))
                beamSettings.Add(("extendSpeed", es));
            if (beamJson.TryGetValue("direction", out var dir))
                beamSettings.Add(("direction", dir));
            if (beamJson.TryGetValue("radius", out var br))
                beamSettings.Add(("radius", br));
            if (beamJson.TryGetValue("beamWidth", out var bw))
                beamSettings.Add(("beamWidth", bw));
            if (beamJson.TryGetValue("damagePerInterval", out var dpi))
                beamSettings.Add(("damagePerInterval", dpi));
            if (beamJson.TryGetValue("damage", out var dmg))
                beamSettings.Add(("damage", dmg));
            if (beamJson.TryGetValue("interval", out var biv))
                beamSettings.Add(("interval", biv));
            beamSettings.Add(("requireMobTag", true));
            beamSettings.Add(("excludeOwner", true));
            beamSettings.Add(("showVisualization", true));
            beamSettings.Add(("vizColor", vizColor));
            beamSettings.Add(("debugLogs", p.debugLogs || debugLogs));

            bool spawnOnInterval = false;
            if (beamJson.TryGetValue("spawnOnInterval", out var soi))
            {
                if (soi is bool b)
                    spawnOnInterval = b;
                else if (soi is string ss && bool.TryParse(ss, out var pb))
                    spawnOnInterval = pb;
            }
            int numberToSpawn = 1;
            if (
                beamJson.TryGetValue("numberOfItemsToSpawn", out var nts)
                || beamJson.TryGetValue("NumberOfItemsToSpawn", out nts)
            )
            {
                if (nts is int ni)
                    numberToSpawn = Mathf.Max(1, ni);
                else if (nts is float nf)
                    numberToSpawn = Mathf.Max(1, Mathf.RoundToInt(nf));
                else if (nts is string ns && int.TryParse(ns, out var nsi))
                    numberToSpawn = Mathf.Max(1, nsi);
            }
            float spawnInterval = 0.5f;
            bool gotSpawnInterval = false;
            if (beamJson.TryGetValue("spawnInterval", out var spiv))
            {
                if (spiv is float fsv)
                {
                    spawnInterval = Mathf.Max(0.01f, fsv);
                    gotSpawnInterval = true;
                }
                else if (spiv is int isv)
                {
                    spawnInterval = Mathf.Max(0.01f, isv);
                    gotSpawnInterval = true;
                }
                else if (spiv is string ssv && float.TryParse(ssv, out var psv))
                {
                    spawnInterval = Mathf.Max(0.01f, psv);
                    gotSpawnInterval = true;
                }
            }
            if (!gotSpawnInterval && beamJson.TryGetValue("interval", out var iv))
            {
                if (iv is float fiv)
                    spawnInterval = Mathf.Max(0.01f, fiv);
                else if (iv is int iiv)
                    spawnInterval = Mathf.Max(0.01f, iiv);
                else if (iv is string siv && float.TryParse(siv, out var pf))
                    spawnInterval = Mathf.Max(0.01f, pf);
            }
            if (debugLogs)
            {
                Debug.Log(
                    $"[ProcederalItemGenerator] Beam spawnInterval resolved to {spawnInterval}",
                    this
                );
            }

            string spawnBehavior = null;
            if (beamJson.TryGetValue("spawnBehavior", out var sbRaw))
                spawnBehavior = (sbRaw as string)?.Trim();
            string spawnBehaviorNorm = string.IsNullOrEmpty(spawnBehavior)
                ? null
                : spawnBehavior.ToLowerInvariant();
            float spawnRadius = 0f;
            if (beamJson.TryGetValue("spawnRadius", out var srv))
            {
                if (srv is float fr)
                    spawnRadius = Mathf.Max(0f, fr);
                else if (srv is int ir)
                    spawnRadius = Mathf.Max(0f, ir);
                else if (srv is string sr && float.TryParse(sr, out var pr))
                    spawnRadius = Mathf.Max(0f, pr);
            }

            if (spawnOnInterval)
            {
                var spawner = root.AddComponent<BeamIntervalSpawner>();
                spawner.generator = this;
                spawner.owner = ownerT;
                spawner.interval = spawnInterval;
                spawner.countPerInterval = Mathf.Max(1, numberToSpawn);
                spawner.debugLogs = p.debugLogs || debugLogs;

                if (!string.IsNullOrEmpty(spawnBehaviorNorm))
                {
                    if (spawnBehaviorNorm == "chaos")
                    {
                        var chaos =
                            root.GetComponent<ChaosSpawnPosition>()
                            ?? root.AddComponent<ChaosSpawnPosition>();
                        spawner.spawnResolverBehaviour = chaos;
                    }
                    else if (spawnBehaviorNorm == "neuteral" || spawnBehaviorNorm == "neutral")
                    {
                        var neu =
                            root.GetComponent<NeutralSpawnPositon>()
                            ?? root.AddComponent<NeutralSpawnPositon>();
                        spawner.spawnResolverBehaviour = neu;
                    }
                }

                spawner.SetBeamSettings(beamSettings.ToArray());
                spawner.spawnRadius = spawnRadius;
                if (autoApplyCompatibleModifiers)
                    ForwardModifiersToSpawner(spawner, instruction, p);
                return;
            }

            var beam = CreateChild("Beam", root.transform);
            AddMechanicByName(beam, "Beam", beamSettings.ToArray());
            InitializeMechanics(beam, owner, target);
            subItems.Add(beam);
        }

        public void AddModifierToAll(List<GameObject> subItems, MechanicKind kind, ItemParams p)
        {
            if (subItems == null)
                return;
            foreach (var go in subItems)
            {
                if (go == null)
                    continue;
                switch (kind)
                {
                    case MechanicKind.RippleOnHit:
                        if (HasMechanic(go, "Whip"))
                        {
                            Log("Skipping incompatible modifier 'RippleOnHit' on Whip.");
                            break;
                        }
                        AddMechanicByName(
                            go,
                            "RippleOnHit",
                            new (string key, object val)[]
                            {
                                ("debugLogs", p.debugLogs || debugLogs),
                            }
                        );
                        break;
                    case MechanicKind.Orbit:
                        if (HasMechanic(go, "Beam"))
                        {
                            Log("Skipping incompatible modifier 'Orbit' on Beam.");
                            break;
                        }
                        if (HasMechanic(go, "Whip"))
                        {
                            Log("Skipping incompatible modifier 'Orbit' on Whip.");
                            break;
                        }
                        if (HasMechanic(go, "Ripple"))
                        {
                            Log("Skipping incompatible modifier 'Orbit' on Ripple.");
                            break;
                        }
                        if (HasMechanic(go, "Strike"))
                        {
                            Log("Skipping incompatible modifier 'Orbit' on Strike.");
                            break;
                        }
                        SetExistingMechanicSetting(go, "Projectile", "disableSelfSpeed", true);
                        AddMechanicByName(
                            go,
                            "Orbit",
                            new (string key, object val)[]
                            {
                                ("radius", p.orbitRadius),
                                ("angularSpeedDeg", p.orbitSpeedDeg > 0 ? p.orbitSpeedDeg : 90f),
                            }
                        );
                        var orbitJson = LoadAndMergeJsonSettings("Orbit");
                        if (orbitJson.TryGetValue("destroyOnHit", out var odh))
                        {
                            bool val = false;
                            if (odh is bool bt)
                                val = bt;
                            else if (odh is string s && bool.TryParse(s, out var pb))
                                val = pb;
                            SetExistingMechanicSetting(go, "Projectile", "destroyOnHit", val);
                        }
                        break;
                    case MechanicKind.Explosion:
                        AddMechanicByName(
                            go,
                            "Explosion",
                            new (string key, object val)[]
                            {
                                ("debugLogs", p.debugLogs || debugLogs),
                            }
                        );
                        break;
                    case MechanicKind.Bounce:
                        if (HasMechanic(go, "Aura"))
                        {
                            Log("Skipping incompatible modifier 'Bounce' on Aura.");
                            break;
                        }
                        if (HasMechanic(go, "Strike"))
                        {
                            Log("Skipping incompatible modifier 'Bounce' on Strike.");
                            break;
                        }
                        if (HasMechanic(go, "Whip"))
                        {
                            Log("Skipping incompatible modifier 'Bounce' on Whip.");
                            break;
                        }
                        if (HasMechanic(go, "Ripple"))
                        {
                            Log("Skipping incompatible modifier 'Bounce' on Ripple.");
                            break;
                        }
                        AddMechanicByName(go, "Bounce", new (string key, object val)[] { });
                        break;
                    case MechanicKind.Drain:
                        AddMechanicByName(
                            go,
                            "Drain",
                            new (string key, object val)[]
                            {
                                ("radius", p.drainRadius),
                                ("interval", Mathf.Max(0.01f, p.drainInterval)),
                                ("damagePerInterval", Mathf.Max(0, p.drainDamage)),
                                ("lifeStealRatio", Mathf.Clamp01(p.lifeStealRatio)),
                            }
                        );
                        break;
                    case MechanicKind.Lock:
                        AddMechanicByName(go, "Lock", new (string key, object val)[] { });
                        break;
                    case MechanicKind.Track:
                        if (HasMechanic(go, "Strike"))
                        {
                            Log("Skipping incompatible modifier 'Track' on Strike.");
                            break;
                        }
                        if (HasMechanic(go, "Ripple"))
                        {
                            Log("Skipping incompatible modifier 'Track' on Ripple.");
                            break;
                        }
                        AddMechanicByName(go, "Track", new (string key, object val)[] { });
                        break;
                    case MechanicKind.DamageOverTime:
                        if (HasMechanic(go, "Whip"))
                        {
                            Log("Skipping incompatible modifier 'DamageOverTime' on Whip.");
                            break;
                        }
                        AddMechanicByName(
                            go,
                            "DamageOverTime",
                            new (string key, object val)[]
                            {
                                ("interval", p.drainInterval),
                                ("damagePerInterval", p.drainDamage),
                            }
                        );
                        break;
                }
                InitializeMechanics(go, owner, target);
            }
        }

        internal void BuildStrike(
            GameObject root,
            ItemInstruction instruction,
            ItemParams p,
            List<GameObject> subItems
        )
        {
            var strike = CreateChild("Strike", root.transform);
            var strikeJson = LoadAndMergeJsonSettings("Strike");
            Color vizColor = Color.black;
            if (strikeJson.TryGetValue("spriteColor", out var sc))
            {
                Color c;
                if (Config.TryParseColor(sc, out c))
                    vizColor = c;
            }

            var settings = new List<(string key, object val)>();
            if (strikeJson.TryGetValue("interval", out var iv))
                settings.Add(("interval", iv));
            if (strikeJson.TryGetValue("damagePerInterval", out var dmg))
                settings.Add(("damagePerInterval", dmg));
            if (strikeJson.TryGetValue("damage", out var dmg2))
                settings.Add(("damage", dmg2));
            settings.Add(("requireMobTag", true));
            settings.Add(("excludeOwner", true));
            settings.Add(("showVisualization", true));
            settings.Add(("vizColor", vizColor));
            settings.Add(("debugLogs", p.debugLogs || debugLogs));

            AddMechanicByName(strike, "Strike", settings.ToArray());
            InitializeMechanics(strike, owner, target);
            subItems.Add(strike);
        }
    }
}
