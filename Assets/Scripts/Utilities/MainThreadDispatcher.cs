using System;
using System.Collections.Concurrent;
using UnityEngine;

// Simple main-thread dispatcher to run actions from background threads (e.g., WebSocket callbacks)
public static class MainThreadDispatcher
{
    private class DispatcherRunner : MonoBehaviour
    {
        private void Update()
        {
            while (_queue.TryDequeue(out var action))
            {
                try
                {
                    action?.Invoke();
                }
                catch (Exception ex)
                {
                    Debug.LogError($"MainThreadDispatcher: {ex.Message}\n{ex.StackTrace}");
                }
            }
        }
    }

    private static readonly ConcurrentQueue<Action> _queue = new ConcurrentQueue<Action>();
    private static bool _initialized;

    private static void EnsureInitialized()
    {
        if (_initialized)
            return;
        var go = new GameObject("MainThreadDispatcher");
        go.hideFlags = HideFlags.HideAndDontSave;
        UnityEngine.Object.DontDestroyOnLoad(go);
        go.AddComponent<DispatcherRunner>();
        _initialized = true;
    }

    public static void Enqueue(Action action)
    {
        EnsureInitialized();
        _queue.Enqueue(action);
    }
}
