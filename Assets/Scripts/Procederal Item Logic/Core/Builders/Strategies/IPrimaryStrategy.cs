using System.Collections.Generic;
using UnityEngine;

namespace Game.Procederal.Core.Builders.Strategies
{
    /// Defines how to build a specific primary kind. Strategies encapsulate payload creation
    /// and optional spawner setup, while modifier application remains generic via the generator
    /// helper or a future ModifierEngine.
    public interface IPrimaryStrategy
    {
        Game.Procederal.MechanicKind Kind { get; }
        void Build(
            Game.Procederal.ProcederalItemGenerator gen,
            GameObject root,
            Game.Procederal.ItemInstruction instruction,
            Game.Procederal.ItemParams p,
            List<GameObject> subItems
        );
    }
}
