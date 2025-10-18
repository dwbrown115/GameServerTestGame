using System;
using System.Collections.Generic;
using UnityEngine;

namespace Game.Procederal.Api
{
    /// Lightweight registry that maps MechanicName -> MechanicPath from the two JSON lists.
    /// Does not enforce schema beyond those two fields, and tolerates extra fields.
    public class MechanicCatalog
    {
        [Serializable]
        private class PrimaryEntry
        {
            public string MechanicName = string.Empty;
            public string MechanicPath = string.Empty;
        }

        [Serializable]
        private class ModifierEntry
        {
            public string MechanicName = string.Empty;
            public string MechanicPath = string.Empty;
        }

        [Serializable]
        private class WrapperP
        {
            public List<PrimaryEntry> Items = new();
        }

        [Serializable]
        private class WrapperM
        {
            public List<ModifierEntry> Items = new();
        }

        [Serializable]
        private class Wrapper<T>
        {
            public List<T> Items = new();
        }

        private readonly Dictionary<string, string> _nameToPath = new(
            StringComparer.OrdinalIgnoreCase
        );

        public static MechanicCatalog Load(TextAsset primaryJson, TextAsset modifierJson)
        {
            if (
                (primaryJson == null || string.IsNullOrWhiteSpace(primaryJson.text))
                && (modifierJson == null || string.IsNullOrWhiteSpace(modifierJson.text))
            )
                return null;
            var cat = new MechanicCatalog();
            if (primaryJson != null && !string.IsNullOrWhiteSpace(primaryJson.text))
                cat.AddPrimaryList(primaryJson.text);
            if (modifierJson != null && !string.IsNullOrWhiteSpace(modifierJson.text))
                cat.AddModifierList(modifierJson.text);
            return cat;
        }

        public bool TryGetPath(string mechanicName, out string mechanicPath)
        {
            mechanicPath = null;
            if (string.IsNullOrWhiteSpace(mechanicName))
                return false;
            return _nameToPath.TryGetValue(mechanicName, out mechanicPath);
        }

        private void AddPrimaryList(string json)
        {
            var list = FromJsonArray<PrimaryEntry>(json);
            foreach (var e in list)
            {
                if (e == null || string.IsNullOrWhiteSpace(e.MechanicName))
                    continue;
                if (!string.IsNullOrWhiteSpace(e.MechanicPath))
                    _nameToPath[e.MechanicName] = e.MechanicPath;
            }
        }

        private void AddModifierList(string json)
        {
            var list = FromJsonArray<ModifierEntry>(json);
            foreach (var e in list)
            {
                if (e == null || string.IsNullOrWhiteSpace(e.MechanicName))
                    continue;
                if (!string.IsNullOrWhiteSpace(e.MechanicPath))
                    _nameToPath[e.MechanicName] = e.MechanicPath;
            }
        }

        private static List<T> FromJsonArray<T>(string json)
        {
            string wrapped = "{\"Items\":" + json + "}";
            var w = JsonUtility.FromJson<Wrapper<T>>(wrapped);
            return w != null && w.Items != null ? w.Items : new List<T>();
        }
    }
}
