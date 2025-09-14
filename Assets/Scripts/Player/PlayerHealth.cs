using System;
using UnityEngine;

[DisallowMultipleComponent]
public class PlayerHealth : MonoBehaviour, IDamageable
{
    [Header("Health")]
    [SerializeField]
    private int maxHealth = 100;

    [SerializeField]
    private int currentHealth;

    public event Action<int, int> OnHealthChanged; // current, max
    public event Action OnDied;

    public bool IsAlive => currentHealth > 0;
    public int MaxHealth => maxHealth;
    public int CurrentHealth => currentHealth;

    public void GetHealth(out int current, out int max)
    {
        current = currentHealth;
        max = maxHealth;
    }

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
            OnHealthChanged?.Invoke(currentHealth, maxHealth);
    }

    public void TakeDamage(int amount, Vector2 hitPoint, Vector2 hitNormal)
    {
        if (!IsAlive)
            return;
        int before = currentHealth;
        currentHealth = Mathf.Clamp(currentHealth - Mathf.Abs(amount), 0, maxHealth);
        if (currentHealth != before)
            OnHealthChanged?.Invoke(currentHealth, maxHealth);
        if (currentHealth == 0)
        {
            OnDied?.Invoke();
            HandleDeath();
        }
    }

    private void HandleDeath()
    {
        // Mark game over and disable player control/collider
        GameStateManager.IsGameOver = true;
        // Simple default: disable movement and collider, can extend later
        var rb2d = GetComponent<Rigidbody2D>();
        if (rb2d)
            rb2d.linearVelocity = Vector2.zero;
        var pc = GetComponent<PlayerController2D>();
        if (pc)
        {
            // Reuse existing game-over flow
            var method = typeof(PlayerController2D).GetMethod("DisconnectWebSocket");
            try
            {
                pc.SendMessage("DisableMovement", SendMessageOptions.DontRequireReceiver);
            }
            catch { }
            try
            {
                pc.DisconnectWebSocket();
            }
            catch { }
        }
        var col = GetComponent<Collider2D>();
        if (col)
            col.enabled = false;
        // Optionally: play animation, respawn logic, notify managers
    }
}
