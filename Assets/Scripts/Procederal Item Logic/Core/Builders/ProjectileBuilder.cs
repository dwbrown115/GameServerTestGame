using System.Collections.Generic;
using Game.Procederal.Core;
using UnityEngine;

namespace Game.Procederal.Core.Builders
{
    // First migrated builder: handles Projectile primary construction paths (orbit, interval, static)
    public class ProjectileBuilder : IPrimaryBuilder
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
            // Inline port of BuildProjectileSet to decouple from generator
            int desired = p != null ? p.subItemCount : 1;
            if (gen.generatorChildrenCount > 0)
                desired = gen.generatorChildrenCount;
            int count = Mathf.Max(1, desired);
            bool wantOrbit = instruction.HasSecondary(Game.Procederal.MechanicKind.Orbit);

            var projectileJson = gen.LoadAndMergeJsonSettings("Projectile");
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

            // Spawn behavior selection from JSON (e.g., neuteral | neutral | chaos | orbit)
            string spawnBehavior = null;
            if (projectileJson.TryGetValue("spawnBehavior", out var sbRaw))
                spawnBehavior = (sbRaw as string)?.Trim();
            string spawnBehaviorNorm = string.IsNullOrEmpty(spawnBehavior)
                ? null
                : spawnBehavior.ToLowerInvariant();

            // Projectile spawn behavior from JSON
            bool spawnOnInterval = false;
            if (projectileJson.TryGetValue("spawnOnInterval", out var soi))
            {
                if (soi is bool b)
                    spawnOnInterval = b;
                else if (soi is string ss && bool.TryParse(ss, out var pb))
                    spawnOnInterval = pb;
            }
            int numberToSpawn = 1;
            // Accept both camelCase and original key
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
                var orbitOverrides = gen.LoadKvpArrayForMechanic("Orbit", "MechanicOverrides");
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

            // (Deprecated orbit spawner path removed) orbit now handled via static spawn + OrbitMechanic.

            if (spawnOnInterval)
            {
                var spawner = root.AddComponent<Game.Procederal.Api.IntervalSpawner>();
                spawner.generator = gen;
                spawner.owner = gen.owner != null ? gen.owner : gen.transform;
                spawner.interval = spawnInterval;
                int intervalCount =
                    gen.generatorChildrenCount > 0 ? gen.generatorChildrenCount : numberToSpawn;
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
                spawner.debugLogs = p.debugLogs || gen.debugLogs;
                if (!string.IsNullOrEmpty(spawnBehaviorNorm))
                {
                    if (spawnBehaviorNorm == "chaos")
                    {
                        var chaos =
                            root.GetComponent<Game.Procederal.Api.ChaosSpawnPosition>()
                            ?? root.AddComponent<Game.Procederal.Api.ChaosSpawnPosition>();
                        spawner.spawnResolverBehaviour = chaos;
                    }
                    else if (spawnBehaviorNorm == "neuteral" || spawnBehaviorNorm == "neutral")
                    {
                        var neu =
                            root.GetComponent<Game.Procederal.Api.NeutralSpawnPositon>()
                            ?? root.AddComponent<Game.Procederal.Api.NeutralSpawnPositon>();
                        spawner.spawnResolverBehaviour = neu;
                    }
                }
                if (gen.autoApplyCompatibleModifiers)
                    gen.ForwardModifiersToSpawner(spawner, instruction, p);
                return;
            }

            for (int i = 0; i < count; i++)
            {
                var go = new GameObject($"Projectile_{i}");
                go.transform.SetParent(root.transform, false);
                go.transform.localPosition = Vector3.zero;
                float visualScale = p.projectileSize;
                go.transform.localScale = Vector3.one * Mathf.Max(0.0001f, visualScale);
                var sr = go.AddComponent<SpriteRenderer>();
                Sprite chosen = null;
                switch ((spriteType ?? "circle").ToLowerInvariant())
                {
                    case "custom":
                        if (!string.IsNullOrEmpty(customPath))
                            chosen = Resources.Load<Sprite>(customPath);
                        if (chosen == null)
                            chosen = Game.Procederal.ProcederalItemGenerator.GetUnitCircleSprite();
                        break;
                    case "square":
                        chosen = Game.Procederal.ProcederalItemGenerator.GetUnitSquareSprite();
                        break;
                    case "circle":
                    default:
                        chosen = Game.Procederal.ProcederalItemGenerator.GetUnitCircleSprite();
                        break;
                }
                sr.sprite = chosen;
                sr.color = projColor;
                sr.sortingOrder = 0;

                var cc = go.AddComponent<CircleCollider2D>();
                cc.isTrigger = true;
                cc.radius = 0.5f;

                if (go.GetComponent<Rigidbody2D>() == null)
                {
                    var rb = go.AddComponent<Rigidbody2D>();
                    rb.bodyType = RigidbodyType2D.Kinematic;
                    rb.interpolation = RigidbodyInterpolation2D.Interpolate;
                    rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
                }

                gen.AddMechanicByName(
                    go,
                    "Projectile",
                    new (string key, object val)[]
                    {
                        ("damage", p.projectileDamage),
                        ("destroyOnHit", p.projectileDestroyOnHit),
                        ("excludeOwner", true),
                        ("requireMobTag", true),
                        ("debugLogs", p.debugLogs || gen.debugLogs),
                        ("disableSelfSpeed", wantOrbit),
                    }
                );

                if (wantOrbit)
                {
                    gen.AddMechanicByName(
                        go,
                        "Orbit",
                        new (string key, object val)[]
                        {
                            ("radius", p.orbitRadius),
                            ("angularSpeedDeg", p.orbitSpeedDeg > 0 ? p.orbitSpeedDeg : 90f),
                            ("debugLogs", p.debugLogs || gen.debugLogs),
                        }
                    );

                    float angle = p.startAngleDeg + (360f * i / count);
                    var a = angle * Mathf.Deg2Rad;
                    var pos = new Vector3(
                        Mathf.Cos(a) * p.orbitRadius,
                        Mathf.Sin(a) * p.orbitRadius,
                        0f
                    );
                    go.transform.localPosition = pos;
                }

                gen.InitializeMechanics(go, gen.owner, gen.target);
                subItems.Add(go);
            }
        }
    }
}
