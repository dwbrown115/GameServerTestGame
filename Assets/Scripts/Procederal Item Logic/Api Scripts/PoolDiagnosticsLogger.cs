using System.Collections.Generic;
using System.Text;
using Game.Procederal.Core;
using UnityEngine;

namespace Game.Procederal.Api
{
    /// <summary>
    /// Periodically logs pooling statistics gathered from the active object factory.
    /// Attach this to a ProcederalItemGenerator or any GameObject and optionally
    /// assign a generator reference. Useful for verifying pooling budgets at runtime.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PoolDiagnosticsLogger : MonoBehaviour
    {
        [Tooltip(
            "ProcederalItemGenerator to query for its object factory (falls back to locator)."
        )]
        public Game.Procederal.ProcederalItemGenerator generator;

        [Tooltip("Seconds between automatic log dumps (0 disables interval logging).")]
        public float logIntervalSeconds = 5f;

        [Tooltip("Log immediately when enabled.")]
        public bool logOnEnable = true;

        [Tooltip("Specific pool keys to include (leave empty to log every key).")]
        public List<string> keyFilters = new List<string>();

        [Tooltip("Include pools with zero active+available counts when keyFilters is empty.")]
        public bool includeEmptyPools = false;

        private float _nextLogTime;

        private void Awake()
        {
            if (generator == null)
                generator = GetComponent<Game.Procederal.ProcederalItemGenerator>();
        }

        private void OnEnable()
        {
            _nextLogTime = Time.time + Mathf.Max(0f, logIntervalSeconds);
            if (logOnEnable)
                Dump("OnEnable");
        }

        private void Update()
        {
            if (logIntervalSeconds <= 0f)
                return;
            if (Time.time < _nextLogTime)
                return;
            Dump("Interval");
            _nextLogTime = Time.time + Mathf.Max(0.1f, logIntervalSeconds);
        }

        [ContextMenu("Dump Pool Stats Now")]
        public void DumpFromContextMenu()
        {
            Dump("ContextMenu");
        }

        private void Dump(string reason)
        {
            var diagnostics = ResolveDiagnostics();
            if (diagnostics == null)
                return;

            var builder = new StringBuilder();
            builder.Append("[PoolDiagnostics] ");
            builder.Append(reason);
            builder.Append(' ');
            builder.Append(Time.time.ToString("F2"));
            builder.Append("s\n");

            bool hasFilters = keyFilters != null && keyFilters.Count > 0;
            int rows = 0;
            if (hasFilters)
            {
                for (int i = 0; i < keyFilters.Count; i++)
                {
                    var key = keyFilters[i];
                    if (string.IsNullOrWhiteSpace(key))
                        continue;
                    var snapshot = diagnostics.GetStatistics(key);
                    if (!includeEmptyPools && snapshot.Active == 0 && snapshot.Available == 0)
                        continue;
                    AppendRow(builder, snapshot);
                    rows++;
                }
            }
            else
            {
                foreach (var snapshot in diagnostics.GetAllStatistics())
                {
                    if (!includeEmptyPools && snapshot.Active == 0 && snapshot.Available == 0)
                        continue;
                    AppendRow(builder, snapshot);
                    rows++;
                }
            }

            if (rows == 0)
            {
                builder.Append("(no pools matched filter)");
            }

            Debug.Log(builder.ToString(), this);
        }

        private static void AppendRow(StringBuilder builder, PoolStatisticsSnapshot snapshot)
        {
            builder.Append("- ");
            builder.Append(snapshot.Key);
            builder.Append(':');
            builder.Append(" active=");
            builder.Append(snapshot.Active);
            builder.Append(", available=");
            builder.Append(snapshot.Available);
            builder.Append(", peak=");
            builder.Append(snapshot.PeakActive);
            builder.Append(", created=");
            builder.Append(snapshot.TotalCreated);
            builder.Append(", reused=");
            builder.Append(snapshot.TotalReused);
            builder.Append(", returned=");
            builder.Append(snapshot.TotalReturned);
            builder.Append(", discarded=");
            builder.Append(snapshot.TotalDiscarded);
            builder.Append('\n');
        }

        private IPooledItemDiagnostics ResolveDiagnostics()
        {
            IItemObjectFactory factory = null;
            if (generator != null)
                factory = generator.GetActiveObjectFactory();
            factory ??= ItemObjectFactoryLocator.Factory;
            if (factory is IPooledItemDiagnostics diag)
                return diag;
            return null;
        }
    }
}
