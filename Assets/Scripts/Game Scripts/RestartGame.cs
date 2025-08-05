using UnityEngine;
using UnityEngine.SceneManagement;

public class RestartGame : MonoBehaviour
{
    /// <summary>
    /// Restarts the current active scene.
    /// This method is intended to be called from a UI Button's OnClick event.
    /// </summary>
    public void RestartCurrentScene()
    {
        Debug.Log("Restarting the current scene...");
        // Resume time in case it was paused.
        Time.timeScale = 1f;

        // Reload the current scene.
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }
}
