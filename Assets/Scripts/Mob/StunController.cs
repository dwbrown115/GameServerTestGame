using UnityEngine;

[DisallowMultipleComponent]
public class StunController : MonoBehaviour
{
    private float _stunUntil;

    public bool IsStunned => Time.time < _stunUntil;

    public void ApplyStun(float seconds)
    {
        float dur = Mathf.Max(0f, seconds);
        _stunUntil = Mathf.Max(_stunUntil, Time.time + dur);
        // Try to stop immediate movement if a Rigidbody2D exists
        var rb = GetComponent<Rigidbody2D>();
        if (rb != null)
        {
            rb.linearVelocity = Vector2.zero;
        }
    }
}
