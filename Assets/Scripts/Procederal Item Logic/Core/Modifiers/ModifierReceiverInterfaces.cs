using UnityEngine;

namespace Game.Procederal.Core
{
    /// Defines a contract for components that can receive modifier specifications at runtime.
    public interface IModifierReceiver
    {
        void AddModifierSpec(string mechanicName, params (string key, object val)[] settings);
    }

    /// Provides the transform that should own modifiers applied via IModifierReceiver.
    public interface IModifierOwnerProvider
    {
        Transform ModifierOwner { get; }
    }

    /// Allows modifier routing to enable aim assistance (e.g., Track) without reflection.
    public interface IAimAtNearestEnemyToggle
    {
        bool AimAtNearestEnemy { get; set; }
    }
}
