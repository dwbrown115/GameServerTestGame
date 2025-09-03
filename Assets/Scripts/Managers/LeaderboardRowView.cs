using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Simple component holding references to a single leaderboard row's UI elements.
/// </summary>
public class LeaderboardRowView : MonoBehaviour
{
    public TMP_Text rankText;
    public TMP_Text userText;
    public TMP_Text scoreText;
    public Image background;

    public void Set(int rank, string username, int score)
    {
        if (rankText)
            rankText.text = rank.ToString();
        if (userText)
            userText.text = string.IsNullOrEmpty(username) ? "<no name>" : username;
        if (scoreText)
            scoreText.text = score.ToString();
    }
}
