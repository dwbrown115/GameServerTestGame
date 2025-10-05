using UnityEngine;

namespace Game.Procederal.Api
{
    /// Tag component applied to sequence-spawned children.
    /// Provides the zero-based index within the sequence and the total count captured at spawn time.
    [DisallowMultipleComponent]
    public class SequenceIndexTag : MonoBehaviour
    {
        public int index;
        public int total;
    }
}
