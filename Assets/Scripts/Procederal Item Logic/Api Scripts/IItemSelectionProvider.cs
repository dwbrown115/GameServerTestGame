using Game.Procederal;

namespace Game.Procederal.Api
{
    /// Abstraction for selecting an item build (primary + modifiers + parameters).
    /// Implement this for online/server-driven selection. The controller will call this when running in online mode
    /// (i.e., GameMode.Offline is false if using the global toggle, or local offlineMode is false otherwise).
    public interface IItemSelectionProvider
    {
        /// Returns true and outputs a selection when available.
        bool TryGetSelection(out ItemInstruction instruction, out ItemParams parameters);
    }
}
