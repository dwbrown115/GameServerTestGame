using System.Collections.Generic;
using UnityEngine;

namespace Game.Procederal.Core.Builders
{
    public static class PrimaryBuilders
    {
        private static Dictionary<Game.Procederal.MechanicKind, IPrimaryBuilder> _map;

        public static IPrimaryBuilder Get(Game.Procederal.MechanicKind kind)
        {
            if (_map == null)
            {
                _map = new Dictionary<Game.Procederal.MechanicKind, IPrimaryBuilder>();
                // Register available builders here
                var generic = new Game.Procederal.Core.Builders.GenericPrimaryBuilder();
                // Register the generic builder for all supported kinds
                _map[Game.Procederal.MechanicKind.Projectile] = generic;
                _map[Game.Procederal.MechanicKind.Aura] = generic;
                _map[Game.Procederal.MechanicKind.Beam] = generic;
                _map[Game.Procederal.MechanicKind.Strike] = generic;
                _map[Game.Procederal.MechanicKind.Whip] = generic;
                _map[Game.Procederal.MechanicKind.Ripple] = generic;
                _map[Game.Procederal.MechanicKind.SwordSlash] = generic;
            }
            _map.TryGetValue(kind, out var b);
            return b;
        }
    }
}
