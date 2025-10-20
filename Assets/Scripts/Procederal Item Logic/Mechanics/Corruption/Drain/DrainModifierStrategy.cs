using UnityEngine;

namespace Game.Procederal.Core.Builders.Modifiers
{
    public class DrainModifierStrategy : IModifierStrategy
    {
        public Game.Procederal.MechanicKind Kind => Game.Procederal.MechanicKind.Drain;

        public void Apply(
            Game.Procederal.ProcederalItemGenerator generator,
            GameObject target,
            Game.Procederal.ItemParams parameters
        )
        {
            if (generator == null || target == null)
                return;

            generator.AddMechanicByName(
                target,
                "Drain",
                new (string key, object val)[]
                {
                    ("radius", parameters.drainRadius),
                    ("interval", Mathf.Max(0.01f, parameters.drainInterval)),
                    ("damagePerInterval", Mathf.Max(0, parameters.drainDamage)),
                    ("lifeStealRatio", Mathf.Clamp01(parameters.lifeStealRatio)),
                }
            );
        }
    }
}
