using Game.Procederal;
using Game.Procederal.Api;
using UnityEngine;

/// Central entry-point for starting item generation.
/// - If offline = true: uses OfflineItemGeneratorApi to select a random compatible primary+modifier.
/// - If offline = false: asks a provided IItemSelectionProvider (server integration) for the selection.
/// Offline is determined by GameMode.Offline when 'useGlobalGameModeToggle' is enabled (default),
/// otherwise by the local 'offlineMode' field. Then calls ProcederalItemGenerator.Create(...).
[DisallowMultipleComponent]
public class ItemGenerationController : MonoBehaviour
{
    [Header("Mode")]
    [Tooltip(
        "When enabled, reads GameMode.Offline (set by WebSocketManager) to decide offline/online."
    )]
    public bool useGlobalGameModeToggle = true;

    [Tooltip("Fallback when useGlobalGameModeToggle = false.")]
    public bool offlineMode = true;

    [Header("References")]
    public ProcederalItemGenerator generator;

    [Tooltip("Optional provider that supplies server-driven selections when offlineMode=false.")]
    public MonoBehaviour selectionProviderBehaviour; // must implement IItemSelectionProvider

    // JSON lists are sourced from the ProcederalItemGenerator (or Resources fallback).

    [Header("Spawn")]
    [Tooltip("If true, generates a single item once on Start().")]
    public bool spawnOnStart = true;

    [Header("Output")]
    [Tooltip(
        "Where to parent generated items (optional). If null, generator defaultParent or self is used."
    )]
    public Transform outputParent;

    [Header("Debug")]
    public bool debugLogs = false;
    public int rngSeed = 0;

    [Tooltip("When true (and offline), force a fixed selection instead of random.")]
    public bool debugUseFixedSelection = false;

    [Tooltip(
        "Name of the primary mechanic to force (must match JSON entry, e.g., 'Projectile', 'Aura', 'Drain', 'Beam')."
    )]
    public string debugPrimary = "";

    [Tooltip("Optional modifier mechanic names to add (must match JSON entries).")]
    public string[] debugSecondary;

    private IItemSelectionProvider _selectionProvider;

    private void Awake()
    {
        if (generator == null)
            generator = GetComponent<ProcederalItemGenerator>();
        if (selectionProviderBehaviour != null)
            _selectionProvider = selectionProviderBehaviour as IItemSelectionProvider;
    }

    private void Start()
    {
        if (spawnOnStart)
            StartGeneration();
    }

    [ContextMenu("Start Generation Now")]
    public void StartGeneration()
    {
        if (generator == null)
        {
            if (debugLogs)
                Debug.LogWarning("[ItemGenerationController] No generator assigned.");
            return;
        }

        ItemInstruction instr = null;
        ItemParams parms = null;

        bool isOffline = useGlobalGameModeToggle ? GameMode.Offline : offlineMode;

        if (isOffline)
        {
            if (debugUseFixedSelection && !string.IsNullOrWhiteSpace(debugPrimary))
            {
                // Use fixed selection specified in inspector
                instr = new ItemInstruction
                {
                    primary = debugPrimary,
                    secondary = new System.Collections.Generic.List<string>(),
                };
                if (debugSecondary != null)
                {
                    for (int i = 0; i < debugSecondary.Length; i++)
                    {
                        var s = debugSecondary[i];
                        if (!string.IsNullOrWhiteSpace(s))
                            instr.secondary.Add(s.Trim());
                    }
                }
                parms = new ItemParams();
                if (debugLogs)
                {
                    Debug.Log(
                        $"[ItemGenerationController] Debug fixed selection => primary={instr.primary} secondary=[{string.Join(",", instr.secondary)}]"
                    );
                }
            }
            else
            {
                // Resolve JSONs from generator; fallback to Resources by name
                var primary =
                    generator.primaryMechanicListJson != null
                        ? generator.primaryMechanicListJson
                        : Resources.Load<TextAsset>("Primary Mechanic List");
                var modifier =
                    generator.modifierMechanicListJson != null
                        ? generator.modifierMechanicListJson
                        : Resources.Load<TextAsset>("Modifier Mechanic List");

                System.Random rng = (rngSeed != 0) ? new System.Random(rngSeed) : null;
                var combo = OfflineItemGeneratorApi.MakeRandom(primary, modifier, rng);
                instr = combo.instruction;
                parms = combo.parameters;
                if (debugLogs)
                    Debug.Log($"[ItemGenerationController] Offline selection => {combo.debug}");
            }
        }
        else
        {
            if (_selectionProvider == null)
            {
                if (debugLogs)
                    Debug.LogWarning(
                        "[ItemGenerationController] No selection provider for online mode."
                    );
                return;
            }
            if (!_selectionProvider.TryGetSelection(out instr, out parms))
            {
                if (debugLogs)
                    Debug.LogWarning(
                        "[ItemGenerationController] Selection provider returned no selection."
                    );
                return;
            }
            if (debugLogs)
                Debug.Log(
                    $"[ItemGenerationController] Online selection => primary={instr.primary} secondary=[{string.Join(",", instr.secondary ?? new System.Collections.Generic.List<string>())}]"
                );
        }

        if (parms == null)
            parms = new ItemParams();

        var root = generator.Create(instr, parms, outputParent);
        if (debugLogs)
            Debug.Log(
                $"[ItemGenerationController] Generated root: {(root ? root.name : "<null>")}"
            );
    }
}
