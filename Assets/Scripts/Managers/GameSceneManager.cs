using UnityEngine;

public class GameSceneManager : MonoBehaviour
{
    void Start()
    {
        GameStateManager.IsGameOver = false; // Reset on start
        CountdownTimer.OnCountdownFinished += OnGameOver;

        Debug.Log("GameSceneManager Start");
        if (WebSocketManager.Instance != null)
        {
            string sessionId = WebSocketManager.Instance.GetSessionId();
            Debug.Log($"GameSceneManager: Session ID from WebSocketManager.Instance.GetSessionId() is '{sessionId}'");
            if (!string.IsNullOrEmpty(sessionId))
            {
                ValidatedObjectsManager.CreateOrResetFile(sessionId);
            }
            else
            {
                Debug.Log("GameSceneManager: Session ID is not ready, subscribing to OnSessionIdReady event.");
                WebSocketManager.OnSessionIdReady += CreateValidatedObjectsFile;
            }
        }
        else
        {
            Debug.LogError("WebSocketManager instance not found.");
        }
    }

    void OnDestroy()
    {
        // It's good practice to unsubscribe from static events.
        CountdownTimer.OnCountdownFinished -= OnGameOver;
        Debug.Log("GameSceneManager OnDestroy: Unsubscribing from OnSessionIdReady event.");
        WebSocketManager.OnSessionIdReady -= CreateValidatedObjectsFile;
    }

    void OnGameOver()
    {
        GameStateManager.IsGameOver = true;
        DestroyUnclaimedCollectibles();
    }

    void DestroyUnclaimedCollectibles()
    {
        Collectible[] collectibles = FindObjectsByType<Collectible>(FindObjectsSortMode.None);
        foreach (Collectible collectible in collectibles)
        {
            if (!ValidatedObjectsManager.IsObjectClaimed(collectible.gameObject.name))
            {
                Destroy(collectible.gameObject);
            }
        }
    }

    void CreateValidatedObjectsFile(string sessionId)
    {
        Debug.Log($"GameSceneManager: CreateValidatedObjectsFile called with session ID: {sessionId}");
        ValidatedObjectsManager.CreateOrResetFile(sessionId);
        // Unsubscribe after receiving the event to avoid multiple calls
        Debug.Log("GameSceneManager: Unsubscribing from OnSessionIdReady event after receiving it.");
        WebSocketManager.OnSessionIdReady -= CreateValidatedObjectsFile;
    }
}
