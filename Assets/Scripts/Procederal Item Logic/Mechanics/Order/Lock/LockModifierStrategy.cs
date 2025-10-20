using UnityEngine;

namespace Game.Procederal.Core.Builders.Modifiers
{
    public class LockModifierStrategy : IModifierStrategy
    {
        public Game.Procederal.MechanicKind Kind => Game.Procederal.MechanicKind.Lock;

        public void Apply(
            Game.Procederal.ProcederalItemGenerator generator,
            GameObject target,
            Game.Procederal.ItemParams parameters
        )
        {
            if (generator == null || target == null)
                return;

            generator.AddMechanicByName(target, "Lock", System.Array.Empty<(string, object)>());
        }
    }
}
