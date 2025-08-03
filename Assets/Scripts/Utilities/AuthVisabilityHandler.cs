using UnityEngine;

/// <summary>
/// Controls the visibility of a GameObject based on the user's authentication state. Call RefreshVisibility() to update the object's state.
/// </summary>
public class AuthVisibilityHandler : MonoBehaviour
{
    [Tooltip(
        "If true, this object will be active when the user is logged IN. If false, it will be active when they are logged OUT."
    )]
    public bool showWhenLoggedIn = true;

    private void OnEnable()
    {
        // Perform an initial check to set the correct state when this object is enabled.
        RefreshVisibility();
    }

    /// <summary>
    /// Checks the current authentication state and updates the GameObject's visibility accordingly.
    /// </summary>
    public void RefreshVisibility()
    {
        // It's good practice to check if the instance exists, especially during scene transitions.
        if (JwtManager.Instance == null)
        {
            // If the JwtManager isn't ready, default to a safe state (usually inactive).
            if (gameObject.activeSelf)
            {
                gameObject.SetActive(false);
            }
            return;
        }

        bool isLoggedIn = JwtManager.Instance.IsTokenValid();

        // Determine if this object should be active based on its configuration.
        bool shouldBeActive = (isLoggedIn == showWhenLoggedIn);

        // Only call SetActive if the state needs to change, to avoid unnecessary work.
        if (gameObject.activeSelf != shouldBeActive)
        {
            gameObject.SetActive(shouldBeActive);
        }
    }
}
