namespace Game.Procederal.Core
{
    /// Provides an initialized mechanic catalog instance for runtime systems.
    public interface IMechanicCatalogProvider
    {
        IMechanicCatalog Catalog { get; }
    }
}
