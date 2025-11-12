using UnityEngine;

namespace Game.Procederal.Core
{
    /// Provides helper methods so mechanics can release generated payloads back to the generator pool.
    public static class MechanicLifecycleUtility
    {
        public static void Release(GameObject go, bool immediate = false)
        {
            if (go == null)
                return;

            var handle = go.GetComponent<GeneratedObjectHandle>();
            if (handle != null && handle.Owner != null)
            {
                handle.Owner.ReleaseTree(go);
                return;
            }

            if (immediate)
                Object.DestroyImmediate(go);
            else
                Object.Destroy(go);
        }

        public static void Release(Component component, bool immediate = false)
        {
            if (component == null)
                return;
            Release(component.gameObject, immediate);
        }
    }
}
