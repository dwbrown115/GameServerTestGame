using System.Collections.Generic;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Game.Procederal.Core
{
    /// Attachable to the player to automatically recycle pooled payloads that stray too far.
    [DisallowMultipleComponent]
    public sealed class AutoDespawnByDistance : MonoBehaviour
    {
        [Tooltip(
            "Generator whose payloads should be monitored. Defaults to first found on this object or its children."
        )]
        public ProcederalItemGenerator generator;

        [Tooltip("Maximum allowed distance before a payload is released back to the pool.")]
        public float maxDistance = 25f;

        [Tooltip("Seconds between range checks (minimum 0.05).")]
        public float checkInterval = 0.25f;

        [Tooltip("Log when payloads are released by this watcher (editor aid).")]
        public bool debugLogs = false;

        private readonly List<GeneratedObjectHandle> _handles = new List<GeneratedObjectHandle>(
            128
        );
        private float _timer;

        private void Awake()
        {
            if (generator == null)
                generator = GetComponentInChildren<ProcederalItemGenerator>();
        }

        private void OnEnable()
        {
            _timer = 0f;
        }

        private void OnDrawGizmosSelected()
        {
            float radius = maxDistance;
            if (radius <= 0f)
                return;

            Vector3 origin = ResolveOriginGizmo();
            const int segments = 64;
            Gizmos.color = new Color(0f, 0.75f, 1f, 0.4f);
            Vector3 prev = origin + new Vector3(radius, 0f, 0f);
            for (int i = 1; i <= segments; i++)
            {
                float angle = (Mathf.PI * 2f / segments) * i;
                Vector3 next =
                    origin + new Vector3(Mathf.Cos(angle) * radius, Mathf.Sin(angle) * radius, 0f);
                Gizmos.DrawLine(prev, next);
                prev = next;
            }

#if UNITY_EDITOR
            UnityEditor.Handles.color = new Color(0f, 0.75f, 1f, 0.15f);
            UnityEditor.Handles.DrawSolidDisc(origin, Vector3.forward, radius);
#endif
        }

        private void Update()
        {
            if (generator == null || maxDistance <= 0f)
                return;

            _timer += Time.deltaTime;
            if (_timer < Mathf.Max(0.05f, checkInterval))
                return;

            _timer = 0f;
            CullOutOfRange();
        }

        private void CullOutOfRange()
        {
            GeneratedObjectHandle.CopyActive(_handles);
            if (_handles.Count == 0)
                return;

            Vector3 origin = ResolveOrigin();
            float sqrMax = maxDistance * maxDistance;

            for (int i = 0; i < _handles.Count; i++)
            {
                var handle = _handles[i];
                if (handle == null)
                    continue;
                if (!ReferenceEquals(handle.Owner, generator))
                    continue;

                Transform payloadTransform = handle.transform;
                float sqrDistance = (payloadTransform.position - origin).sqrMagnitude;
                if (sqrDistance <= sqrMax)
                    continue;

                if (debugLogs)
                {
                    Debug.Log(
                        $"[AutoDespawnByDistance] Releasing '{payloadTransform.name}' (sqrDistance={sqrDistance:F2}).",
                        handle
                    );
                }

                generator.ReleaseTree(payloadTransform.gameObject);
            }
        }

        private Vector3 ResolveOrigin()
        {
            if (generator != null && generator.owner != null)
                return generator.owner.position;
            return transform.position;
        }

        private Vector3 ResolveOriginGizmo()
        {
            if (generator != null && generator.owner != null)
                return generator.owner.position;
            return Application.isPlaying ? transform.position : transform.position;
        }

        private void OnValidate()
        {
            if (maxDistance < 0f)
                maxDistance = 0f;
            if (checkInterval < 0.05f)
                checkInterval = 0.05f;
        }
    }
}
