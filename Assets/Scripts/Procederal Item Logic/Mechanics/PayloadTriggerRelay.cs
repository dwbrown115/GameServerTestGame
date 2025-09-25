using UnityEngine;

/// Attach to the payload to relay trigger events up the hierarchy to any listeners (MechanicRunner or others).
public class PayloadTriggerRelay : MonoBehaviour
{
    public bool debugLogs = false;

    private void Awake()
    {
        // Optionally, pick up a debug flag from a component on parents if present
        var runner = GetComponentInParent<Game.Procederal.Api.MechanicRunner>();
        if (runner != null)
            debugLogs = runner.debugLogs;
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (debugLogs && other != null)
        {
            Debug.Log(
                $"[PayloadTriggerRelay] {name} ENTER with {other.name} tag={other.tag} layer={other.gameObject.layer}",
                this
            );
        }
        gameObject.SendMessageUpwards(
            "OnPayloadTriggerEnter2D",
            other,
            SendMessageOptions.DontRequireReceiver
        );
    }

    private void OnTriggerStay2D(Collider2D other)
    {
        if (debugLogs && other != null)
        {
            Debug.Log(
                $"[PayloadTriggerRelay] {name} STAY with {other.name} tag={other.tag} layer={other.gameObject.layer}",
                this
            );
        }
        gameObject.SendMessageUpwards(
            "OnPayloadTriggerStay2D",
            other,
            SendMessageOptions.DontRequireReceiver
        );
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        if (debugLogs && other != null)
        {
            Debug.Log(
                $"[PayloadTriggerRelay] {name} EXIT with {other.name} tag={other.tag} layer={other.gameObject.layer}",
                this
            );
        }
        gameObject.SendMessageUpwards(
            "OnPayloadTriggerExit2D",
            other,
            SendMessageOptions.DontRequireReceiver
        );
    }
}
