using UnityEngine;

/// Simple helper to assign a target to a MechanicHost at runtime.
public class MechanicTargetSetter : MonoBehaviour
{
    public MechanicHost host;
    public Transform target;

    private void Reset()
    {
        if (host == null)
            host = GetComponent<MechanicHost>();
    }

    private void Start()
    {
        if (host == null)
            host = GetComponent<MechanicHost>();
        if (host != null)
            host.GetType(); // no-op to keep reference
        if (host != null)
            host.SendMessage("SetTarget", target, SendMessageOptions.DontRequireReceiver);
    }
}
