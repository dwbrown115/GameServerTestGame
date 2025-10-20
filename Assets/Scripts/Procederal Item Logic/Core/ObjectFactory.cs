using System;
using System.Collections.Generic;
using UnityEngine;

namespace Game.Procederal.Core
{
    /// Provides creation and release hooks so the generator can reuse payload GameObjects.
    public interface IItemObjectFactory
    {
        GameObject Acquire(string key, Transform parent, bool worldPositionStays = false);
        void Release(GameObject instance);
    }

    public interface IItemObjectFactoryProvider
    {
        IItemObjectFactory Factory { get; }
    }

    public interface IItemObjectFactoryInitializer
    {
        void EnsureReady();
    }

    public interface IPooledPayloadResettable
    {
        void ResetForPool();
    }

    public static class ItemObjectFactoryLocator
    {
        private static IItemObjectFactory _factory;

        public static IItemObjectFactory Factory
        {
            get
            {
                if (_factory == null)
                    _factory = new DefaultItemObjectFactory();
                return _factory;
            }
        }

        public static void Register(IItemObjectFactory factory)
        {
            _factory = factory ?? new DefaultItemObjectFactory();
        }

        public static void Reset()
        {
            _factory = new DefaultItemObjectFactory();
        }
    }

    internal sealed class DefaultItemObjectFactory : IItemObjectFactory
    {
        public GameObject Acquire(string key, Transform parent, bool worldPositionStays = false)
        {
            var go = new GameObject(string.IsNullOrWhiteSpace(key) ? "GeneratorItem" : key);
            if (parent != null)
                go.transform.SetParent(parent, worldPositionStays);
            return go;
        }

        public void Release(GameObject instance)
        {
            if (instance == null)
                return;
            UnityEngine.Object.Destroy(instance);
        }
    }

    /// Simple per-name pooling behaviour that can be dropped into a scene and assigned to the generator.
    [DisallowMultipleComponent]
    public class SimpleItemObjectPoolBehaviour
        : MonoBehaviour,
            IItemObjectFactory,
            IItemObjectFactoryProvider
    {
        [Tooltip(
            "Optional transform under which pooled objects are parked. Defaults to this transform."
        )]
        public Transform poolRoot;

        [Tooltip("Maximum cached instances per key (0 = unlimited).")]
        public int maxPerKey = 0;

        private readonly Dictionary<string, Stack<GameObject>> _pool = new(
            StringComparer.OrdinalIgnoreCase
        );

        private Transform PoolRoot => poolRoot != null ? poolRoot : transform;

        public IItemObjectFactory Factory => this;

        public GameObject Acquire(string key, Transform parent, bool worldPositionStays = false)
        {
            key = string.IsNullOrWhiteSpace(key) ? "GeneratorItem" : key;

            if (_pool.TryGetValue(key, out var stack))
            {
                while (stack.Count > 0)
                {
                    var candidate = stack.Pop();
                    if (candidate == null)
                        continue;

                    if (!IsReusable(candidate))
                    {
                        // Not yet cleared (Destroy pending); defer until next request
                        stack.Push(candidate);
                        break;
                    }

                    PrepareInstance(candidate, parent, worldPositionStays, key);
                    return candidate;
                }
            }

            var go = new GameObject(key);
            PrepareInstance(go, parent, worldPositionStays, key);
            return go;
        }

        public void Release(GameObject instance)
        {
            if (instance == null)
                return;

            var key = string.IsNullOrWhiteSpace(instance.name) ? "GeneratorItem" : instance.name;
            var stack = GetStack(key);
            if (maxPerKey > 0 && stack.Count >= maxPerKey)
            {
                UnityEngine.Object.Destroy(instance);
                return;
            }

            CleanupInstance(instance);
            instance.transform.SetParent(PoolRoot, worldPositionStays: false);
            instance.SetActive(false);
            stack.Push(instance);
        }

        private void PrepareInstance(
            GameObject instance,
            Transform parent,
            bool worldPositionStays,
            string key
        )
        {
            instance.name = key;
            instance.SetActive(true);
            if (parent != null)
                instance.transform.SetParent(parent, worldPositionStays);
            instance.transform.localPosition = Vector3.zero;
            instance.transform.localRotation = Quaternion.identity;
            instance.transform.localScale = Vector3.one;
        }

        private void CleanupInstance(GameObject instance)
        {
            // Schedule removal of components and children. They will be gone before the next acquire (end of frame).
            var components = instance.GetComponents<Component>();
            for (int i = 0; i < components.Length; i++)
            {
                var comp = components[i];
                if (comp == null || comp is Transform)
                    continue;
                if (comp is IPooledPayloadResettable resettable)
                {
                    resettable.ResetForPool();
                    continue;
                }
                if (Application.isPlaying)
                    Destroy(comp);
                else
                    DestroyImmediate(comp);
            }

            for (int i = instance.transform.childCount - 1; i >= 0; i--)
            {
                var child = instance.transform.GetChild(i);
                if (child == null)
                    continue;
                var childGo = child.gameObject;
                child.SetParent(null, false);
                if (Application.isPlaying)
                    Destroy(childGo);
                else
                    DestroyImmediate(childGo);
            }
        }

        private bool IsReusable(GameObject instance)
        {
            if (instance == null)
                return false;
            if (instance.transform.childCount > 0)
                return false;

            var components = instance.GetComponents<Component>();
            for (int i = 0; i < components.Length; i++)
            {
                var comp = components[i];
                if (comp == null)
                    continue;
                if (comp is Transform)
                    continue;
                return false;
            }
            return true;
        }

        private Stack<GameObject> GetStack(string key)
        {
            if (!_pool.TryGetValue(key, out var stack))
            {
                stack = new Stack<GameObject>();
                _pool[key] = stack;
            }
            return stack;
        }
    }
}
