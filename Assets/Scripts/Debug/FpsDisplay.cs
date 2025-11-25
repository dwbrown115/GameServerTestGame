using TMPro;
using UnityEngine;

namespace Game.Debugging
{
    /// <summary>
    /// Minimal FPS overlay for quick in-editor diagnostics. Attach to a TextMeshProUGUI
    /// (or any TMP_Text) and it will keep the label updated with the current frame time,
    /// average FPS, and 1% low snapshot taken from a rolling buffer.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(TMP_Text))]
    public sealed class FpsDisplay : MonoBehaviour
    {
        [Tooltip("Optional explicit text component. Defaults to the TMP_Text on this GameObject.")]
        public TMP_Text target;

        [Tooltip("Seconds used for exponential smoothing of deltaTime. Set near zero for raw FPS.")]
        [Range(0f, 1f)]
        public float smoothingWindow = 0.15f;

        [Tooltip("Display template. {0}=ms, {1}=fps, {2}=avg fps, {3}=1% low fps")]
        public string displayFormat = "{0:0.0} ms  ({1:0} FPS)  |  Avg {2:0}  |  1% {3:0}";

        [Tooltip("How many recent frames to store for percentile math.")]
        [Range(32, 4096)]
        public int percentileSampleSize = 512;

        private float _smoothedDeltaTime;
        private float[] _fpsSamples;
        private float[] _sortedSamples;
        private int _sampleWriteIndex;
        private int _sampleCount;
        private double _fpsSum;
        private long _fpsTotalSamples;

        private void Awake()
        {
            if (target == null)
                target = GetComponent<TMP_Text>();
            _smoothedDeltaTime = Time.unscaledDeltaTime;
        }

        private void Update()
        {
            if (target == null)
                return;

            float dt = Mathf.Max(0.0001f, Time.unscaledDeltaTime);
            if (smoothingWindow <= 0f)
            {
                _smoothedDeltaTime = dt;
            }
            else
            {
                float lerp = Mathf.Clamp01(Time.unscaledDeltaTime / smoothingWindow);
                _smoothedDeltaTime = Mathf.Lerp(_smoothedDeltaTime, dt, lerp);
            }

            float smoothedFps = 1f / _smoothedDeltaTime;
            float ms = _smoothedDeltaTime * 1000f;

            // Track raw FPS for averages/percentiles (avoid smoothing bias)
            float rawFps = 1f / dt;
            _fpsSum += rawFps;
            _fpsTotalSamples++;

            EnsureSampleBuffers();
            _fpsSamples[_sampleWriteIndex] = rawFps;
            _sampleWriteIndex = (_sampleWriteIndex + 1) % _fpsSamples.Length;
            _sampleCount = Mathf.Min(_sampleCount + 1, _fpsSamples.Length);

            float avgFps = _fpsTotalSamples > 0 ? (float)(_fpsSum / _fpsTotalSamples) : rawFps;
            float onePercentLow = CalculateOnePercentLow(rawFps);

            target.SetText(displayFormat, ms, smoothedFps, avgFps, onePercentLow);
        }

        private void EnsureSampleBuffers()
        {
            if (percentileSampleSize <= 0)
                percentileSampleSize = 256;

            if (_fpsSamples == null || _fpsSamples.Length != percentileSampleSize)
            {
                _fpsSamples = new float[percentileSampleSize];
                _sortedSamples = new float[percentileSampleSize];
                _sampleWriteIndex = 0;
                _sampleCount = 0;
            }
            else if (_sortedSamples == null || _sortedSamples.Length != percentileSampleSize)
            {
                _sortedSamples = new float[percentileSampleSize];
            }
        }

        private float CalculateOnePercentLow(float fallback)
        {
            if (_sampleCount == 0)
                return fallback;

            if (_sortedSamples == null || _sortedSamples.Length != _fpsSamples.Length)
                _sortedSamples = new float[_fpsSamples.Length];

            System.Array.Copy(_fpsSamples, 0, _sortedSamples, 0, _sampleCount);
            System.Array.Sort(_sortedSamples, 0, _sampleCount);

            int idx = Mathf.Clamp(Mathf.FloorToInt(_sampleCount * 0.01f), 0, _sampleCount - 1);
            return _sortedSamples[idx];
        }
    }
}
