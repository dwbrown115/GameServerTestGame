using System.Collections.Generic;
using UnityEngine;

namespace Game.Procederal.Core.Builders.Strategies
{
    public class RippleStrategy : IPrimaryStrategy
    {
        public Game.Procederal.MechanicKind Kind => Game.Procederal.MechanicKind.Ripple;

        public void Build(
            Game.Procederal.ProcederalItemGenerator gen,
            GameObject root,
            Game.Procederal.ItemInstruction instruction,
            Game.Procederal.ItemParams p,
            List<GameObject> subItems
        )
        {
            gen.BuildRipple(root, instruction, p, subItems);
        }
    }
}
