using System.Collections.Generic;
using Game.Procederal.Api;
using Game.Procederal.Core;
using Game.Procederal.Core.Builders.Modifiers;
using UnityEngine;

namespace Game.Procederal
{
    public partial class ProcederalItemGenerator
    {
        private Transform ResolveOwner()
        {
            return owner != null ? owner : transform;
        }

        private GameObject CreateChild(string name, Transform parent)
        {
            return AcquireObject(name, parent);
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

            float startRadius = MechanicSettingNormalizer.Radius(json, "startRadius", 1f);
            float endDiameter = MechanicSettingNormalizer.Float(json, "endDiameter", 8f, 0.01f);
            float growDuration = MechanicSettingNormalizer.Duration(json, "growDuration", 1.5f);
            float edgeThickness = MechanicSettingNormalizer.Float(
                json,
                "edgeThickness",
                0.2f,
                0.01f
            );
            int damage = MechanicSettingNormalizer.Damage(json, "damage", 5);
            bool showViz = MechanicSettingNormalizer.Bool(json, "showVisualization", true);
            Color vizColor = MechanicSettingNormalizer.Color(json, "spriteColor", Color.cyan);
            bool spawnOnInterval = MechanicSettingNormalizer.Bool(json, "spawnOnInterval", false);
            int numberToSpawn = MechanicSettingNormalizer.Count(
                json,
                1,
                "numberOfItemsToSpawn",
                "NumberOfItemsToSpawn"
            );
            float interval = MechanicSettingNormalizer.Interval(json, "interval", 0.5f);

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
            int seriesCount = Mathf.Max(1, MechanicSettingNormalizer.Int(json, "seriesCount", 3));
            float intervalBetween = MechanicSettingNormalizer.Float(
                json,
                "intervalBetween",
                0.08f,
                0f
            );
            bool spawnOnInterval = MechanicSettingNormalizer.Bool(json, "spawnOnInterval", false);
            float seriesInterval = MechanicSettingNormalizer.Interval(json, "interval", 0.8f);
            float outerRadius = MechanicSettingNormalizer.Radius(json, "outerRadius", 1.5f);
            float width = MechanicSettingNormalizer.Float(json, "width", 0.5f, 0.0001f);
            float arcLen = MechanicSettingNormalizer.Float(json, "arcLengthDeg", 120f, 1f, 359f);
            bool edgeOnly = MechanicSettingNormalizer.Bool(json, "edgeOnly", true);
            float edgeThickness = MechanicSettingNormalizer.Float(
                json,
                "edgeThickness",
                0.2f,
                0.0001f
            );
            int damage = MechanicSettingNormalizer.Damage(json, "damage", 8);
            float speed = Mathf.Max(0.01f, MechanicSettingNormalizer.Speed(json, "speed", 12f));
            Color color = MechanicSettingNormalizer.Color(json, "spriteColor", Color.white);

            if (spawnOnInterval)
            {
                var spawner = root.AddComponent<SwordSlashIntervalSpawner>();
                spawner.generator = this;
                spawner.owner = ownerT;
                spawner.interval = Mathf.Max(0.01f, seriesInterval);
                spawner.seriesCount = Mathf.Max(1, seriesCount);
                spawner.intervalBetween = intervalBetween;
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
                if (
                    TargetingServiceLocator.Service.TryFindNearestMob(ownerT, out var nearest)
                    && nearest != null
                )
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
            string spriteType = MechanicSettingNormalizer.String(
                projectileJson,
                "spriteType",
                "circle"
            );
            if (string.IsNullOrEmpty(spriteType))
                spriteType = "circle";
            string customPath = MechanicSettingNormalizer.String(
                projectileJson,
                "customSpritePath",
                null
            );
            Color projColor = MechanicSettingNormalizer.Color(
                projectileJson,
                "spriteColor",
                Color.white
            );

            string spawnBehavior = MechanicSettingNormalizer.String(
                projectileJson,
                "spawnBehavior",
                null
            );
            string spawnBehaviorNorm = string.IsNullOrEmpty(spawnBehavior)
                ? null
                : spawnBehavior.ToLowerInvariant();

            bool spawnOnInterval = MechanicSettingNormalizer.Bool(
                projectileJson,
                "spawnOnInterval",
                false
            );
            int numberToSpawn = MechanicSettingNormalizer.Count(
                projectileJson,
                1,
                "numberOfItemsToSpawn",
                "NumberOfItemsToSpawn"
            );
            float spawnInterval = MechanicSettingNormalizer.Interval(
                projectileJson,
                "interval",
                0.5f,
                0.01f
            );
            float spawnRadius = MechanicSettingNormalizer.Radius(projectileJson, "radius", 0f);
            float lifetime = MechanicSettingNormalizer.Lifetime(
                projectileJson,
                "lifetime",
                -1f,
                -1f
            );
            float projSpeed = MechanicSettingNormalizer.Speed(projectileJson, "speed", -1f);

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

            float jRadius = MechanicSettingNormalizer.Radius(auraJson, "radius", 2f);
            float jInterval = MechanicSettingNormalizer.Interval(auraJson, "interval", 0.5f, 0.01f);
            int jDamage = MechanicSettingNormalizer.Damage(auraJson, "damagePerInterval", 1);
            bool jShowViz = MechanicSettingNormalizer.Bool(auraJson, "showVisualization", true);
            Color auraVizColor = MechanicSettingNormalizer.Color(
                auraJson,
                "spriteColor",
                new Color(0f, 0f, 0f, 0.5f)
            );

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

            Color vizColor = MechanicSettingNormalizer.Color(beamJson, "spriteColor", Color.white);

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

            bool spawnOnInterval = MechanicSettingNormalizer.Bool(
                beamJson,
                "spawnOnInterval",
                false
            );
            int numberToSpawn = MechanicSettingNormalizer.Count(
                beamJson,
                1,
                "numberOfItemsToSpawn",
                "NumberOfItemsToSpawn"
            );
            float spawnInterval = MechanicSettingNormalizer.Interval(
                beamJson,
                "interval",
                0.5f,
                0.01f
            );

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
            float spawnRadius = MechanicSettingNormalizer.Radius(beamJson, "spawnRadius", 0f);

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

            var strategy = ModifierStrategies.Get(kind);
            if (strategy == null)
            {
                Log($"No modifier strategy registered for '{kind}'.");
                return;
            }

            foreach (var go in subItems)
            {
                if (go == null)
                    continue;

                strategy.Apply(this, go, p);
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
            Color vizColor = MechanicSettingNormalizer.Color(
                strikeJson,
                "spriteColor",
                Color.black
            );

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
