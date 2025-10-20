using System.Collections.Generic;
using UnityEngine;

namespace Game.Procederal.Core.Builders.Modifiers
{
    public class OrbitModifierStrategy : IModifierStrategy
    {
        public Game.Procederal.MechanicKind Kind => Game.Procederal.MechanicKind.Orbit;

        public void Apply(
            Game.Procederal.ProcederalItemGenerator generator,
            GameObject target,
            Game.Procederal.ItemParams parameters
        )
        {
            if (generator == null || target == null)
                return;

            if (generator.HasMechanic(target, "Beam"))
            {
                generator.Log("Skipping incompatible modifier 'Orbit' on Beam.");
                return;
            }
            if (generator.HasMechanic(target, "Whip"))
            {
                generator.Log("Skipping incompatible modifier 'Orbit' on Whip.");
                return;
            }
            if (generator.HasMechanic(target, "Ripple"))
            {
                generator.Log("Skipping incompatible modifier 'Orbit' on Ripple.");
                return;
            }
            if (generator.HasMechanic(target, "Strike"))
            {
                generator.Log("Skipping incompatible modifier 'Orbit' on Strike.");
                return;
            }

            generator.SetExistingMechanicSetting(target, "Projectile", "disableSelfSpeed", true);
            generator.AddMechanicByName(
                target,
                "Orbit",
                new (string key, object val)[]
                {
                    ("radius", parameters.orbitRadius),
                    (
                        "angularSpeedDeg",
                        parameters.orbitSpeedDeg > 0 ? parameters.orbitSpeedDeg : 90f
                    ),
                }
            );

            Dictionary<string, object> orbitJson = generator.LoadAndMergeJsonSettings("Orbit");
            if (orbitJson != null && orbitJson.TryGetValue("destroyOnHit", out var destroySetting))
            {
                bool destroyOnHit = false;
                if (destroySetting is bool b)
                    destroyOnHit = b;
                else if (destroySetting is string s && bool.TryParse(s, out var parsed))
                    destroyOnHit = parsed;

                generator.SetExistingMechanicSetting(
                    target,
                    "Projectile",
                    "destroyOnHit",
                    destroyOnHit
                );
            }
        }
    }
}
