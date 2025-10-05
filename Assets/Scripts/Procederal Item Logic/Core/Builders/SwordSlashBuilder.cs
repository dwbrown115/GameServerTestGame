using System.Collections.Generic;
using UnityEngine;

namespace Game.Procederal.Core.Builders
{
    public class SwordSlashBuilder : IPrimaryBuilder
    {
        public Game.Procederal.MechanicKind Kind => Game.Procederal.MechanicKind.SwordSlash;

        public void Build(
            Game.Procederal.ProcederalItemGenerator gen,
            GameObject root,
            Game.Procederal.ItemInstruction instruction,
            Game.Procederal.ItemParams p,
            List<GameObject> subItems
        )
        {
            gen.BuildSwordSlash(root, instruction, p, subItems);
        }
    }
}
