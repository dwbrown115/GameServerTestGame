using Game.Procederal;
using Game.Procederal.Api;
using UnityEngine;

/// Example component that uses OfflineItemGeneratorApi to pick a random primary+modifier
/// and then asks ProcederalItemGenerator to build it. Attach this to the Player or a spawner.
public class SampleRandomItemSpawner : MonoBehaviour
{
    [Header("References")]
    [Tooltip(
        "Generator that will build the selected item. If left empty, we'll search on this GameObject."
    )]
    public ProcederalItemGenerator generator;

    [Tooltip(
        "Primary Mechanic List JSON (optional). If empty, we will use the generator's field or Resources."
    )]
    public TextAsset primaryMechanicListJson;

    [Tooltip(
        "Modifier Mechanic List JSON (optional). If empty, we will use the generator's field or Resources."
    )]
    public TextAsset modifierMechanicListJson;

    [Header("Spawn Settings")]
    public bool spawnOnStart = true;

    [Min(1)]
    public int subItemCount = 1;

    [Tooltip("Optional parent where generated roots will be placed.")]
    public Transform outputParent;

    [Header("Debug")]
    public bool debugLogs = false;
    public int rngSeed = 0; // 0 = non-deterministic

    private void Awake()
    {
        if (generator == null)
            generator = GetComponent<ProcederalItemGenerator>();
        if (generator == null)
            Debug.LogWarning("[SampleRandomItemSpawner] ProcederalItemGenerator was not found.");
    }

    private void Start()
    {
        if (spawnOnStart)
            SpawnRandom();
    }

    [ContextMenu("Spawn Random Item Now")]
    public void SpawnRandom()
    {
        if (generator == null)
        {
            Debug.LogWarning("[SampleRandomItemSpawner] No generator assigned.");
            return;
        }

        // Resolve JSON assets: prefer local, then generator, then Resources
        var primary =
            primaryMechanicListJson != null
                ? primaryMechanicListJson
                : (
                    generator.primaryMechanicListJson != null
                        ? generator.primaryMechanicListJson
                        : Resources.Load<TextAsset>("Primary Mechanic List")
                );
        var modifier =
            modifierMechanicListJson != null
                ? modifierMechanicListJson
                : (
                    generator.modifierMechanicListJson != null
                        ? generator.modifierMechanicListJson
                        : Resources.Load<TextAsset>("Modifier Mechanic List")
                );

        System.Random rng = (rngSeed != 0) ? new System.Random(rngSeed) : null;
        var combo = OfflineItemGeneratorApi.MakeRandom(primary, modifier, rng);

        // Allow caller to inject simple params like count
        if (combo.parameters == null)
            combo.parameters = new ItemParams();
        combo.parameters.subItemCount = Mathf.Max(1, subItemCount);

        var root = generator.Create(combo.instruction, combo.parameters, outputParent);
        if (debugLogs)
        {
            Debug.Log(
                $"[SampleRandomItemSpawner] Spawned: {combo.debug}; root={(root ? root.name : "<null>")}"
            );
        }
    }
}
