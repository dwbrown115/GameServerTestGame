using System;
using UnityEngine;

[DisallowMultipleComponent]
public class MobHealth : MonoBehaviour, IDamageable
{
    [Header("Health")] [SerializeField] private int maxHealth = 50;
    [SerializeField] private int currentHealth;

    public event Action<int, int> OnHealthChanged;
    public event Action OnDied;

    public bool IsAlive => currentHealth > 0;

    private void Awake()
    {
        currentHealth = Mathf.Clamp(currentHealth <= 0 ? maxHealth : currentHealth, 0, maxHealth);
    }

    public void Heal(int amount)
    {
        if (!IsAlive) return;
        int before = currentHealth;
        currentHealth = Mathf.Clamp(currentHealth + Mathf.Abs(amount), 0, maxHealth);
        if (currentHealth != before) OnHealthChanged?.Invoke(currentHealth, maxHealth);
    }

    public void TakeDamage(int amount, Vector2 hitPoint, Vector2 hitNormal)
    {
        if (!IsAlive) return;
        int before = currentHealth;
        currentHealth = Mathf.Clamp(currentHealth - Mathf.Abs(amount), 0, maxHealth);
        if (currentHealth != before) OnHealthChanged?.Invoke(currentHealth, maxHealth);
        if (currentHealth == 0)
        {
            OnDied?.Invoke();
            HandleDeath();
        }
    }

    private void HandleDeath()
    {
        // Simple default: disable AI and destroy.
        var ai = GetComponent<SimpleMobAI>();
        if (ai) ai.enabled = false;
        Destroy(gameObject);
    }
}
