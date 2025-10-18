using System;
using System.Collections.Generic;
using UnityEngine;

namespace Game.Procederal.Ota
{
    [Serializable]
    public sealed class MechanicOtaManifestEntry
    {
        public string mechanicName;
        public string category;
        public string resourcePath;
        public string file;
        public string sourcePath;
        public string hash;
        public string attribute;

        public bool HasHash => !string.IsNullOrWhiteSpace(hash);
    }

    [Serializable]
    internal sealed class MechanicOtaManifestWrapper
    {
        public List<MechanicOtaManifestEntry> items = new List<MechanicOtaManifestEntry>();
    }

    public sealed class MechanicOtaManifest
    {
        private static readonly MechanicOtaManifest _empty = new MechanicOtaManifest(
            Array.Empty<MechanicOtaManifestEntry>()
        );

        private readonly List<MechanicOtaManifestEntry> _entries;
        private readonly Dictionary<string, MechanicOtaManifestEntry> _byName;

        public static MechanicOtaManifest Empty => _empty;

        private MechanicOtaManifest(IEnumerable<MechanicOtaManifestEntry> entries)
        {
            _entries = new List<MechanicOtaManifestEntry>();
            _byName = new Dictionary<string, MechanicOtaManifestEntry>(
                StringComparer.OrdinalIgnoreCase
            );

            foreach (var entry in entries)
            {
                if (entry == null || string.IsNullOrWhiteSpace(entry.mechanicName))
                    continue;

                var name = entry.mechanicName.Trim();
                entry.mechanicName = name;
                entry.category = entry.category?.Trim();
                entry.resourcePath = entry.resourcePath?.Trim();
                entry.file = entry.file?.Trim();
                entry.sourcePath = entry.sourcePath?.Trim();
                entry.hash = entry.hash?.Trim();
                entry.attribute = entry.attribute?.Trim();

                if (_byName.TryGetValue(name, out var existing))
                {
                    int existingIndex = _entries.IndexOf(existing);
                    if (existingIndex >= 0)
                        _entries[existingIndex] = entry;
                    _byName[name] = entry;
                }
                else
                {
                    _byName[name] = entry;
                    _entries.Add(entry);
                }
            }
        }

        public IReadOnlyList<MechanicOtaManifestEntry> Entries => _entries;

        public bool TryGetEntry(string mechanicName, out MechanicOtaManifestEntry entry)
        {
            if (string.IsNullOrWhiteSpace(mechanicName))
            {
                entry = null;
                return false;
            }

            return _byName.TryGetValue(mechanicName.Trim(), out entry);
        }

        public MechanicOtaManifest Merge(MechanicOtaManifest overlay)
        {
            if (overlay == null || ReferenceEquals(overlay, Empty) || overlay._entries.Count == 0)
                return this;

            var merged = new List<MechanicOtaManifestEntry>(_entries);
            var map = new Dictionary<string, MechanicOtaManifestEntry>(
                _byName,
                StringComparer.OrdinalIgnoreCase
            );

            foreach (var entry in overlay._entries)
            {
                if (entry == null || string.IsNullOrWhiteSpace(entry.mechanicName))
                    continue;

                var key = entry.mechanicName.Trim();
                if (map.TryGetValue(key, out var existing))
                {
                    int idx = merged.IndexOf(existing);
                    if (idx >= 0)
                        merged[idx] = entry;
                    map[key] = entry;
                }
                else
                {
                    map[key] = entry;
                    merged.Add(entry);
                }
            }

            return new MechanicOtaManifest(merged);
        }

        public static MechanicOtaManifest FromJson(string json, string origin = "inline")
        {
            if (string.IsNullOrWhiteSpace(json))
                return Empty;

            string wrapped = "{\"items\":" + json + "}";
            var wrapper = JsonUtility.FromJson<MechanicOtaManifestWrapper>(wrapped);
            if (wrapper == null || wrapper.items == null || wrapper.items.Count == 0)
                return Empty;

            return new MechanicOtaManifest(wrapper.items);
        }

        public static MechanicOtaManifest LoadFromResources(
            string resourcePath = "ProcederalMechanics/index"
        )
        {
            var asset = Resources.Load<TextAsset>(resourcePath);
            if (asset == null || string.IsNullOrWhiteSpace(asset.text))
                return Empty;
            return FromJson(asset.text, resourcePath);
        }
    }
}
