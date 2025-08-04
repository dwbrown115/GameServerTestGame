using TMPro; // Make sure to import the TextMeshPro namespace
using UnityEngine;

public class ScoreUI : MonoBehaviour
{
    private TMP_Text scoreText;
    private const string SCORE_PREFS_KEY = "PlayerScore";

    private void Awake()
    {
        scoreText = GetComponent<TMP_Text>();
        // Clear the score from previous sessions when the game starts.
        PlayerPrefs.DeleteKey(SCORE_PREFS_KEY);
        PlayerPrefs.Save();
    }

    private void OnEnable()
    {
        // Subscribe to the OnScoreChanged event from the Collectible class.
        Collectible.OnScoreChanged += UpdateScoreText;
    }

    private void OnDisable()
    {
        // Unsubscribe from the event when the object is disabled to prevent memory leaks.
        Collectible.OnScoreChanged -= UpdateScoreText;
    }

    private void Start()
    {
        int initialScore = PlayerPrefs.GetInt(SCORE_PREFS_KEY, 0);
        UpdateScoreText(initialScore);
    }

    private void UpdateScoreText(int newScore)
    {
        scoreText.text = $"Score: {newScore}";
    }
}
