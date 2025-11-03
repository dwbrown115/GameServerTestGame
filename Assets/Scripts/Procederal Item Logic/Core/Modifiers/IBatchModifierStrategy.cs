using System.Collections.Generic;
using UnityEngine;

namespace Game.Procederal.Core.Builders.Modifiers
{
    /// Supports applying a modifier to a group of generated payloads in a single pass.
    public interface IBatchModifierStrategy : IModifierStrategy
    {
        void ApplyToGroup(
            Game.Procederal.ProcederalItemGenerator generator,
            List<GameObject> targets,
            Game.Procederal.ItemParams parameters
        );
    }
}
