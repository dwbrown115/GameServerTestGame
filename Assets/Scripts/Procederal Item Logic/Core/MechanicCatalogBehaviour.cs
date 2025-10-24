using UnityEngine;

namespace Game.Procederal.Core
{
    /// Unity-friendly provider that bootstraps the MechanicsRegistry and exposes it via IMechanicCatalog.
    [DefaultExecutionOrder(-500)]
    public sealed class MechanicCatalogBehaviour
        : MonoBehaviour,
            IMechanicCatalogProvider,
            IMechanicCatalogInitializer
    {
        [Tooltip(
            "Optional override catalog for primary mechanics. Leave null to rely on OTA/Resources."
        )]
        public TextAsset primaryCatalogOverride;

        [Tooltip("Optional override catalog for modifiers. Leave null to rely on OTA/Resources.")]
        public TextAsset modifierCatalogOverride;

        [Tooltip("If true, ensure the registry is initialized during Awake.")]
        public bool initializeOnAwake = true;

        public IMechanicCatalog Catalog => MechanicsRegistry.Instance;

        private void Awake()
        {
            MechanicsRegistry.VerboseLogging = false;
            if (!initializeOnAwake)
                return;

            MechanicsRegistry.Instance.EnsureInitialized(
                primaryCatalogOverride,
                modifierCatalogOverride
            );
        }

        public void EnsureReady()
        {
            MechanicsRegistry.VerboseLogging = false;
            MechanicsRegistry.Instance.EnsureInitialized(
                primaryCatalogOverride,
                modifierCatalogOverride
            );
        }
    }
}
