using UnityEngine;

/// Attach to the payload to relay trigger events up to the MechanicHost and its mechanics.
public class PayloadTriggerRelay : MonoBehaviour
{
    private MechanicHost _host;
    public bool debugLogs = false;

    private void Awake()
    {
        _host = GetComponentInParent<MechanicHost>();
        if (_host != null)
            debugLogs = _host.debugLogs;
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
        if (_host != null)
        {
            _host.SendMessage(
                "OnPayloadTriggerEnter2D",
                other,
                SendMessageOptions.DontRequireReceiver
            );
        }
        else
        {
            gameObject.SendMessageUpwards(
                "OnPayloadTriggerEnter2D",
                other,
                SendMessageOptions.DontRequireReceiver
            );
        }
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
        if (_host != null)
        {
            _host.SendMessage(
                "OnPayloadTriggerStay2D",
                other,
                SendMessageOptions.DontRequireReceiver
            );
        }
        else
        {
            gameObject.SendMessageUpwards(
                "OnPayloadTriggerStay2D",
                other,
                SendMessageOptions.DontRequireReceiver
            );
        }
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
        if (_host != null)
        {
            _host.SendMessage(
                "OnPayloadTriggerExit2D",
                other,
                SendMessageOptions.DontRequireReceiver
            );
        }
        else
        {
            gameObject.SendMessageUpwards(
                "OnPayloadTriggerExit2D",
                other,
                SendMessageOptions.DontRequireReceiver
            );
        }
    }
}
