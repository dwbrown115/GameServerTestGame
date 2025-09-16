using Game.Procederal;
using Game.Procederal.Api;
using UnityEngine;

/// Example online selection provider that always returns a fixed selection.
/// Attach this to any GameObject and assign it to ItemGenerationController.selectionProviderBehaviour.
public class FixedSelectionProvider : MonoBehaviour, IItemSelectionProvider
{
    [Header("Fixed Selection")]
    public string primary = "Projectile";
    public string[] secondary = new[] { "Orbit" };

    [Header("Parameters")]
    public int subItemCount = 1;
    public int projectileDamage = 10;
    public float orbitRadius = 2f;
    public float orbitSpeedDeg = 90f;

    public bool TryGetSelection(out ItemInstruction instruction, out ItemParams parameters)
    {
        instruction = new ItemInstruction
        {
            primary = primary,
            secondary = new System.Collections.Generic.List<string>(
                secondary ?? System.Array.Empty<string>()
            ),
        };
        parameters = new ItemParams
        {
            subItemCount = Mathf.Max(1, subItemCount),
            projectileDamage = projectileDamage,
            orbitRadius = orbitRadius,
            orbitSpeedDeg = orbitSpeedDeg,
        };
        return true;
    }
}
