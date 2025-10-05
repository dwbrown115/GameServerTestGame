using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Game.Procederal.Core.Builders
{
    /// A unified builder that can construct any primary by delegating to the generator's
    /// internal per-primary methods. This lets us centralize modifier handling and registry wiring
    /// while keeping primary-specific construction in one place.
    public class GenericPrimaryBuilder : IPrimaryBuilder
    {
        // Kind is not used for routing; this builder is registered for all kinds.
        public Game.Procederal.MechanicKind Kind => Game.Procederal.MechanicKind.Projectile;

        private static bool _init;

        private static void EnsureStrategiesRegistered()
        {
            if (_init)
                return;
            _init = true;
            // Discover and register all IPrimaryStrategy implementations via reflection
            var strategyType = typeof(Strategies.IPrimaryStrategy);
            var asm = strategyType.Assembly;
            var types = asm.GetTypes()
                .Where(t => strategyType.IsAssignableFrom(t) && !t.IsInterface && !t.IsAbstract);
            foreach (var t in types)
            {
                try
                {
                    var instance = Activator.CreateInstance(t) as Strategies.IPrimaryStrategy;
                    if (instance != null)
                        Strategies.PrimaryStrategies.Register(instance);
                }
                catch (Exception ex)
                {
                    Debug.LogWarning(
                        $"Failed to register primary strategy {t.FullName}: {ex.Message}"
                    );
                }
            }
        }

        public void Build(
            Game.Procederal.ProcederalItemGenerator gen,
            GameObject root,
            Game.Procederal.ItemInstruction instruction,
            Game.Procederal.ItemParams p,
            List<GameObject> subItems
        )
        {
            // Prefer the single generic strategy when available; otherwise, fall back to specific strategies
            var generic = new Strategies.GenericPrimaryStrategy();
            generic.Build(gen, root, instruction, p, subItems);
        }
    }
}
