using System;
using System.Collections.Generic;
using UnityEngine;

namespace Game.Procederal.Core.Builders
{
    /// <summary>
    /// Central helper that assembles child payload objects from shared specs so individual builders only describe intent.
    /// </summary>
    public static class UnifiedChildBuilder
    {
        public enum ColliderShape2D
        {
            None = 0,
            Circle,
        }

        public struct SpriteSpec
        {
            public bool Enabled;
            public string SpriteType;
            public string CustomSpritePath;
            public Color Color;
            public int SortingOrder;
        }

        public struct ColliderSpec
        {
            public bool Enabled;
            public ColliderShape2D Shape;
            public float Radius;
            public Vector2 Offset;
            public bool IsTrigger;
        }

        public struct RigidbodySpec
        {
            public bool Enabled;
            public RigidbodyType2D BodyType;
            public bool FreezeRotation;
            public float GravityScale;
            public RigidbodyInterpolation2D Interpolation;
            public CollisionDetectionMode2D CollisionDetection;
        }

        public struct MechanicSpec
        {
            public string Name;
            public (string key, object val)[] Settings;
            public bool SkipIfPresent;
        }

        public sealed class ChildSpec
        {
            public string ChildName;
            public Transform Parent;
            public Vector3? LocalPosition;
            public Quaternion? LocalRotation;
            public Vector3? LocalScale;
            public int? Layer;
            public SpriteSpec? Visual;
            public ColliderSpec? Collider;
            public RigidbodySpec? Rigidbody;
            public List<MechanicSpec> Mechanics;
            public Transform OwnerOverride;
            public Transform TargetOverride;
            public List<Action<GameObject>> Mutators;
            public float? LifetimeSeconds;
            public bool InitializeMechanics = true;
        }

        public static GameObject BuildChild(
            Game.Procederal.ProcederalItemGenerator gen,
            ChildSpec spec
        )
        {
            if (gen == null)
                throw new ArgumentNullException(nameof(gen));
            if (spec == null)
                throw new ArgumentNullException(nameof(spec));

            var parent = spec.Parent != null ? spec.Parent : gen.transform;
            var childName = string.IsNullOrWhiteSpace(spec.ChildName)
                ? "GeneratedChild"
                : spec.ChildName;
            var child = gen.CreateChild(childName, parent);

            var t = child.transform;
            t.localPosition = spec.LocalPosition ?? Vector3.zero;
            t.localRotation = spec.LocalRotation ?? Quaternion.identity;
            t.localScale = spec.LocalScale ?? Vector3.one;
            child.layer = spec.Layer ?? parent.gameObject.layer;

            if (spec.Visual.HasValue)
                ApplySprite(child, spec.Visual.Value);
            if (spec.Collider.HasValue)
                ApplyCollider(child, spec.Collider.Value);
            if (spec.Rigidbody.HasValue)
                ApplyRigidbody(child, spec.Rigidbody.Value);
            if (spec.LifetimeSeconds.HasValue)
                ApplyLifetime(child, spec.LifetimeSeconds.Value);

            if (spec.Mutators != null)
            {
                for (int i = 0; i < spec.Mutators.Count; i++)
                {
                    var mutator = spec.Mutators[i];
                    mutator?.Invoke(child);
                }
            }

            if (spec.Mechanics != null)
            {
                for (int i = 0; i < spec.Mechanics.Count; i++)
                {
                    var mechanic = spec.Mechanics[i];
                    if (string.IsNullOrWhiteSpace(mechanic.Name))
                        continue;
                    if (mechanic.SkipIfPresent && gen.HasMechanic(child, mechanic.Name))
                        continue;
                    var payload = mechanic.Settings ?? Array.Empty<(string key, object val)>();
                    gen.AddMechanicByName(child, mechanic.Name, payload);
                }
            }

            if (spec.InitializeMechanics)
            {
                var owner = spec.OwnerOverride != null ? spec.OwnerOverride : gen.owner;
                var target =
                    spec.TargetOverride != null
                        ? spec.TargetOverride
                        : gen.ResolveTargetOrDefault();
                gen.InitializeMechanics(child, owner, target);
            }

            return child;
        }

        public static List<GameObject> BuildChildren(
            Game.Procederal.ProcederalItemGenerator gen,
            IEnumerable<ChildSpec> specs
        )
        {
            if (gen == null)
                throw new ArgumentNullException(nameof(gen));
            if (specs == null)
                throw new ArgumentNullException(nameof(specs));
            var results = new List<GameObject>();
            foreach (var spec in specs)
            {
                if (spec == null)
                    continue;
                results.Add(BuildChild(gen, spec));
            }
            return results;
        }

        private static void ApplySprite(GameObject child, SpriteSpec visual)
        {
            var sr = child.GetComponent<SpriteRenderer>();
            if (!visual.Enabled)
            {
                if (sr != null)
                    sr.enabled = false;
                return;
            }

            if (sr == null)
                sr = child.AddComponent<SpriteRenderer>();
            sr.enabled = true;
            sr.sprite = ResolveSprite(visual.SpriteType, visual.CustomSpritePath);
            sr.color = visual.Color;
            sr.sortingOrder = visual.SortingOrder;
        }

        private static void ApplyCollider(GameObject child, ColliderSpec spec)
        {
            if (!spec.Enabled)
            {
                var existing = child.GetComponent<Collider2D>();
                if (existing != null)
                    existing.enabled = false;
                return;
            }

            switch (spec.Shape)
            {
                case ColliderShape2D.Circle:
                    var circle = child.GetComponent<CircleCollider2D>();
                    if (circle == null)
                        circle = child.AddComponent<CircleCollider2D>();
                    if (circle == null)
                    {
                        Debug.LogError(
                            "UnifiedChildBuilder could not add CircleCollider2D to child payload."
                        );
                        return;
                    }
                    circle.enabled = true;
                    circle.isTrigger = spec.IsTrigger;
                    circle.offset = spec.Offset;
                    circle.radius = Mathf.Max(0.0001f, spec.Radius);
                    break;
                case ColliderShape2D.None:
                default:
                    var col = child.GetComponent<Collider2D>();
                    if (col != null)
                        col.enabled = false;
                    break;
            }
        }

        private static void ApplyRigidbody(GameObject child, RigidbodySpec spec)
        {
            var rb = child.GetComponent<Rigidbody2D>();
            if (!spec.Enabled)
            {
                if (rb != null)
                    rb.simulated = false;
                return;
            }

            if (rb == null)
                rb = child.AddComponent<Rigidbody2D>();
            rb.simulated = true;
            rb.bodyType = spec.BodyType;
            rb.freezeRotation = spec.FreezeRotation;
            rb.gravityScale = spec.GravityScale;
            rb.interpolation = spec.Interpolation;
            rb.collisionDetectionMode = spec.CollisionDetection;
        }

        private static void ApplyLifetime(GameObject child, float seconds)
        {
            if (seconds <= 0f)
                return;
            var timer =
                child.GetComponent<Game.Procederal.Api.AutoDestroyAfterSeconds>()
                ?? child.AddComponent<Game.Procederal.Api.AutoDestroyAfterSeconds>();
            timer.seconds = seconds;
        }

        private static Sprite ResolveSprite(string spriteType, string customPath)
        {
            var type = spriteType?.Trim()?.ToLowerInvariant();
            switch (type)
            {
                case "custom":
                    if (!string.IsNullOrEmpty(customPath))
                    {
                        var custom = Resources.Load<Sprite>(customPath);
                        if (custom != null)
                            return custom;
                    }
                    return Game.Procederal.ProcederalItemGenerator.GetUnitCircleSprite();
                case "square":
                    return Game.Procederal.ProcederalItemGenerator.GetUnitSquareSprite();
                case "circle":
                case null:
                case "":
                default:
                    return Game.Procederal.ProcederalItemGenerator.GetUnitCircleSprite();
            }
        }
    }
}
