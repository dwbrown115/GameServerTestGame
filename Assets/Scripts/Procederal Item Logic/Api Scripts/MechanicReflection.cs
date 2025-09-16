using System;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEngine;

namespace Game.Procederal.Api
{
    /// Helpers to resolve and add mechanic components by path or name, and set fields/properties.
    public static class MechanicReflection
    {
        /// Attempts to resolve a Type from a mechanicPath string.
        /// Accepts either a C# full type name (e.g. "Mechanics.Neuteral.AuraMechanic")
        /// or a Unity Asset path ending with the .cs filename. If a path is provided, we infer
        /// the type name from the filename and search loaded assemblies for a matching type.
        public static Type ResolveTypeFromMechanicPath(string mechanicPath)
        {
            if (string.IsNullOrWhiteSpace(mechanicPath))
                return null;

            // If it looks like a type name with dots and no path separators, try direct get
            if (!mechanicPath.Contains("/") && mechanicPath.Contains("."))
            {
                var t = Type.GetType(mechanicPath);
                if (t != null)
                    return t;
            }

            // Otherwise, assume it's a Unity path and grab the filename without extension
            var name = Path.GetFileNameWithoutExtension(mechanicPath);
            if (string.IsNullOrWhiteSpace(name))
                return null;

            // Common namespaces we expect
            string[] candidates = new string[]
            {
                name,
                $"Mechanics.Neuteral.{name}",
                $"Mechanics.Corruption.{name}",
                $"Mechanics.Purity.{name}",
                $"Game.{name}",
            };

            // Search all loaded assemblies for a matching type
            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                foreach (var cname in candidates)
                {
                    var t = asm.GetType(cname);
                    if (t != null)
                        return t;
                }
            }

            // Fallback: any type ending in name
            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                var t = asm.GetTypes().FirstOrDefault(x => x.Name == name);
                if (t != null)
                    return t;
            }

            return null;
        }

        /// Adds a component of the mechanic type to the GameObject and applies key/value settings.
        /// Keys are matched to public fields first, then writable properties. Basic conversions supported.
        public static Component AddMechanicWithSettings(
            GameObject go,
            Type mechanicType,
            (string key, object value)[] settings
        )
        {
            if (go == null || mechanicType == null)
                return null;
            var comp = go.AddComponent(mechanicType);
            if (settings != null)
            {
                // Prefer helper-based application when available
                var dict = new System.Collections.Generic.Dictionary<string, object>(
                    System.StringComparer.OrdinalIgnoreCase
                );
                foreach (var (key, value) in settings)
                {
                    if (!string.IsNullOrWhiteSpace(key))
                        dict[key] = value;
                }
                if (!ApplySettingsPreferHelper(comp, dict))
                {
                    foreach (var kv in dict)
                        ApplyMember(comp, kv.Key, kv.Value);
                }
            }
            return comp;
        }

        /// Applies settings using a mechanic-specific helper class if present.
        /// Looks for static class named "{TypeName}Settings" with a static Apply method.
        /// Supported signatures:
        ///   public static void Apply(Component comp, IDictionary<string, object> settings)
        ///   public static void Apply(<ExactType> comp, IDictionary<string, object> settings)
        public static bool ApplySettingsPreferHelper(
            Component comp,
            System.Collections.Generic.IDictionary<string, object> settings
        )
        {
            if (comp == null || settings == null)
                return false;
            var t = comp.GetType();
            string helperName1 = t.FullName + "Settings"; // same namespace + class name + "Settings"
            string helperName2 = (
                string.IsNullOrEmpty(t.Namespace) ? t.Name : t.Namespace + "." + t.Name + "Settings"
            );
            System.Type helper = null;
            foreach (var asm in System.AppDomain.CurrentDomain.GetAssemblies())
            {
                helper = asm.GetType(helperName1) ?? asm.GetType(helperName2);
                if (helper != null)
                    break;
            }
            if (helper == null)
                return false;
            // Find Apply method
            var apply = helper.GetMethod(
                "Apply",
                System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static
            );
            if (apply == null)
                return false;
            var prms = apply.GetParameters();
            if (prms.Length != 2)
                return false;
            object arg0 = comp;
            // If first param is exact mechanic type, ensure compatibility
            if (!prms[0].ParameterType.IsAssignableFrom(t))
                return false;
            // Second param must be IDictionary<string, object>
            if (!typeof(System.Collections.IDictionary).IsAssignableFrom(prms[1].ParameterType))
                return false;
            try
            {
                apply.Invoke(null, new object[] { arg0, settings });
                return true;
            }
            catch
            {
                return false;
            }
        }

        public static bool ApplyMember(Component comp, string key, object value)
        {
            if (comp == null || string.IsNullOrWhiteSpace(key))
                return false;
            var t = comp.GetType();
            // Field first
            var f = t.GetField(key, BindingFlags.Public | BindingFlags.Instance);
            if (f != null)
            {
                try
                {
                    var converted = ConvertValue(value, f.FieldType);
                    f.SetValue(comp, converted);
                    return true;
                }
                catch
                {
                    return false;
                }
            }
            // Property next
            var p = t.GetProperty(key, BindingFlags.Public | BindingFlags.Instance);
            if (p != null && p.CanWrite)
            {
                try
                {
                    var converted = ConvertValue(value, p.PropertyType);
                    p.SetValue(comp, converted);
                    return true;
                }
                catch
                {
                    return false;
                }
            }
            return false;
        }

        public static object ConvertValue(object value, Type targetType)
        {
            if (value == null)
                return null;
            if (targetType.IsInstanceOfType(value))
                return value;
            try
            {
                if (targetType == typeof(string))
                    return value.ToString();
                // UnityEngine.Color and Color32 from common string formats (#RRGGBB[AA])
                if (targetType == typeof(Color) || targetType == typeof(Color32))
                {
                    if (value is string cs && ColorUtility.TryParseHtmlString(cs, out var col))
                    {
                        if (targetType == typeof(Color))
                            return col;
                        return (Color32)col;
                    }
                }
                if (targetType == typeof(int))
                {
                    if (value is string s && int.TryParse(s, out var vi))
                        if (targetType == typeof(Color) || targetType == typeof(Color32))
                        {
                            if (value is string cs && ColorUtils.TryParse(cs, out var col))
                            {
                                if (targetType == typeof(Color))
                                    return col;
                                return (Color32)col;
                            }
                        }
                }
                if (targetType == typeof(bool))
                {
                    if (value is string s)
                    {
                        if (bool.TryParse(s, out var vb))
                            return vb;
                        if (int.TryParse(s, out var vi))
                            return vi != 0;
                    }
                    return System.Convert.ToBoolean(value);
                }
                // Enums
                if (targetType.IsEnum)
                {
                    if (value is string es)
                        return Enum.Parse(targetType, es, true);
                    return Enum.ToObject(targetType, value);
                }
            }
            catch { }
            return value;
        }
    }
}
