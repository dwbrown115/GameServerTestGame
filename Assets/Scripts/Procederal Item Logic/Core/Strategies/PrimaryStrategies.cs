using System.Collections.Generic;

namespace Game.Procederal.Core.Builders.Strategies
{
    public static class PrimaryStrategies
    {
        private static Dictionary<Game.Procederal.MechanicKind, IPrimaryStrategy> _map;

        public static void Register(IPrimaryStrategy strategy)
        {
            if (strategy == null)
                return;
            if (_map == null)
                _map = new Dictionary<Game.Procederal.MechanicKind, IPrimaryStrategy>();
            _map[strategy.Kind] = strategy;
        }

        public static IPrimaryStrategy Get(Game.Procederal.MechanicKind kind)
        {
            if (_map == null)
                _map = new Dictionary<Game.Procederal.MechanicKind, IPrimaryStrategy>();
            _map.TryGetValue(kind, out var s);
            return s;
        }
    }
}
