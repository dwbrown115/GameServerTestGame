using System.Collections.Generic;
using UnityEngine;

namespace Game.Procederal.Core.Builders
{
    public class BeamBuilder : IPrimaryBuilder
    {
        public Game.Procederal.MechanicKind Kind => Game.Procederal.MechanicKind.Beam;

        public void Build(
            Game.Procederal.ProcederalItemGenerator gen,
            GameObject root,
            Game.Procederal.ItemInstruction instruction,
            Game.Procederal.ItemParams p,
            List<GameObject> subItems
        )
        {
            gen.BuildBeam(root, instruction, p, subItems);
        }
    }
}
