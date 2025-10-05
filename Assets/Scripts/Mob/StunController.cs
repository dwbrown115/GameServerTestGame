using UnityEngine;

public interface IStunnable
{
    void ApplyStun(float seconds);
    bool IsStunned { get; }
}

[DisallowMultipleComponent]
public class StunController : MonoBehaviour, IStunnable
{
    private float _stunUntil;

    [Tooltip(
        "When true, keep this object's Rigidbody2D velocity at zero while stunned (localized freeze)."
    )]
    public bool hardFreeze = true;

    public bool IsStunned => Time.time < _stunUntil;
    private Rigidbody2D _rb;
    private Vector3 _frozenPosition;
    private bool _posLocked;

    private void Awake()
    {
        _rb = GetComponent<Rigidbody2D>();
    }

    public void ApplyStun(float seconds)
    {
        float dur = Mathf.Max(0f, seconds);
        _stunUntil = Mathf.Max(_stunUntil, Time.time + dur);
        if (Application.isPlaying && dur > 0f)
            Debug.Log(
                $"[StunController] ApplyStun dur={dur:0.###} newUntil={_stunUntil:0.###} on {name}",
                this
            );
        // Try to stop immediate movement if a Rigidbody2D exists
        if (_rb == null)
            _rb = GetComponent<Rigidbody2D>();
        if (_rb != null)
        {
            _rb.linearVelocity = Vector2.zero;
        }
        if (hardFreeze)
        {
            _frozenPosition = transform.position;
            _posLocked = true;
        }
    }

    private void Update()
    {
        if (!hardFreeze || !IsStunned)
            return;
        if (_rb == null)
            _rb = GetComponent<Rigidbody2D>();
        if (_rb != null && _rb.linearVelocity.sqrMagnitude > 0f)
        {
            _rb.linearVelocity = Vector2.zero; // enforce freeze only on this object
            Debug.Log($"[StunController] Enforcing freeze (Update) vel->0 {name}", this);
        }
        else if (_rb == null && _posLocked)
        {
            // No rigidbody path: revert any transform drift
            if ((transform.position - _frozenPosition).sqrMagnitude > 0.0001f)
            {
                Debug.Log($"[StunController] Correcting position drift (Update) {name}", this);
                transform.position = _frozenPosition;
            }
        }
    }

    private void FixedUpdate()
    {
        if (!hardFreeze || !IsStunned)
            return;
        if (_rb == null)
            _rb = GetComponent<Rigidbody2D>();
        if (_rb != null)
        {
            if (_rb.linearVelocity.sqrMagnitude > 0f)
            {
                Debug.Log($"[StunController] Enforcing freeze (FixedUpdate) vel->0 {name}", this);
            }
            _rb.linearVelocity = Vector2.zero;
            if (_posLocked)
            {
                // For kinematic bodies, ensure transform hasn't drifted
                if ((transform.position - _frozenPosition).sqrMagnitude > 0.0001f)
                {
                    transform.position = _frozenPosition;
                }
            }
        }
        else if (_posLocked)
        {
            if ((transform.position - _frozenPosition).sqrMagnitude > 0.0001f)
            {
                Debug.Log($"[StunController] Correcting position drift (FixedUpdate) {name}", this);
                transform.position = _frozenPosition;
            }
        }
    }

    private void LateUpdate()
    {
        if (!hardFreeze || !IsStunned)
            return;
        if (_posLocked)
        {
            if ((transform.position - _frozenPosition).sqrMagnitude > 0.0001f)
            {
                Debug.Log($"[StunController] Correcting position drift (LateUpdate) {name}", this);
                transform.position = _frozenPosition;
            }
        }
    }
}
