using UnityEngine;

namespace Game.Procederal.Core.Builders.Modifiers
{
    /// Defines how to apply a specific modifier mechanic to a generated payload.
    public interface IModifierStrategy
    {
        Game.Procederal.MechanicKind Kind { get; }

        void Apply(
            Game.Procederal.ProcederalItemGenerator generator,
            GameObject target,
            Game.Procederal.ItemParams parameters
        );
    }
}
