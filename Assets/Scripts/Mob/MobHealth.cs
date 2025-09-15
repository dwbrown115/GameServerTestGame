using System;
using UnityEngine;

[DisallowMultipleComponent]
public class MobHealth : MonoBehaviour, IDamageable
{
    [Header("Health")]
    [SerializeField]
    private int maxHealth = 50;

    [SerializeField]
    private int currentHealth;

    public event Action<int, int> OnHealthChanged;
    public event Action OnDied;

    public bool IsAlive => currentHealth > 0;

    [Header("Debug")]
    public bool debugLogs = false;

    private void Awake()
    {
        currentHealth = Mathf.Clamp(currentHealth <= 0 ? maxHealth : currentHealth, 0, maxHealth);
    }

    public void Heal(int amount)
    {
        if (!IsAlive)
            return;
        int before = currentHealth;
        currentHealth = Mathf.Clamp(currentHealth + Mathf.Abs(amount), 0, maxHealth);
        if (currentHealth != before)
        {
            if (debugLogs)
                Debug.Log(
                    $"[MobHealth] {name}: Heal {before} -> {currentHealth}/{maxHealth}",
                    this
                );
            OnHealthChanged?.Invoke(currentHealth, maxHealth);
        }
    }

    // Expose current/max for UI and helpers
    public void GetHealth(out int current, out int max)
    {
        current = currentHealth;
        max = maxHealth;
    }

    public void TakeDamage(int amount, Vector2 hitPoint, Vector2 hitNormal)
    {
        if (!IsAlive)
            return;
        int before = currentHealth;
        currentHealth = Mathf.Clamp(currentHealth - Mathf.Abs(amount), 0, maxHealth);
        if (currentHealth != before)
        {
            if (debugLogs)
                Debug.Log(
                    $"[MobHealth] {name}: Damage {before} -> {currentHealth}/{maxHealth} at {hitPoint}",
                    this
                );
            OnHealthChanged?.Invoke(currentHealth, maxHealth);
        }
        if (currentHealth == 0)
        {
            OnDied?.Invoke();
            if (debugLogs)
                Debug.Log($"[MobHealth] {name}: Died", this);
            HandleDeath();
        }
    }

    private void HandleDeath()
    {
        // Simple default: disable AI and destroy.
        var ai = GetComponent<SimpleMobAI>();
        if (ai)
            ai.enabled = false;

        // Increment score by 1 on kill
        int newScore;
        if (GameMode.Offline)
        {
            int current = PlayerPrefs.GetInt("PlayerScore", 0);
            newScore = current + 1;
            PlayerPrefs.SetInt("PlayerScore", newScore);
            PlayerPrefs.Save();
            ScoreEvents.RaiseScoreChanged(newScore);
        }
        else
        {
            // Future: send to server or central manager; for now, still raise local event
            int current = PlayerPrefs.GetInt("PlayerScore", 0);
            newScore = current + 1;
            PlayerPrefs.SetInt("PlayerScore", newScore);
            PlayerPrefs.Save();
            ScoreEvents.RaiseScoreChanged(newScore);
        }
        Destroy(gameObject);
    }
}
