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
    }

    public void Initialize(string uniqueId, Vector3 position)
    {
        gameObject.name = uniqueId; // Set the name here

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
        if (GameStateManager.IsGameOver) return;

        // Check if the object that entered the trigger is the player.
        if (other.CompareTag(PLAYER_TAG))
        {
            string currentObjectId = gameObject.name;

            // --- Anti-cheat: Check for duplicates in scene at runtime ---
            Collectible[] allCollectibles = FindObjectsByType<Collectible>(FindObjectsSortMode.None);
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
            Destroy(gameObject);
        }
    }
}
