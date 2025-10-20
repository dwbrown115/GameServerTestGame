using System.Collections.Generic;
using Game.Procederal.Core;
using UnityEngine;

namespace Mechanics.Neuteral
{
    public static class OrbitMechanicSettings
    {
        public static void Apply(OrbitMechanic comp, IDictionary<string, object> s)
        {
            if (comp == null || s == null)
                return;
            comp.angularSpeedDeg = MechanicSettingNormalizer.Float(
                s,
                "angularSpeedDeg",
                comp.angularSpeedDeg
            );
            comp.radius = MechanicSettingNormalizer.Radius(s, "radius", comp.radius);
            comp.startAngleDeg = MechanicSettingNormalizer.Float(
                s,
                "startAngleDeg",
                comp.startAngleDeg
            );
            comp.debugLogs = MechanicSettingNormalizer.Bool(s, "debugLogs", comp.debugLogs);
        }
    }
}
