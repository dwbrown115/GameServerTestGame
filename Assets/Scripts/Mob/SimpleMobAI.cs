using System.Collections;
using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
[DisallowMultipleComponent]
public class SimpleMobAI : MonoBehaviour
{
    [Header("Targeting")]
    public Transform target; // assign player at runtime if null
    public float detectionRadius = 10f;
    public LayerMask targetMask;

    [Header("Movement")]
    public float moveSpeed = 3f;
    public float stoppingDistance = 1.25f;

    [Header("Attack")]
    public float attackInterval = 1.0f;
    public int attackDamage = 10;
    public float attackRange = 1.5f;

    private Rigidbody2D _rb;
    private float _lastAttackTime;
    private bool _stopped;
    private StunController _stun;

    private void Awake()
    {
        _rb = GetComponent<Rigidbody2D>();
    }

    private void OnEnable()
    {
        GameOverController.OnCountdownFinished += StopAllActions;
    }

    private void OnDisable()
    {
        GameOverController.OnCountdownFinished -= StopAllActions;
    }

    private void Start()
    {
        if (target == null)
        {
            var player = GameObject.FindGameObjectWithTag("Player");
            if (player != null)
                target = player.transform;
        }
    }

    private void FixedUpdate()
    {
        if (_stopped)
        {
            if (_rb != null)
                _rb.linearVelocity = Vector2.zero;
            return;
        }
        if (_stun == null)
            _stun = GetComponent<StunController>();
        if (_stun != null && _stun.IsStunned)
        {
            if (_rb != null)
            {
                if (_rb.linearVelocity.sqrMagnitude > 0f)
                {
                    Debug.Log(
                        $"[SimpleMobAI] Stunned; clearing velocity from {_rb.linearVelocity} on {name}",
                        this
                    );
                }
                _rb.linearVelocity = Vector2.zero;
            }
            return;
        }
        if (target == null)
        {
            _rb.linearVelocity = Vector2.zero;
            return;
        }

        var toTarget = (target.position - transform.position);
        float dist = toTarget.magnitude;

        // Detection gate
        if (dist > detectionRadius)
        {
            _rb.linearVelocity = Vector2.zero;
            return;
        }

        // Move towards until within stopping distance
        if (dist > Mathf.Max(stoppingDistance, 0.01f))
        {
            Vector2 dir = toTarget.normalized;
            _rb.linearVelocity = dir * moveSpeed;
        }
        else
        {
            _rb.linearVelocity = Vector2.zero;
            TryAttack();
        }
    }

    private void StopAllActions()
    {
        _stopped = true;
        if (_rb != null)
            _rb.linearVelocity = Vector2.zero;
    }

    private void TryAttack()
    {
        if (Time.time - _lastAttackTime < attackInterval)
            return;
        if (target == null)
            return;
        var toTarget = (target.position - transform.position);
        if (toTarget.magnitude > attackRange)
            return;

        var damageable = target.GetComponent<IDamageable>();
        if (damageable == null)
        {
            // try to find on parent
            damageable = target.GetComponentInParent<IDamageable>();
        }

        if (damageable != null && damageable.IsAlive)
        {
            Vector2 hitPoint = target.position;
            Vector2 hitNormal = (-toTarget).normalized;
            damageable.TakeDamage(attackDamage, hitPoint, hitNormal);
            _lastAttackTime = Time.time;
        }
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(1, 0, 0, 0.25f);
        Gizmos.DrawWireSphere(transform.position, detectionRadius);
        Gizmos.color = new Color(1, 0.5f, 0, 0.25f);
        Gizmos.DrawWireSphere(transform.position, attackRange);
        Gizmos.color = new Color(0, 1, 0, 0.25f);
        Gizmos.DrawWireSphere(transform.position, stoppingDistance);
    }
}
