using UnityEngine;

public interface IMechanic
{
    void Initialize(MechanicContext ctx);
    void Tick(float dt);
}

public interface IMechanicInitializeOrder
{
    int InitializeOrder { get; }
}

public interface IMechanicActivationGuard
{
    bool IsMechanicReady(out string reason);
}
