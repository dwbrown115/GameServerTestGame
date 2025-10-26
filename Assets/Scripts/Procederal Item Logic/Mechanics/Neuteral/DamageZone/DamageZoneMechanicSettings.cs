using System.Collections.Generic;
using Game.Procederal.Core;
using UnityEngine;

namespace Mechanics.Neuteral
{
    /// Helper to map loosely typed JSON blobs onto DamageZoneMechanic instances.
    public static class DamageZoneMechanicSettings
    {
        public static void Apply(DamageZoneMechanic comp, IDictionary<string, object> settings)
        {
            if (comp == null || settings == null)
                return;

            comp.radius = MechanicSettingNormalizer.Radius(settings, "radius", comp.radius);
            comp.damagePerInterval = MechanicSettingNormalizer.Damage(
                settings,
                "damagePerInterval",
                comp.damagePerInterval
            );
            comp.interval = MechanicSettingNormalizer.Interval(
                settings,
                comp.interval,
                0.0001f,
                "damageInterval",
                "interval"
            );
            comp.initialDelay = MechanicSettingNormalizer.Duration(
                settings,
                "initialDelay",
                comp.initialDelay
            );
            comp.lifetimeSeconds = MechanicSettingNormalizer.Lifetime(
                settings,
                "lifetimeSeconds",
                comp.lifetimeSeconds
            );
            comp.destroyOnExpire = MechanicSettingNormalizer.Bool(
                settings,
                "destroyOnExpire",
                comp.destroyOnExpire
            );
            comp.disableOnExpire = MechanicSettingNormalizer.Bool(
                settings,
                "disableOnExpire",
                comp.disableOnExpire
            );
            comp.followOwner = MechanicSettingNormalizer.Bool(
                settings,
                "followOwner",
                comp.followOwner
            );
            comp.followTarget = MechanicSettingNormalizer.Bool(
                settings,
                "followTarget",
                comp.followTarget
            );
            comp.excludeOwner = MechanicSettingNormalizer.Bool(
                settings,
                "excludeOwner",
                comp.excludeOwner
            );
            comp.requireMobTag = MechanicSettingNormalizer.Bool(
                settings,
                "requireMobTag",
                comp.requireMobTag
            );
            comp.showVisualization = MechanicSettingNormalizer.Bool(
                settings,
                "showVisualization",
                comp.showVisualization
            );
            comp.vizColor = MechanicSettingNormalizer.Color(settings, "spriteColor", comp.vizColor);
            comp.vizColor = MechanicSettingNormalizer.Color(settings, "vizColor", comp.vizColor);
            comp.vizSortingOrder = MechanicSettingNormalizer.Int(
                settings,
                "vizSortingOrder",
                comp.vizSortingOrder
            );
            comp.debugLogs = MechanicSettingNormalizer.Bool(settings, "debugLogs", comp.debugLogs);

            float offsetX = MechanicSettingNormalizer.Float(
                settings,
                comp.worldOffset.x,
                "offsetX",
                "offset_x",
                "worldOffsetX",
                "world_offset_x"
            );
            float offsetY = MechanicSettingNormalizer.Float(
                settings,
                comp.worldOffset.y,
                "offsetY",
                "offset_y",
                "worldOffsetY",
                "world_offset_y"
            );
            comp.worldOffset = new Vector2(offsetX, offsetY);

            int mask = MechanicSettingNormalizer.Int(
                settings,
                comp.targetLayers.value,
                "targetLayerMask",
                "targetLayers",
                "layerMask"
            );
            comp.targetLayers = mask;
        }
    }
}
