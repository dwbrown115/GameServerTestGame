using UnityEngine;

namespace Game.Procederal.Api
{
    /// Repeats a connected SequenceSpawnBehavior on a fixed interval.
    [DisallowMultipleComponent]
    public class SequenceIntervalRepeater : MonoBehaviour
    {
        public SequenceSpawnBehavior sequence;
        public float interval = 0.5f;
        public bool immediateFirstBurst = false;
        public bool active = true;
        public bool debugLogs = false;

        private float _timer;
        private bool _didFirst;

        private void OnEnable()
        {
            _timer = 0f;
            _didFirst = false;
            // Defer immediate-first handling to Update as well so runtime property assignment also works.
            if (immediateFirstBurst && active)
            {
                Fire();
                _didFirst = true;
                _timer = 0f;
            }
        }

        private void Update()
        {
            if (!active || sequence == null)
                return;
            _timer += Time.deltaTime;
            float minI = Mathf.Max(0.01f, interval);
            if (!_didFirst)
            {
                if (immediateFirstBurst)
                {
                    // Support immediate-first even when property was set after OnEnable
                    Fire();
                    _didFirst = true;
                    _timer = 0f;
                }
                else if (_timer >= minI)
                {
                    _timer = 0f;
                    Fire();
                    _didFirst = true;
                }
            }
            else
            {
                if (_timer >= minI)
                {
                    _timer = 0f;
                    Fire();
                }
            }
        }

        private void Fire()
        {
            if (sequence == null)
                return;
            if (debugLogs)
                Debug.Log("[SequenceIntervalRepeater] Firing sequence", this);
            sequence.BeginSequence();
        }
    }
}
