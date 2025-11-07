using System.Collections.Generic;

namespace Game.Procederal.Core.Builders.Modifiers
{
    /// Public descriptor used to convey secondary mechanic settings to modifier strategies.
    public class SecondaryMechanicSettingDescriptor
    {
        public string MechanicName;
        public Dictionary<string, object> Properties;
        public Dictionary<string, object> Overrides;
    }
}
