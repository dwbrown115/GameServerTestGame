using UnityEngine;

namespace Mechanics
{
    /// <summary>
    /// Contract for mechanics that want to react to beam segment damage events.
    /// Attach an implementation to the same GameObject as the BeamMechanic.
    /// BeamMechanic will call OnBeamSegmentDamage after each damage tick for segments it tracks (e.g. "head", "tail").
    /// Keep logic lightweight; expensive work should be deferred.
    /// </summary>
    public interface IBeamSegmentModifier
    {
        /// <summary>
        /// Return list (array) of segment identifiers this modifier wants to receive.
        /// Return null or empty to indicate ALL segments. Typical names: "head", "tail".
        /// Called once during caching/rescan (not per tick) so can allocate safely.
        /// </summary>
        string[] GetTargetSegments();

        /// <summary>
        /// Invoked after a damage tick for each targeted segment that dealt >0 damage.
        /// segmentName: canonical identifier (e.g. "head", "tail").
        /// damage: damage amount that segment contributed this tick.
        /// beam: originating beam reference.
        /// </summary>
        void OnBeamSegmentDamage(string segmentName, int damage, Neuteral.BeamMechanic beam);
    }
}
