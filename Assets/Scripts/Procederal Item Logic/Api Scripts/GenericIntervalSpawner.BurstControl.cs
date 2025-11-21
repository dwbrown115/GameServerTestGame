using Game.Procederal.Core;
using UnityEngine;

namespace Game.Procederal.Api
{
    /// Burst limiting and child tracking helpers for GenericIntervalSpawner.
    public partial class GenericIntervalSpawner
    {
        public void LimitBursts(int burstCount)
        {
            if (burstCount <= 0)
            {
                _burstLimitActive = false;
                _maxBurstLimit = -1;
                StopAndDisable();
                return;
            }

            _burstLimitActive = true;
            _maxBurstLimit = burstCount;
            if (_executedBursts >= _maxBurstLimit)
                StopAndDisable();
        }

        public void ExecuteSingleBurstAndDisable()
        {
            _burstLimitActive = true;
            _maxBurstLimit = 1;

            if (!isActiveAndEnabled)
                enabled = true;

            if (_executedBursts == 0 && !_stopped)
                SpawnBurst();

            StopAndDisable();
        }

        private void StopAndDisable()
        {
            if (_stopped)
                return;
            _stopped = true;
            enabled = false;
        }

        private bool EnsureChildSlotAvailable()
        {
            if (!enforceActiveChildLimit)
                return true;

            PruneTrackedChildren();
            int limit = GetChildLimit();
            if (limit <= 0)
                return true;

            if (_trackedChildren.Count < limit)
                return true;

            if (!recycleOldestChild)
            {
                if (debugLogs)
                {
                    Debug.Log(
                        $"[GenericIntervalSpawner] Active child limit {limit} reached; skipping spawn.",
                        this
                    );
                }
                return false;
            }

            Transform toRelease = null;
            for (int i = 0; i < _trackedChildren.Count; i++)
            {
                var candidate = _trackedChildren[i];
                if (candidate == null)
                    continue;
                toRelease = candidate;
                _trackedChildren.RemoveAt(i);
                _trackedChildSet.Remove(candidate);
                break;
            }

            if (toRelease != null)
            {
                if (debugLogs)
                {
                    Debug.Log(
                        $"[GenericIntervalSpawner] Releasing '{toRelease.name}' to respect limit {limit}.",
                        this
                    );
                }

                if (generator != null)
                {
                    generator.ReleaseTree(toRelease.gameObject);
                }
                else
                {
#if UNITY_EDITOR
                    if (!Application.isPlaying)
                        Object.DestroyImmediate(toRelease.gameObject);
                    else
#endif
                        Object.Destroy(toRelease.gameObject);
                }
            }

            PruneTrackedChildren();
            return _trackedChildren.Count < limit;
        }

        private int GetChildLimit()
        {
            int limit = maxActiveChildren;
            if (limit <= 0)
                limit = Mathf.Max(1, countPerInterval);
            return limit;
        }

        private void TrackSpawnedChild(GameObject go)
        {
            if (go == null)
                return;

            var child = go.transform;
            if (_trackedChildSet.Add(child))
                _trackedChildren.Add(child);

            _pooledChildSet.Remove(go);

            var tracker = child.GetComponent<SpawnedChildTracker>();
            if (tracker == null)
                tracker = child.gameObject.AddComponent<SpawnedChildTracker>();
            tracker.Initialize(this);
        }

        private void HandleChildReturned(Transform child, bool destroyed)
        {
            if (child == null)
                return;
            var go = child.gameObject;

            if (_trackedChildSet.Remove(child))
            {
                for (int i = _trackedChildren.Count - 1; i >= 0; i--)
                {
                    if (_trackedChildren[i] == child)
                    {
                        _trackedChildren.RemoveAt(i);
                        break;
                    }
                }
            }

            if (go == null)
                return;

            if (destroyed)
            {
                _pooledChildSet.Remove(go);
                return;
            }

            if (_pooledChildSet.Add(go))
                _pooledChildren.Enqueue(go);
        }

        private void PruneTrackedChildren()
        {
            for (int i = _trackedChildren.Count - 1; i >= 0; i--)
            {
                var child = _trackedChildren[i];
                if (child == null || !child.gameObject.activeInHierarchy)
                {
                    _trackedChildSet.Remove(child);
                    _trackedChildren.RemoveAt(i);
                }
            }

            if (!parentSpawnedToSpawner)
                return;

            for (int i = 0; i < transform.childCount; i++)
            {
                var child = transform.GetChild(i);
                if (child == null)
                    continue;
                if (_trackedChildSet.Add(child))
                    _trackedChildren.Add(child);
            }
        }

        private void ClearTrackedChildren()
        {
            _trackedChildren.Clear();
            _trackedChildSet.Clear();
        }

        private void RebuildTrackedChildren()
        {
            ClearTrackedChildren();
            if (!parentSpawnedToSpawner)
                return;

            for (int i = 0; i < transform.childCount; i++)
            {
                var child = transform.GetChild(i);
                if (child == null)
                    continue;
                _trackedChildren.Add(child);
                _trackedChildSet.Add(child);
            }
        }

        private sealed class SpawnedChildTracker : MonoBehaviour, IPooledPayloadResettable
        {
            private GenericIntervalSpawner _owner;

            public void Initialize(GenericIntervalSpawner owner)
            {
                _owner = owner;
            }

            public void ResetForPool()
            {
                _owner = null;
            }

            private void OnDisable()
            {
                _owner?.HandleChildReturned(transform, destroyed: false);
            }

            private void OnDestroy()
            {
                _owner?.HandleChildReturned(transform, destroyed: true);
            }
        }
    }
}
