using System.Collections;
using UnityEngine;

[DisallowMultipleComponent]
public class PlayerProjectileShooter : MonoBehaviour
{
    [Header("References")]
    public PlayerController2D playerController;

    [Header("Projectile Prefab")]
    [Tooltip(
        "Prefab with Projectile + CircleCollider2D + Rigidbody2D. If empty, one will be created programmatically."
    )]
    public GameObject projectilePrefab;

    [Header("Config")]
    public float projectileRadius = 0.15f;
    public float projectileSpeed = 8f;
    public int projectileDamage = 10;
    public float projectileLifetime = 3f;
    public float spawnInterval = 0.5f;

    private Rigidbody2D _rb;
    private Coroutine _loop;
    private bool _stopped;

    private void Awake()
    {
        _rb = GetComponent<Rigidbody2D>();
        if (playerController == null)
            playerController = GetComponent<PlayerController2D>();
    }

    private void OnEnable()
    {
        GameOverController.OnCountdownFinished += StopFiring;
        _loop = StartCoroutine(FireLoop());
    }

    private void OnDisable()
    {
        GameOverController.OnCountdownFinished -= StopFiring;
        if (_loop != null)
            StopCoroutine(_loop);
    }

    private IEnumerator FireLoop()
    {
        var wait = new WaitForSeconds(Mathf.Max(0.01f, spawnInterval));
        while (true)
        {
            if (_stopped)
                yield break;
            FireOnce();
            yield return wait;
        }
    }

    private void FireOnce()
    {
        Vector2 dir = GetMoveDirection();
        if (dir.sqrMagnitude < 0.0001f)
            return; // don't fire if not moving

        var projGO =
            projectilePrefab != null ? Instantiate(projectilePrefab) : CreateDefaultProjectile();
        projGO.transform.position =
            transform.position + (Vector3)(dir.normalized * (projectileRadius + 0.2f));
        projGO.transform.rotation = Quaternion.identity;

        var proj = projGO.GetComponent<Projectile>();
        if (proj == null)
            proj = projGO.AddComponent<Projectile>();
        var cc = projGO.GetComponent<CircleCollider2D>();
        if (cc == null)
            cc = projGO.AddComponent<CircleCollider2D>();
        cc.isTrigger = true;
        cc.radius = projectileRadius;

        var rb = projGO.GetComponent<Rigidbody2D>();
        if (rb == null)
            rb = projGO.AddComponent<Rigidbody2D>();
        rb.gravityScale = 0f;
        rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;

        proj.speed = projectileSpeed;
        proj.damage = projectileDamage;
        proj.lifetime = projectileLifetime;
        proj.direction = dir.normalized;
        proj.SetOwner(transform);
        proj.Launch();
    }

    private GameObject CreateDefaultProjectile()
    {
        var go = new GameObject("Projectile");
        var sr = go.AddComponent<SpriteRenderer>();
        // A built-in circle sprite might not exist; leave blank or assign in prefab in Editor
        sr.color = Color.white;
        return go;
    }

    private Vector2 GetMoveDirection()
    {
        // Prefer rigidbody velocity as truth; fallback to last input if needed
        if (_rb != null && _rb.linearVelocity.sqrMagnitude > 0.0001f)
            return _rb.linearVelocity.normalized;
        // If PlayerController2D exposes input, we could read it; for now zero
        return Vector2.zero;
    }

    private void StopFiring()
    {
        _stopped = true;
        if (_loop != null)
        {
            StopCoroutine(_loop);
            _loop = null;
        }
    }
}
