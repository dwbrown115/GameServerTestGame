using UnityEngine;

public class GameSceneManager : MonoBehaviour
{
    void Start()
    {
        GameStateManager.IsGameOver = false; // Reset on start
        GameOverController.OnCountdownFinished += OnGameOver;

        Debug.Log("GameSceneManager Start");
        if (WebSocketManager.Instance != null)
        {
            string sessionId = WebSocketManager.Instance.GetSessionId();
            Debug.Log(
                $"GameSceneManager: Session ID from WebSocketManager.Instance.GetSessionId() is '{sessionId}'"
            );
            if (!string.IsNullOrEmpty(sessionId))
            {
                ValidatedObjectsManager.CreateOrResetFile(sessionId);
            }
            else
            {
                Debug.Log(
                    "GameSceneManager: Session ID is not ready, subscribing to OnSessionIdReady event."
                );
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
        GameOverController.OnCountdownFinished -= OnGameOver;
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
        // Avoid hard dependency on Collectible type; use tag-based search
        var candidates = GameObject.FindGameObjectsWithTag("Collectible");
        foreach (var go in candidates)
        {
            if (!ValidatedObjectsManager.IsObjectClaimed(go.name))
            {
                Destroy(go);
            }
        }
    }

    void CreateValidatedObjectsFile(string sessionId)
    {
        Debug.Log(
            $"GameSceneManager: CreateValidatedObjectsFile called with session ID: {sessionId}"
        );
        ValidatedObjectsManager.CreateOrResetFile(sessionId);
        // Unsubscribe after receiving the event to avoid multiple calls
        Debug.Log(
            "GameSceneManager: Unsubscribing from OnSessionIdReady event after receiving it."
        );
        WebSocketManager.OnSessionIdReady -= CreateValidatedObjectsFile;
    }
}
