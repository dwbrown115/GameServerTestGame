using System.Collections.Generic;
using UnityEngine;

namespace Game.Procederal.Core
{
    /// Tags GameObjects created by ProcederalItemGenerator so pooled releases can be routed correctly.
    [DisallowMultipleComponent]
    public sealed class GeneratedObjectHandle : MonoBehaviour, IPooledPayloadResettable
    {
        private static readonly List<GeneratedObjectHandle> _active =
            new List<GeneratedObjectHandle>();

        public ProcederalItemGenerator Owner { get; private set; }
        public string Key { get; private set; } = string.Empty;

        public void Initialize(ProcederalItemGenerator owner, string key)
        {
            Owner = owner;
            Key = string.IsNullOrWhiteSpace(key) ? string.Empty : key;
        }

        public void ResetForPool()
        {
            Owner = null;
            Key = string.Empty;
        }

        private void OnEnable()
        {
            _active.Add(this);
        }

        private void OnDisable()
        {
            _active.Remove(this);
        }

        internal static void CopyActive(List<GeneratedObjectHandle> buffer)
        {
            if (buffer == null)
                return;
            buffer.Clear();
            buffer.AddRange(_active);
        }
    }
}
