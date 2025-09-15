using UnityEngine;

public interface IMechanic
{
    void Initialize(MechanicContext ctx);
    void Tick(float dt);
}
