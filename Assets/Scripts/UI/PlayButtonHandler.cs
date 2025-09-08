using UnityEngine;

public class PlayButtonHandler : MonoBehaviour
{
    public void OnPlayPressed()
    {
        // Prefer the configured singleton
        var mgr = WebSocketManager.Instance;
        if (mgr == null)
        {
            // Try to find an existing configured instance in the scene(s)
#if UNITY_2022_2_OR_NEWER
            mgr = Object.FindFirstObjectByType<WebSocketManager>();
#else
            mgr = FindObjectOfType<WebSocketManager>();
#endif
            if (mgr != null)
            {
                Debug.Log("PlayButtonHandler: Resolved WebSocketManager via scene lookup.");
            }
        }
        if (mgr == null)
        {
            Debug.LogError(
                "PlayButtonHandler: No WebSocketManager found. Ensure it exists in a boot scene and is DontDestroyOnLoad."
            );
            return;
        }
        mgr.AuthenticateWebSocket();
        Debug.Log("PlayButtonHandler: Play pressed -> AuthenticateWebSocket()");
    }
}
