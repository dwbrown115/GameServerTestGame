using System.Collections.Generic;

namespace Game.Procederal.Core
{
    /// Abstraction over mechanic lookup/merging so runtime systems can resolve
    /// mechanic metadata without dealing with manifest bootstrap concerns.
    public interface IMechanicCatalog
    {
        bool TryGetPath(string mechanicName, out string path);
        Dictionary<string, object> GetMergedSettings(string mechanicName);
        Dictionary<string, object> GetKvpArray(string mechanicName, string arrayName);
        List<string> GetIncompatibleWith(string mechanicName);
    }
}
