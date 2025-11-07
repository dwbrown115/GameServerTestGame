using UnityEngine;

namespace Game.Procederal.Core.Builders.Modifiers
{
    public class TrackModifierStrategy : IModifierStrategy
    {
        public Game.Procederal.MechanicKind Kind => Game.Procederal.MechanicKind.Track;

        public void Apply(
            Game.Procederal.ProcederalItemGenerator generator,
            GameObject target,
            Game.Procederal.ItemParams parameters
        )
        {
            if (generator == null || target == null)
                return;

            if (generator.HasMechanic(target, "Strike"))
            {
                generator.Log("Skipping incompatible modifier 'Track' on Strike.");
                return;
            }
            if (generator.HasMechanic(target, "RipplePrimary"))
            {
                generator.Log("Skipping incompatible modifier 'Track' on RipplePrimary.");
                return;
            }

            generator.AddMechanicByName(target, "Track", System.Array.Empty<(string, object)>());
        }
    }
}
