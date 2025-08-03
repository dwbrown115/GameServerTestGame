using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// A simple marker component to identify a GameObject as a UI Panel.
/// This allows other scripts to easily find panels in the hierarchy.
/// </summary>
public class UIPanel : MonoBehaviour
{
    // This component has no logic. It's just a tag.
}

/// <summary>
/// This script can be attached to a UI Button. When clicked, it will find the
/// closest parent GameObject that has a UIPanel component and deactivate it.
/// </summary>
[RequireComponent(typeof(Button))]
public class CloseParentPanelButton : MonoBehaviour
{
    public void ClosePanel()
    {
        // Find the UIPanel component in the parents of this button.
        UIPanel panel = GetComponentInParent<UIPanel>();

        if (panel != null)
        {
            panel.gameObject.SetActive(false);
        }
        else
        {
            Debug.LogWarning(
                "Close button is not a child of a GameObject with a UIPanel component.",
                this
            );
        }
    }
}
