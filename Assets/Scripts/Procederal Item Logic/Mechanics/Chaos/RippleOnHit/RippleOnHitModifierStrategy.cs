using UnityEngine;

namespace Game.Procederal.Core.Builders.Modifiers
{
    public class RippleOnHitModifierStrategy : IModifierStrategy
    {
        public Game.Procederal.MechanicKind Kind => Game.Procederal.MechanicKind.RippleOnHit;

        public void Apply(
            Game.Procederal.ProcederalItemGenerator generator,
            GameObject target,
            Game.Procederal.ItemParams parameters
        )
        {
            if (generator == null || target == null)
                return;

            if (generator.HasMechanic(target, "Whip"))
            {
                generator.Log("Skipping incompatible modifier 'RippleOnHit' on Whip.");
                return;
            }

            generator.AddMechanicByName(
                target,
                "RippleOnHit",
                new (string key, object val)[]
                {
                    ("debugLogs", parameters.debugLogs || generator.debugLogs),
                }
            );
        }
    }
}
