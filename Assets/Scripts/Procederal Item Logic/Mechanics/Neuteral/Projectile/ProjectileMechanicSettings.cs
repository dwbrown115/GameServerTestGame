using System.Collections.Generic;
using Game.Procederal.Core;
using UnityEngine;

namespace Mechanics.Neuteral
{
    public static class ProjectileMechanicSettings
    {
        public static void Apply(ProjectileMechanic comp, IDictionary<string, object> s)
        {
            if (comp == null || s == null)
                return;
            if (s.TryGetValue("direction", out var dirRaw) && dirRaw is Vector2 dir)
                comp.direction = dir;
            comp.speed = MechanicSettingNormalizer.Speed(s, "speed", comp.speed);
            comp.damage = MechanicSettingNormalizer.Damage(s, "damage", comp.damage);
            comp.disableSelfSpeed = MechanicSettingNormalizer.Bool(
                s,
                "disableSelfSpeed",
                comp.disableSelfSpeed
            );
            comp.requireMobTag = MechanicSettingNormalizer.Bool(
                s,
                "requireMobTag",
                comp.requireMobTag
            );
            comp.excludeOwner = MechanicSettingNormalizer.Bool(
                s,
                "excludeOwner",
                comp.excludeOwner
            );
            comp.destroyOnHit = MechanicSettingNormalizer.Bool(
                s,
                "destroyOnHit",
                comp.destroyOnHit
            );
            comp.debugLogs = MechanicSettingNormalizer.Bool(s, "debugLogs", comp.debugLogs);
            comp.spriteType = MechanicSettingNormalizer.String(s, "spriteType", comp.spriteType);
            comp.customSpritePath = MechanicSettingNormalizer.String(
                s,
                "customSpritePath",
                comp.customSpritePath
            );
            comp.spriteColor = MechanicSettingNormalizer.Color(s, "spriteColor", comp.spriteColor);
        }
    }
}
