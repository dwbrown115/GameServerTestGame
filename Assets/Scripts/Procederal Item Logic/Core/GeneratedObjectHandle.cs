using UnityEngine;

namespace Game.Procederal.Core
{
    /// Tags GameObjects created by ProcederalItemGenerator so pooled releases can be routed correctly.
    [DisallowMultipleComponent]
    public sealed class GeneratedObjectHandle : MonoBehaviour, IPooledPayloadResettable
    {
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
    }
}
