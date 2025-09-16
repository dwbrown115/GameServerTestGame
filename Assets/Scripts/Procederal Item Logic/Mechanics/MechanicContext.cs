using UnityEngine;

/// Shared data passed to all mechanics in a host.
public class MechanicContext
{
    public Transform Owner;     // The GameObject this host is on
    public Transform Payload;   // The transform the mechanics will act upon (defaults to Owner)
    public Transform Target;    // Optional target for aim/orbit/etc

    public Rigidbody2D OwnerRb2D;   // Optional
    public Rigidbody2D PayloadRb2D; // Optional
}
