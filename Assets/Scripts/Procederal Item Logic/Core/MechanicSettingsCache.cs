using System;
using System.Collections.Generic;

namespace Game.Procederal.Core
{
    /// Caches normalized mechanic settings to avoid repeated JSON merges and coercion at runtime.
    public sealed class MechanicSettingsCache
    {
        private readonly IMechanicCatalog _catalog;
        private readonly Dictionary<string, Dictionary<string, object>> _cache = new(
            StringComparer.OrdinalIgnoreCase
        );
        private readonly object _lock = new();

        public MechanicSettingsCache(IMechanicCatalog catalog)
        {
            _catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
        }

        public IMechanicCatalog Catalog => _catalog;

        /// Returns a clone of the cached settings for the mechanic. The optional normalizer is invoked once when the cache entry is created.
        public Dictionary<string, object> Get(
            string mechanicName,
            Action<Dictionary<string, object>> normalizer = null
        )
        {
            if (string.IsNullOrWhiteSpace(mechanicName))
                return new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);

            lock (_lock)
            {
                if (_cache.TryGetValue(mechanicName, out var cached))
                    return Clone(cached);
            }

            var merged =
                _catalog.GetMergedSettings(mechanicName)
                ?? new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);

            normalizer?.Invoke(merged);

            var stored = Clone(merged);
            lock (_lock)
            {
                _cache[mechanicName] = stored;
            }

            return Clone(stored);
        }

        public void Invalidate(string mechanicName)
        {
            if (string.IsNullOrWhiteSpace(mechanicName))
                return;
            lock (_lock)
            {
                _cache.Remove(mechanicName);
            }
        }

        public void Clear()
        {
            lock (_lock)
            {
                _cache.Clear();
            }
        }

        private static Dictionary<string, object> Clone(Dictionary<string, object> source)
        {
            return new Dictionary<string, object>(source, StringComparer.OrdinalIgnoreCase);
        }
    }
}
