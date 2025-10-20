using System;
using System.Collections.Generic;
using UnityEngine;

namespace Game.Procederal.Core
{
    public static class TargetingServiceConstants
    {
        public const string DefaultMobTag = "Mob";
    }

    public interface ITargetingService
    {
        Transform FindNearestByTag(
            Transform origin,
            string tag,
            float maxDistance = Mathf.Infinity,
            bool includeInactive = false,
            Predicate<Transform> filter = null
        );

        bool TryFindNearestByTag(
            Transform origin,
            string tag,
            out Transform target,
            float maxDistance = Mathf.Infinity,
            bool includeInactive = false,
            Predicate<Transform> filter = null
        );

        Transform PickRandomByTag(
            Transform origin,
            string tag,
            float maxDistance = Mathf.Infinity,
            bool includeInactive = false,
            Predicate<Transform> filter = null
        );

        Vector2 ResolveDirectionToNearest(
            Transform origin,
            string tag,
            Vector2 fallback,
            float maxDistance = Mathf.Infinity,
            bool includeInactive = false,
            Predicate<Transform> filter = null
        );
    }

    public static class TargetingServiceExtensions
    {
        public static Transform FindNearestMob(
            this ITargetingService service,
            Transform origin,
            float maxDistance = Mathf.Infinity,
            bool includeInactive = false,
            Predicate<Transform> filter = null
        )
        {
            service ??= TargetingServiceLocator.Service;
            return service.FindNearestByTag(
                origin,
                TargetingServiceLocator.DefaultMobTag,
                maxDistance,
                includeInactive,
                filter
            );
        }

        public static bool TryFindNearestMob(
            this ITargetingService service,
            Transform origin,
            out Transform target,
            float maxDistance = Mathf.Infinity,
            bool includeInactive = false,
            Predicate<Transform> filter = null
        )
        {
            service ??= TargetingServiceLocator.Service;
            return service.TryFindNearestByTag(
                origin,
                TargetingServiceLocator.DefaultMobTag,
                out target,
                maxDistance,
                includeInactive,
                filter
            );
        }

        public static Transform PickRandomMob(
            this ITargetingService service,
            Transform origin,
            float maxDistance = Mathf.Infinity,
            bool includeInactive = false,
            Predicate<Transform> filter = null
        )
        {
            service ??= TargetingServiceLocator.Service;
            return service.PickRandomByTag(
                origin,
                TargetingServiceLocator.DefaultMobTag,
                maxDistance,
                includeInactive,
                filter
            );
        }

        public static Vector2 ResolveDirectionToNearestMob(
            this ITargetingService service,
            Transform origin,
            Vector2 fallback,
            float maxDistance = Mathf.Infinity,
            bool includeInactive = false,
            Predicate<Transform> filter = null
        )
        {
            service ??= TargetingServiceLocator.Service;
            return service.ResolveDirectionToNearest(
                origin,
                TargetingServiceLocator.DefaultMobTag,
                fallback,
                maxDistance,
                includeInactive,
                filter
            );
        }
    }

    public static class TargetingServiceLocator
    {
        private static ITargetingService _service;
        private static string _defaultMobTag = TargetingServiceConstants.DefaultMobTag;

        public static ITargetingService Service
        {
            get
            {
                if (_service == null)
                    _service = new DefaultTargetingService();
                return _service;
            }
        }

        public static string DefaultMobTag
        {
            get =>
                string.IsNullOrWhiteSpace(_defaultMobTag)
                    ? TargetingServiceConstants.DefaultMobTag
                    : _defaultMobTag;
            set =>
                _defaultMobTag = string.IsNullOrWhiteSpace(value)
                    ? TargetingServiceConstants.DefaultMobTag
                    : value.Trim();
        }

        public static void Register(ITargetingService service)
        {
            _service = service ?? new DefaultTargetingService();
        }

        public static void Reset()
        {
            _service = new DefaultTargetingService();
            _defaultMobTag = TargetingServiceConstants.DefaultMobTag;
        }
    }

    internal sealed class DefaultTargetingService : ITargetingService
    {
        private readonly List<Transform> _candidates = new List<Transform>(64);

        public Transform FindNearestByTag(
            Transform origin,
            string tag,
            float maxDistance = Mathf.Infinity,
            bool includeInactive = false,
            Predicate<Transform> filter = null
        )
        {
            if (!PopulateCandidates(origin, tag, maxDistance, includeInactive, filter))
                return null;

            Vector3 originPos = origin.position;
            float bestDist = float.PositiveInfinity;
            Transform best = null;
            for (int i = 0; i < _candidates.Count; i++)
            {
                var candidate = _candidates[i];
                float d2 = (candidate.position - originPos).sqrMagnitude;
                if (d2 < bestDist)
                {
                    bestDist = d2;
                    best = candidate;
                }
            }
            return best;
        }

        public bool TryFindNearestByTag(
            Transform origin,
            string tag,
            out Transform target,
            float maxDistance = Mathf.Infinity,
            bool includeInactive = false,
            Predicate<Transform> filter = null
        )
        {
            target = FindNearestByTag(origin, tag, maxDistance, includeInactive, filter);
            return target != null;
        }

        public Transform PickRandomByTag(
            Transform origin,
            string tag,
            float maxDistance = Mathf.Infinity,
            bool includeInactive = false,
            Predicate<Transform> filter = null
        )
        {
            if (!PopulateCandidates(origin, tag, maxDistance, includeInactive, filter))
                return null;

            int count = _candidates.Count;
            if (count == 0)
                return null;
            int idx = UnityEngine.Random.Range(0, count);
            return _candidates[idx];
        }

        public Vector2 ResolveDirectionToNearest(
            Transform origin,
            string tag,
            Vector2 fallback,
            float maxDistance = Mathf.Infinity,
            bool includeInactive = false,
            Predicate<Transform> filter = null
        )
        {
            var target = FindNearestByTag(origin, tag, maxDistance, includeInactive, filter);
            if (target == null)
            {
                if (fallback.sqrMagnitude > 1e-6f)
                    return fallback.normalized;
                return Vector2.right;
            }

            Vector2 delta = target.position - origin.position;
            if (delta.sqrMagnitude < 1e-6f)
                return fallback.sqrMagnitude > 1e-6f ? fallback.normalized : Vector2.right;
            return delta.normalized;
        }

        private bool PopulateCandidates(
            Transform origin,
            string tag,
            float maxDistance,
            bool includeInactive,
            Predicate<Transform> filter
        )
        {
            _candidates.Clear();
            if (origin == null || string.IsNullOrWhiteSpace(tag))
                return false;

            float maxDistSq =
                float.IsInfinity(maxDistance) || maxDistance <= 0f
                    ? float.PositiveInfinity
                    : maxDistance * maxDistance;

            if (!includeInactive)
            {
                var objects = GameObject.FindGameObjectsWithTag(tag);
                if (objects == null || objects.Length == 0)
                    return false;
                for (int i = 0; i < objects.Length; i++)
                {
                    var go = objects[i];
                    if (go == null)
                        continue;
                    var t = go.transform;
                    if (!IsCandidateValid(t, origin, maxDistSq, filter))
                        continue;
                    _candidates.Add(t);
                }
            }
            else
            {
                var transforms = Resources.FindObjectsOfTypeAll<Transform>();
                if (transforms == null || transforms.Length == 0)
                    return false;
                for (int i = 0; i < transforms.Length; i++)
                {
                    var t = transforms[i];
                    if (t == null || !t.CompareTag(tag))
                        continue;
                    if (!IsCandidateValid(t, origin, maxDistSq, filter))
                        continue;
                    _candidates.Add(t);
                }
            }

            return _candidates.Count > 0;
        }

        private static bool IsCandidateValid(
            Transform candidate,
            Transform origin,
            float maxDistSq,
            Predicate<Transform> filter
        )
        {
            if (candidate == null || candidate == origin)
                return false;

            Vector3 diff = candidate.position - origin.position;
            if (maxDistSq < float.PositiveInfinity && diff.sqrMagnitude > maxDistSq)
                return false;

            if (filter != null && !filter(candidate))
                return false;

            return true;
        }
    }

    [DisallowMultipleComponent]
    public class TargetingServiceBehaviour : MonoBehaviour, ITargetingService
    {
        [Tooltip("Default tag considered an enemy when using mob helper methods.")]
        public string mobTag = TargetingServiceConstants.DefaultMobTag;

        [Tooltip("Include inactive objects when searching.")]
        public bool includeInactive = false;

        [Tooltip(
            "When enabled, registers this behaviour as the global targeting service on Awake."
        )]
        public bool registerGlobal = true;

        private DefaultTargetingService _impl;

        private void Awake()
        {
            _impl = new DefaultTargetingService();
            if (!string.IsNullOrWhiteSpace(mobTag))
                TargetingServiceLocator.DefaultMobTag = mobTag.Trim();
            if (registerGlobal)
                TargetingServiceLocator.Register(this);
        }

        private void OnDestroy()
        {
            if (registerGlobal && ReferenceEquals(TargetingServiceLocator.Service, this))
            {
                TargetingServiceLocator.Reset();
            }
        }

        public Transform FindNearestByTag(
            Transform origin,
            string tag,
            float maxDistance = Mathf.Infinity,
            bool includeInactiveObjects = false,
            Predicate<Transform> filter = null
        )
        {
            return _impl.FindNearestByTag(
                origin,
                tag,
                maxDistance,
                includeInactiveObjects || includeInactive,
                filter
            );
        }

        public bool TryFindNearestByTag(
            Transform origin,
            string tag,
            out Transform target,
            float maxDistance = Mathf.Infinity,
            bool includeInactiveObjects = false,
            Predicate<Transform> filter = null
        )
        {
            return _impl.TryFindNearestByTag(
                origin,
                tag,
                out target,
                maxDistance,
                includeInactiveObjects || includeInactive,
                filter
            );
        }

        public Transform PickRandomByTag(
            Transform origin,
            string tag,
            float maxDistance = Mathf.Infinity,
            bool includeInactiveObjects = false,
            Predicate<Transform> filter = null
        )
        {
            return _impl.PickRandomByTag(
                origin,
                tag,
                maxDistance,
                includeInactiveObjects || includeInactive,
                filter
            );
        }

        public Vector2 ResolveDirectionToNearest(
            Transform origin,
            string tag,
            Vector2 fallback,
            float maxDistance = Mathf.Infinity,
            bool includeInactiveObjects = false,
            Predicate<Transform> filter = null
        )
        {
            return _impl.ResolveDirectionToNearest(
                origin,
                tag,
                fallback,
                maxDistance,
                includeInactiveObjects || includeInactive,
                filter
            );
        }
    }
}
