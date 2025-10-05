using System.Collections.Generic;
using UnityEngine;

namespace Game.Procederal.Core.Builders
{
    public class StrikeBuilder : IPrimaryBuilder
    {
        public Game.Procederal.MechanicKind Kind => Game.Procederal.MechanicKind.Strike;
        public void Build(Game.Procederal.ProcederalItemGenerator gen,
                          GameObject root,
                          Game.Procederal.ItemInstruction instruction,
                          Game.Procederal.ItemParams p,
                          List<GameObject> subItems)
        {
            gen.BuildStrike(root, instruction, p, subItems);
        }
    }
}
