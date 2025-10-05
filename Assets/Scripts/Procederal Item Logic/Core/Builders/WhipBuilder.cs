using System.Collections.Generic;
using UnityEngine;

namespace Game.Procederal.Core.Builders
{
    public class WhipBuilder : IPrimaryBuilder
    {
        public Game.Procederal.MechanicKind Kind => Game.Procederal.MechanicKind.Whip;

        public void Build(
            Game.Procederal.ProcederalItemGenerator gen,
            GameObject root,
            Game.Procederal.ItemInstruction instruction,
            Game.Procederal.ItemParams p,
            List<GameObject> subItems
        )
        {
            gen.BuildWhip(root, instruction, p, subItems);
        }
    }
}
