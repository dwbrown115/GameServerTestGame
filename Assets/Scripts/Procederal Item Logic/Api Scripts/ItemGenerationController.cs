using System;
using Game.Procederal;
using Game.Procederal.Api;
using UnityEngine;

[Serializable]
public class ChildBehaviorConfig
{
    [Header("Movement")]
    [Tooltip(
        "Movement override applied to generated children. Defaults to Shoot (standard behavior)."
    )]
    public ChildBehaviorSelection movement = ChildBehaviorSelection.Shoot;

    [Tooltip(
        "Optional orbit path identifier (e.g., 'Circular'); used when movement engages orbit mechanics."
    )]
    public string orbitPath = "Circular";
}

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

    [Header("Child Behavior")]
    [Tooltip(
        "If > 0, force number of children to spawn per primary (overrides ItemParams.subItemCount)."
    )]
    public int overrideChildrenCount = 0;

    [Tooltip("Overrides for generated children (movement is required; defaults to Shoot).")]
    public ChildBehaviorConfig childBehavior = new ChildBehaviorConfig();

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
                // Resolve JSON overrides from generator; Offline API will load Resources catalogs if null
                var primary = generator.primaryMechanicListJson;
                var modifier = generator.modifierMechanicListJson;

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

        AppendDebugSecondary(instr);

        if (parms == null)
            parms = new ItemParams();

        var behaviorOverrides = ChildBehaviorOverrides.Default;
        if (childBehavior != null)
        {
            var movement = childBehavior.movement;
            if (movement == ChildBehaviorSelection.Unspecified)
                movement = ChildBehaviorOverrides.Default.movement;
            behaviorOverrides.movement = movement;

            if (!string.IsNullOrWhiteSpace(childBehavior.orbitPath))
                behaviorOverrides.orbitPath = childBehavior.orbitPath.Trim();
            else
                behaviorOverrides.orbitPath = ChildBehaviorOverrides.Default.orbitPath;
        }

        parms.childBehavior = behaviorOverrides;

        // Apply inspector override for per-primary children count when present
        if (parms != null && overrideChildrenCount > 0)
            parms.subItemCount = Mathf.Max(1, overrideChildrenCount);

        var root = generator.Create(instr, parms, outputParent);
        if (debugLogs)
            Debug.Log(
                $"[ItemGenerationController] Generated root: {(root ? root.name : "<null>")}"
            );
    }

    private void AppendDebugSecondary(ItemInstruction instr)
    {
        if (instr == null || debugSecondary == null || debugSecondary.Length == 0)
            return;

        if (instr.secondary == null)
            instr.secondary = new System.Collections.Generic.List<string>();

        for (int i = 0; i < debugSecondary.Length; i++)
        {
            var entry = debugSecondary[i];
            if (string.IsNullOrWhiteSpace(entry))
                continue;

            var trimmed = entry.Trim();
            if (!instr.secondary.Contains(trimmed))
                instr.secondary.Add(trimmed);
        }
    }
}
