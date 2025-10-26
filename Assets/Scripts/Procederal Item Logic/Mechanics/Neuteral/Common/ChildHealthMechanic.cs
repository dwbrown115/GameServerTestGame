using System;
using UnityEngine;
using UnityEngine.Events;

namespace Mechanics.Neuteral
{
    /// <summary>
    /// Provides a lightweight health pool for procedurally spawned child payloads.
    /// Implements <see cref="IDamageable"/> so other gameplay systems can damage the payload.
    /// When health reaches zero, optional actions (messages, UnityEvents, destruction) are triggered.
    /// </summary>
    [DisallowMultipleComponent]
    public class ChildHealthMechanic : MonoBehaviour, IMechanic, IDamageable
    {
        [Header("Health")]
        [Min(1)]
        public int maxHealth = 5;

        [Tooltip(
            "If true, health resets to maxHealth every time the mechanic is enabled/initialized."
        )]
        public bool resetHealthOnEnable = true;

        [SerializeField]
        [Tooltip("Current hit points remaining.")]
        private int currentHealth = 5;

        [Header("Depletion Behaviour")]
        [Tooltip("Destroy the payload GameObject when health depletes.")]
        public bool destroyOnDepleted = true;

        [Tooltip(
            "Deactivate the payload GameObject when health depletes (before optional destroy)."
        )]
        public bool disableGameObjectOnDepleted = false;

        [Tooltip("Disable this mechanic component when health depletes.")]
        public bool disableMechanicOnDepleted = true;

        [Tooltip("Disable attached Collider2D components when health depletes.")]
        public bool disableCollidersOnDepleted = true;

        [Header("Messaging / Events")]
        [Tooltip(
            "Optional Unity SendMessage name invoked when health depletes. Payload receives a HealthDepletedMessage struct."
        )]
        public string sendMessageOnDepleted = null;

        [Tooltip("Send the depletion message to the mechanic payload GameObject.")]
        public bool sendMessageToPayload = true;

        [Tooltip("Send the depletion message to the owning transform if available.")]
        public bool sendMessageToOwner = true;

        [Tooltip("Invoked after depletion handling (messages/destruction).")]
        public UnityEvent onDepleted = new UnityEvent();

        [Header("Debug")]
        public bool debugLogs = false;

        private MechanicContext _context;
        private bool _depleted;

        public bool IsAlive => !_depleted && currentHealth > 0;
        public int CurrentHealth => currentHealth;

        public void Initialize(MechanicContext ctx)
        {
            _context = ctx;
            maxHealth = Mathf.Max(1, maxHealth);

            if (resetHealthOnEnable || currentHealth <= 0)
                SetCurrentHealth(maxHealth);
            else
                SetCurrentHealth(currentHealth, clampToMax: true);

            if (debugLogs)
            {
                Debug.Log(
                    $"[ChildHealthMechanic] Initialized health={currentHealth}/{maxHealth}",
                    this
                );
            }
        }

        private void OnEnable()
        {
            if (resetHealthOnEnable)
                SetCurrentHealth(maxHealth);
        }

        public void Tick(float dt) { }

        public void TakeDamage(int amount, Vector2 hitPoint, Vector2 hitNormal)
        {
            if (amount <= 0)
                return;
            if (_depleted)
                return;

            maxHealth = Mathf.Max(1, maxHealth);
            int newHealth = Mathf.Clamp(currentHealth - Mathf.Abs(amount), 0, maxHealth);
            if (debugLogs)
            {
                Debug.Log(
                    $"[ChildHealthMechanic] TakeDamage amount={amount} => {newHealth}/{maxHealth}",
                    this
                );
            }

            SetCurrentHealth(newHealth, clampToMax: true, suppressLog: true);

            if (currentHealth <= 0)
                HandleDepleted(hitPoint, hitNormal);
        }

        bool IDamageable.IsAlive => IsAlive;

        /// <summary>
        /// Heals the child by the specified amount (clamped to maxHealth).
        /// </summary>
        public void Heal(int amount)
        {
            if (amount <= 0)
                return;
            if (_depleted && (destroyOnDepleted || disableGameObjectOnDepleted))
                return;

            maxHealth = Mathf.Max(1, maxHealth);
            int newHealth = Mathf.Clamp(currentHealth + amount, 0, maxHealth);
            SetCurrentHealth(newHealth, clampToMax: true);
        }

        /// <summary>
        /// Sets current health explicitly. Optionally clamps to maxHealth.
        /// </summary>
        public void SetCurrentHealth(int value, bool clampToMax = true, bool suppressLog = false)
        {
            maxHealth = Mathf.Max(1, maxHealth);
            int clamped = clampToMax ? Mathf.Clamp(value, 0, maxHealth) : Mathf.Max(0, value);
            currentHealth = clamped;
            _depleted = currentHealth <= 0;

            if (debugLogs && !suppressLog)
            {
                Debug.Log(
                    $"[ChildHealthMechanic] SetCurrentHealth => {currentHealth}/{maxHealth}",
                    this
                );
            }
        }

        /// <summary>
        /// Resets health back to maxHealth.
        /// </summary>
        public void ResetHealth()
        {
            SetCurrentHealth(maxHealth);
        }

        private void HandleDepleted(Vector2 hitPoint, Vector2 hitNormal)
        {
            if (_depleted)
                return;

            _depleted = true;

            if (debugLogs)
                Debug.Log("[ChildHealthMechanic] Health depleted.", this);

            DispatchDepletedMessage(hitPoint, hitNormal);

            if (onDepleted != null)
                onDepleted.Invoke();

            if (disableCollidersOnDepleted)
            {
                var colliders = GetComponents<Collider2D>();
                for (int i = 0; i < colliders.Length; i++)
                    colliders[i].enabled = false;
            }

            if (disableMechanicOnDepleted)
                enabled = false;

            if (disableGameObjectOnDepleted && gameObject.activeSelf)
                gameObject.SetActive(false);

            if (destroyOnDepleted && gameObject != null)
                Destroy(gameObject);
        }

        private void DispatchDepletedMessage(Vector2 hitPoint, Vector2 hitNormal)
        {
            if (string.IsNullOrWhiteSpace(sendMessageOnDepleted))
                return;

            var payload = new HealthDepletedMessage
            {
                source = this,
                payload = transform,
                owner = _context != null ? _context.Owner : null,
                hitPoint = hitPoint,
                hitNormal = hitNormal,
            };

            if (sendMessageToPayload)
                SendMessageSafely(gameObject, payload);

            if (sendMessageToOwner && _context?.Owner != null)
                SendMessageSafely(_context.Owner.gameObject, payload);

            if (!sendMessageToPayload && !sendMessageToOwner)
                SendMessageSafely(gameObject, payload);
        }

        private void SendMessageSafely(GameObject target, HealthDepletedMessage payload)
        {
            if (target == null)
                return;
            target.SendMessage(
                sendMessageOnDepleted,
                payload,
                SendMessageOptions.DontRequireReceiver
            );
        }

        private void OnValidate()
        {
            maxHealth = Mathf.Max(1, maxHealth);
            currentHealth = Mathf.Clamp(currentHealth, 0, maxHealth);
        }

        [Serializable]
        public struct HealthDepletedMessage
        {
            public ChildHealthMechanic source;
            public Transform payload;
            public Transform owner;
            public Vector2 hitPoint;
            public Vector2 hitNormal;
        }
    }
}
