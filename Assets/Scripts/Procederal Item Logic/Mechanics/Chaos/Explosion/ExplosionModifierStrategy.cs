using UnityEngine;

namespace Game.Procederal.Core.Builders.Modifiers
{
    public class ExplosionModifierStrategy : IModifierStrategy
    {
        public Game.Procederal.MechanicKind Kind => Game.Procederal.MechanicKind.Explosion;

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
                "Explosion",
                new (string key, object val)[]
                {
                    ("debugLogs", parameters.debugLogs || generator.debugLogs),
                }
            );
        }
    }
}
