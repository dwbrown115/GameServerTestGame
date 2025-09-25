using UnityEngine;

/// Simple helper to assign a target to mechanics/runners at runtime without depending on MechanicHost.
public class MechanicTargetSetter : MonoBehaviour
{
    [Tooltip("Optional specific receiver to send SetTarget to. If null, will send upwards.")]
    public Component targetReceiver;
    public Transform target;

    private void Reset()
    {
        // Nothing to auto-assign; prefer SendMessageUpwards pattern
    }

    private void Start()
    {
        if (targetReceiver != null)
        {
            targetReceiver.SendMessage("SetTarget", target, SendMessageOptions.DontRequireReceiver);
        }
        else
        {
            // Broadcast upwards so either MechanicRunner or any listener can capture it
            gameObject.SendMessageUpwards(
                "SetTarget",
                target,
                SendMessageOptions.DontRequireReceiver
            );
        }
    }
}
