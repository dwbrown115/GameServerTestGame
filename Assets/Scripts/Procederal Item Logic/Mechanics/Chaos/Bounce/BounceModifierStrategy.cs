using UnityEngine;

namespace Game.Procederal.Core.Builders.Modifiers
{
    public class BounceModifierStrategy : IModifierStrategy
    {
        public Game.Procederal.MechanicKind Kind => Game.Procederal.MechanicKind.Bounce;

        public void Apply(
            Game.Procederal.ProcederalItemGenerator generator,
            GameObject target,
            Game.Procederal.ItemParams parameters
        )
        {
            if (generator == null || target == null)
                return;

            if (generator.HasMechanic(target, "Aura"))
            {
                generator.Log("Skipping incompatible modifier 'Bounce' on Aura.");
                return;
            }
            if (generator.HasMechanic(target, "Strike"))
            {
                generator.Log("Skipping incompatible modifier 'Bounce' on Strike.");
                return;
            }
            if (generator.HasMechanic(target, "Whip"))
            {
                generator.Log("Skipping incompatible modifier 'Bounce' on Whip.");
                return;
            }
            if (generator.HasMechanic(target, "Ripple"))
            {
                generator.Log("Skipping incompatible modifier 'Bounce' on Ripple.");
                return;
            }

            generator.AddMechanicByName(target, "Bounce", System.Array.Empty<(string, object)>());
        }
    }
}
