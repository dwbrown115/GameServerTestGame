using UnityEngine;

namespace Game.Procederal.Core.Builders
{
    /// <summary>
    /// Centralizes child-count overrides so inspector and generator level clamps plug into existing pipelines.
    /// </summary>
    public static class BuilderChildCountHelper
    {
        /// <summary>
        /// Provides the fallback count to use when JSON does not specify an explicit child count.
        /// Inspector level overrides (ItemParams.subItemCount) win over generator defaults.
        /// </summary>
        public static int ResolveFallbackCount(
            Game.Procederal.ItemParams p,
            Game.Procederal.ProcederalItemGenerator gen,
            int defaultValue
        )
        {
            if (p != null && p.subItemCount > 0)
                return Mathf.Max(1, p.subItemCount);
            if (gen != null && gen.generatorChildrenCount > 0)
                return Mathf.Max(1, gen.generatorChildrenCount);
            return Mathf.Max(1, defaultValue);
        }

        /// <summary>
        /// Applies runtime overrides after the JSON pipeline decides on a count.
        /// </summary>
        public static int ResolveFinalCount(
            int resolvedCount,
            Game.Procederal.ItemParams p,
            Game.Procederal.ProcederalItemGenerator gen
        )
        {
            if (p != null && p.subItemCount > 0)
                return Mathf.Max(1, p.subItemCount);
            if (gen != null && gen.generatorChildrenCount > 0)
                return Mathf.Max(1, gen.generatorChildrenCount);
            return Mathf.Max(1, resolvedCount);
        }
    }
}
