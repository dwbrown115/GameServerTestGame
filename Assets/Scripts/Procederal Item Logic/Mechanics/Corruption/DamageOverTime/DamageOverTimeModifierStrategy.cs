using UnityEngine;

namespace Game.Procederal.Core.Builders.Modifiers
{
    public class DamageOverTimeModifierStrategy : IModifierStrategy
    {
        public Game.Procederal.MechanicKind Kind => Game.Procederal.MechanicKind.DamageOverTime;

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
                generator.Log("Skipping incompatible modifier 'DamageOverTime' on Whip.");
                return;
            }

            generator.AddMechanicByName(
                target,
                "DamageOverTime",
                new (string key, object val)[]
                {
                    ("interval", parameters.drainInterval),
                    ("damagePerInterval", parameters.drainDamage),
                }
            );
        }
    }
}
