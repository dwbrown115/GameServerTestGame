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
            if (immediateFirstBurst && active)
            {
                Fire();
                _didFirst = true;
            }
        }

        private void Update()
        {
            if (!active || sequence == null)
                return;
            _timer += Time.deltaTime;
            float minI = Mathf.Max(0.01f, interval);
            if (!_didFirst && !immediateFirstBurst)
            {
                if (_timer >= minI)
                {
                    _timer = 0f;
                    Fire();
                    _didFirst = true;
                }
            }
            else if (_didFirst)
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
