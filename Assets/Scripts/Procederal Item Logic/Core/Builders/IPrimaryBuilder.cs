using System.Collections.Generic;
using UnityEngine;

namespace Game.Procederal.Core.Builders
{
    public interface IPrimaryBuilder
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
