using System.Collections.Generic;
using UnityEngine;

namespace Game.Procederal.Core.Builders.Strategies
{
    public class ProjectileStrategy : IPrimaryStrategy
    {
        public Game.Procederal.MechanicKind Kind => Game.Procederal.MechanicKind.Projectile;

        public void Build(
            Game.Procederal.ProcederalItemGenerator gen,
            GameObject root,
            Game.Procederal.ItemInstruction instruction,
            Game.Procederal.ItemParams p,
            List<GameObject> subItems
        )
        {
            var builder = Core.Builders.PrimaryBuilders.Get(Kind);
            builder?.Build(gen, root, instruction, p, subItems);
        }
    }
}
