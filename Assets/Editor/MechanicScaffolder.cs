#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEngine;
using System.Collections.Generic;

public class MechanicScaffolder : EditorWindow
{
    private const string MechanicTemplate = @"using UnityEngine;

namespace __NAMESPACE__
{
    public class __CLASS__ : MonoBehaviour, IMechanic
    {
        [Header(""Configurable Fields"")]
        public bool debugLogs = false;
        public float lifetime = 5f;

        private MechanicContext _ctx;
        private float _age;

        public void Initialize(MechanicContext ctx)
        {
            _ctx = ctx;
            _age = 0f;
            if (debugLogs) Debug.Log(""__CLASS__ initialized"");
        }

        public void Tick(float dt)
        {
            _age += dt;
            if (_age >= lifetime)
            {
                if (debugLogs) Debug.Log(""__CLASS__ lifetime ended"");
                enabled = false;
            }
        }
    }
}";

    private const string SettingsTemplate = @"using System.Collections.Generic;
using UnityEngine;

namespace __NAMESPACE__
{
    public static class __SETTINGS_CLASS__
    {
        public static void Apply(__CLASS__ comp, IDictionary<string, object> s)
        {
            if (comp == null || s == null) return;
            if (TryGet<float>(s, ""lifetime"", out var lt)) comp.lifetime = Mathf.Max(0f, lt);
            if (TryGet<bool>(s, ""debugLogs"", out var dl)) comp.debugLogs = dl;
        }

        private static bool TryGet<T>(IDictionary<string, object> s, string key, out T value)
        {
            value = default;
            if (s != null && s.TryGetValue(key, out var raw))
            {
                try
                {
                    if (raw is T tv) { value = tv; return true; }
                    if (typeof(T) == typeof(float) && raw is string fs && float.TryParse(fs, out var f)) { value = (T)(object)f; return true; }
                    if (typeof(T) == typeof(int) && raw is string isv && int.TryParse(isv, out var i)) { value = (T)(object)i; return true; }
                    if (typeof(T) == typeof(bool) && raw is string bs && bool.TryParse(bs, out var b)) { value = (T)(object)b; return true; }
                    if (typeof(T) == typeof(Color) && raw is string cs && ColorUtility.TryParseHtmlString(cs, out var c)) { value = (T)(object)c; return true; }
                } catch {}
            }
            return false;
        }
    }
}";

    private const string ReadmeTemplate = @"# __CLASS__

Scaffolded mechanic.

## Files
- __CLASS__.cs : Core behaviour implementing IMechanic.
- __SETTINGS_CLASS__.cs : Settings dictionary application helper.

## Settings Keys (initial)
- lifetime (float)
- debugLogs (bool)

Expand as needed.
";

    private string mechanicName = "New";
    private string categoryFolder = "Neuteral";
    private string basePath = "Assets/Scripts/Procederal Item Logic/Mechanics";
    private bool openAfterCreate = true;

    [MenuItem("Tools/Create Mechanic...")]
    public static void Open()
    {
        GetWindow<MechanicScaffolder>(true, "Create Mechanic", true);
    }

    private void OnGUI()
    {
        GUILayout.Label("Mechanic Scaffold Generator", EditorStyles.boldLabel);
        mechanicName = EditorGUILayout.TextField("Mechanic Name", mechanicName);
        categoryFolder = EditorGUILayout.TextField("Category Folder", categoryFolder);
        basePath = EditorGUILayout.TextField("Base Mechanics Path", basePath);
        openAfterCreate = EditorGUILayout.Toggle("Open Created Files", openAfterCreate);

        using (new EditorGUI.DisabledScope(string.IsNullOrWhiteSpace(mechanicName)))
        {
            if (GUILayout.Button("Create Mechanic"))
                CreateMechanic();
        }

        EditorGUILayout.HelpBox("Creates <Base>/<Category>/<Name>/ with Mechanic + Settings + README.", MessageType.Info);
    }

    private void CreateMechanic()
    {
        var safe = Sanitize(mechanicName);
        if (string.IsNullOrEmpty(safe))
        {
            Debug.LogError("Invalid mechanic name.");
            return;
        }

        var ns = ($"Mechanics.{categoryFolder}").Replace(' ', '_');
        var mechClass = safe + "Mechanic";
        var settingsClass = safe + "MechanicSettings";
        var dir = Path.Combine(basePath, categoryFolder, safe);
        if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);

        var mechPath = Path.Combine(dir, mechClass + ".cs");
        var settingsPath = Path.Combine(dir, settingsClass + ".cs");
        var readmePath = Path.Combine(dir, "README.md");

        if (!File.Exists(mechPath))
            File.WriteAllText(mechPath, MechanicTemplate
                .Replace("__NAMESPACE__", ns)
                .Replace("__CLASS__", mechClass));
        else
            Debug.LogWarning("Mechanic already exists: " + mechPath);

        if (!File.Exists(settingsPath))
            File.WriteAllText(settingsPath, SettingsTemplate
                .Replace("__NAMESPACE__", ns)
                .Replace("__CLASS__", mechClass)
                .Replace("__SETTINGS_CLASS__", settingsClass));

        if (!File.Exists(readmePath))
            File.WriteAllText(readmePath, ReadmeTemplate
                .Replace("__CLASS__", mechClass)
                .Replace("__SETTINGS_CLASS__", settingsClass));

        AssetDatabase.Refresh();

        if (openAfterCreate)
        {
            OpenIfExists(mechPath);
            OpenIfExists(settingsPath);
            OpenIfExists(readmePath);
        }

        Debug.Log($"Mechanic scaffold created: {dir}");
    }

    private void OpenIfExists(string path)
    {
        var asset = AssetDatabase.LoadAssetAtPath<TextAsset>(path);
        if (asset) AssetDatabase.OpenAsset(asset);
    }

    private string Sanitize(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return null;
        var list = new List<char>();
        foreach (var c in raw)
            if (char.IsLetterOrDigit(c)) list.Add(c);
        if (list.Count == 0) return null;
        if (!char.IsUpper(list[0])) list[0] = char.ToUpperInvariant(list[0]);
        return new string(list.ToArray());
    }
}
#endif
