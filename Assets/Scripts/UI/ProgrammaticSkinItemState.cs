using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Holds local UI-only state for a programmatic skin item.
public class ProgrammaticSkinItemState : MonoBehaviour
{
    [Header("Identity")]
    public string skinId;

    [Header("State Bools (mutually exclusive)")]
    public bool isNotOwned;
    public bool isOwned;
    public bool isActive;

    [Header("Visual Refs")]
    public Outline outline;
    public TextMeshProUGUI stateLabel;
    public Image background; // optional, used by Button targetGraphic
}
