using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(CircleCollider2D))]
public class Projectile : MonoBehaviour
{
    [Header("Config")]
    public float speed = 8f;
    public int damage = 10;
    public float lifetime = 3f;
    public bool destroyOnHit = true;

    [Tooltip("Only apply damage to objects tagged 'Mob' (or their parents)")]
    public bool requireMobTag = true;

    [Header("Runtime")]
    public Vector2 direction = Vector2.right;
    public Transform owner;

    private Rigidbody2D _rb;
    private CircleCollider2D _col;
    private bool _stopped;

    private void Awake()
    {
        _rb = GetComponent<Rigidbody2D>();
        _col = GetComponent<CircleCollider2D>();
        _rb.gravityScale = 0f;
        _rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
        _rb.interpolation = RigidbodyInterpolation2D.Interpolate;
        _col.isTrigger = true;
    }

    private void OnEnable()
    {
        // Start life timer
        if (lifetime > 0f)
        {
            Invoke(nameof(Expire), lifetime);
        }
        GameOverController.OnCountdownFinished += StopMovement;
    }

    public void Launch()
    {
        if (_stopped)
        {
            if (_rb != null)
                _rb.linearVelocity = Vector2.zero;
            return;
        }
        if (direction.sqrMagnitude < 0.0001f)
            return;
        _rb.linearVelocity = direction.normalized * speed;
    }

    public void SetOwner(Transform ownerTransform)
    {
        owner = ownerTransform;
        // Ignore collision with owner's colliders
        if (owner == null)
            return;
        var ownerColliders = owner.GetComponentsInChildren<Collider2D>();
        foreach (var oc in ownerColliders)
        {
            Physics2D.IgnoreCollision(_col, oc, true);
        }
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (other == null)
            return;
        if (owner != null && (other.transform == owner || other.transform.IsChildOf(owner)))
            return; // ignore owner

        if (requireMobTag)
        {
            var t = other.transform;
            bool isMob = t.CompareTag("Mob") || (t.parent != null && t.parent.CompareTag("Mob"));
            if (!isMob)
            {
                if (destroyOnHit)
                    Expire();
                return;
            }
        }

        var dmg = other.GetComponentInParent<IDamageable>();
        if (dmg != null && dmg.IsAlive)
        {
            Vector2 hitPoint = other.ClosestPoint(transform.position);
            Vector2 hitNormal = (Vector2)(other.transform.position - transform.position).normalized;
            dmg.TakeDamage(damage, hitPoint, hitNormal);
            if (destroyOnHit)
                Expire();
            return;
        }

        // Optionally expire on any hit
        if (destroyOnHit)
        {
            Expire();
        }
    }

    private void Expire()
    {
        CancelInvoke();
        Destroy(gameObject);
    }

    private void OnDisable()
    {
        GameOverController.OnCountdownFinished -= StopMovement;
    }

    private void StopMovement()
    {
        _stopped = true;
        if (_rb != null)
            _rb.linearVelocity = Vector2.zero;
    }
}
