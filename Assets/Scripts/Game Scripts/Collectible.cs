using UnityEngine;

public class Collectible : MonoBehaviour
{
    [Tooltip("The amount of score this collectible adds.")]
    [SerializeField]
    private int scoreValue = 10;

    // A static event to notify other parts of the game that the score has changed.
    // This is more efficient than checking the score every frame in the UI.
    public static event System.Action<int> OnScoreChanged;

    private const string PLAYER_TAG = "Player";
    private const string SCORE_PREFS_KEY = "PlayerScore";

    private void OnTriggerEnter2D(Collider2D other)
    {
        // Check if the object that entered the trigger is the player.
        if (other.CompareTag(PLAYER_TAG))
        {
            AddScore();
            Destroy(gameObject);
        }
    }

    private void AddScore()
    {
        int currentScore = PlayerPrefs.GetInt(SCORE_PREFS_KEY, 0);
        int newScore = currentScore + scoreValue;

        PlayerPrefs.SetInt(SCORE_PREFS_KEY, newScore);
        PlayerPrefs.Save(); // It's good practice to save immediately.

        // Invoke the event to notify listeners (like the UI) that the score has updated.
        OnScoreChanged?.Invoke(newScore);
    }
}
