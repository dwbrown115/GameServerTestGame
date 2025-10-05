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
        /// Return true if this modifier should respond to damage reported for the given segment name.
        /// Example future usage: a Bounce-only-head modifier returns true for segment=="head".
        /// </summary>
        bool AppliesToSegment(string segmentName);

        /// <summary>
        /// Invoked after a damage tick when a segment registered >0 damage.
        /// segmentName: canonical identifier (e.g. "head", "tail").
        /// damage: amount of damage that segment inflicted this tick.
        /// beam: reference to the BeamMechanic issuing the callback (may be null in edge cases if destroyed mid-loop).
        /// </summary>
        void OnBeamSegmentDamage(string segmentName, int damage, Neuteral.BeamMechanic beam);
    }
}
