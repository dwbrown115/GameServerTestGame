using UnityEngine;

public class Collectible : MonoBehaviour
{
    // A static event to notify other parts of the game that the score has changed.
    // This is more efficient than checking the score every frame in the UI.
    public static event System.Action<int> OnScoreChanged;

    private const string PLAYER_TAG = "Player";

    public static void InvokeOnScoreChanged(int newScore)
    {
        OnScoreChanged?.Invoke(newScore);
        // Bridge to central aggregator so listeners not referencing this class still get updates.
        ScoreEvents.RaiseScoreChanged(newScore);
    }

    public void Initialize(string uniqueId, Vector3 position)
    {
        gameObject.name = uniqueId; // Set the name here
        // Ensure tag is set so managers can find collectibles without hard type coupling.
        try
        {
            gameObject.tag = "Collectible";
        }
        catch
        {
            // Tag may not exist; ignore.
        }

        if (!IsNumberValid.isValidNumber(uniqueId))
        {
            Debug.LogWarning(
                $"Collectible: Object name '{uniqueId}' is not a valid number. Destroying object."
            );
            Destroy(gameObject);
            return;
        }
        ValidatedObjectsManager.AddActiveObject(uniqueId, position);
        // Enable the component if it was disabled by default
        this.enabled = true;
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (GameStateManager.IsGameOver)
            return;

        // Check if the object that entered the trigger is the player.
        if (other.CompareTag(PLAYER_TAG))
        {
            string currentObjectId = gameObject.name;

            // --- Anti-cheat: Check for duplicates in scene at runtime ---
            Collectible[] allCollectibles = FindObjectsByType<Collectible>(
                FindObjectsSortMode.None
            );
            int count = 0;
            foreach (Collectible c in allCollectibles)
            {
                if (c.gameObject.name == currentObjectId)
                {
                    count++;
                }
            }

            if (count > 1)
            {
                Debug.LogWarning(
                    $"Collectible: Duplicate object '{currentObjectId}' detected on trigger. Destroying this instance."
                );
                Destroy(gameObject);
                return; // Stop processing this collectible
            }

            // --- Anti-cheat: Check if already claimed ---
            if (ValidatedObjectsManager.IsObjectClaimed(currentObjectId))
            {
                Debug.LogWarning(
                    $"Collectible: Object '{currentObjectId}' has already been claimed. Destroying this instance."
                );
                Destroy(gameObject);
                return; // Stop processing this collectible
            }

            ValidatedObjectsManager.DestroyObject(gameObject.name);
            if (GameMode.Offline)
            {
                // In offline mode, immediately increment local score and notify listeners
                int currentScore = PlayerPrefs.GetInt("PlayerScore", 0);
                int newScore = currentScore + 1;
                PlayerPrefs.SetInt("PlayerScore", newScore);
                PlayerPrefs.Save();
                InvokeOnScoreChanged(newScore);
            }
            Destroy(gameObject);
        }
    }
}
