namespace Game.Procederal.Core
{
    /// Optional hook for providers that need to ensure catalog readiness before use.
    public interface IMechanicCatalogInitializer
    {
        void EnsureReady();
    }
}
