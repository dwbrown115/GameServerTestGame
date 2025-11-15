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
        /// Options for creating a payload shell (sprite, collider, rigidbody, timers) via <see cref="CreatePayloadShell"/>.
        /// </summary>
        public struct PayloadShellOptions
        {
            public Transform parent;
            public Vector3 position;
            public int layer;
            public string spriteType;
            public string customSpritePath;
            public Color spriteColor;
            public float uniformScale;
            public bool createCollider;
            public float colliderRadius;
            public bool createRigidBody;
            public RigidbodyType2D bodyType;
            public bool freezeRotation;
            public bool addAutoDestroy;
            public float lifetimeSeconds;
        }

        /// <summary>
        /// Creates a payload shell GameObject with standardized sprite/collider/rigidbody setup.
        /// </summary>
        public static GameObject CreatePayloadShell(string name, in PayloadShellOptions options)
        {
            var go = new GameObject(string.IsNullOrWhiteSpace(name) ? "PayloadShell" : name);
            if (options.parent != null)
                go.transform.SetParent(options.parent, worldPositionStays: true);
            go.transform.position = options.position;
            float scale = options.uniformScale > 0f ? options.uniformScale : 1f;
            go.transform.localScale = Vector3.one * scale;
            go.layer = options.layer;

            if (!string.IsNullOrWhiteSpace(options.spriteType))
            {
                var sr = go.AddComponent<SpriteRenderer>();
                sr.sprite = ResolveSprite(options.spriteType, options.customSpritePath);
                sr.color = options.spriteColor;
            }

            if (options.createCollider)
            {
                var cc = go.AddComponent<CircleCollider2D>();
                cc.isTrigger = true;
                cc.radius = Mathf.Max(0.001f, options.colliderRadius);
            }

            if (options.createRigidBody)
            {
                var rb = go.AddComponent<Rigidbody2D>();
                rb.bodyType = options.bodyType;
                rb.gravityScale = 0f;
                rb.freezeRotation = options.freezeRotation;
                rb.interpolation = RigidbodyInterpolation2D.Interpolate;
                rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
            }

            if (options.addAutoDestroy && options.lifetimeSeconds > 0f)
            {
                var timer = go.AddComponent<AutoDestroyAfterSeconds>();
                timer.seconds = options.lifetimeSeconds;
            }

            return go;
        }

        private static Sprite ResolveSprite(string spriteType, string customSpritePath)
        {
            string type = spriteType?.Trim()?.ToLowerInvariant();
            switch (type)
            {
                case "custom":
                    if (!string.IsNullOrEmpty(customSpritePath))
                    {
                        var custom = Resources.Load<Sprite>(customSpritePath);
                        if (custom != null)
                            return custom;
                    }
                    return ProcederalItemGenerator.GetUnitCircleSprite();
                case "square":
                    return ProcederalItemGenerator.GetUnitSquareSprite();
                case "circle":
                case null:
                case "":
                default:
                    return ProcederalItemGenerator.GetUnitCircleSprite();
            }
        }

        /// <summary>
        /// Spawns a fixed number of child GameObjects under <paramref name="parent"/> each with the specified
        /// primary mechanic (payloadMechanicName). Applies additional payload settings, optional sequence index
        /// tagging, and initializes mechanics through the generator.
        /// </summary>
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

    /// <summary>
    /// Shared auto-destroy timer component used by spawners.
    /// </summary>
    public sealed class AutoDestroyAfterSeconds : MonoBehaviour
    {
        public float seconds = 5f;
        private float _elapsed;

        private void Update()
        {
            _elapsed += Time.deltaTime;
            if (_elapsed >= seconds)
                Destroy(gameObject);
        }
    }
}
