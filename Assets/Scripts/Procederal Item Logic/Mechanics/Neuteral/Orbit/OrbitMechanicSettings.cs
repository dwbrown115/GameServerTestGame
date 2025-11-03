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
                comp.angularSpeedDeg,
                "speed",
                "angularSpeedDeg"
            );
            comp.radius = MechanicSettingNormalizer.Radius(s, "radius", comp.radius);
            comp.startAngleDeg = MechanicSettingNormalizer.Float(
                s,
                "startAngleDeg",
                comp.startAngleDeg
            );
            comp.debugLogs = MechanicSettingNormalizer.Bool(s, "debugLogs", comp.debugLogs);
            string desiredPath = MechanicSettingNormalizer.String(s, "pathId", comp.pathId);
            desiredPath = MechanicSettingNormalizer.String(s, "orbitPath", desiredPath);
            desiredPath = MechanicSettingNormalizer.String(s, "OrbitPath", desiredPath);
            if (!string.IsNullOrWhiteSpace(desiredPath))
                comp.pathId = desiredPath;
            float rotation = MechanicSettingNormalizer.Float(
                s,
                comp.PathRotationDeg,
                "PathRotationDeg",
                "pathRotationDeg"
            );
            comp.PathRotationDeg = rotation;
        }
    }
}
