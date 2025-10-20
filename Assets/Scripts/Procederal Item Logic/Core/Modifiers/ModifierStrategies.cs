using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;

namespace Game.Procederal.Core.Builders.Modifiers
{
    public static class ModifierStrategies
    {
        private static readonly object _initLock = new object();
        private static Dictionary<Game.Procederal.MechanicKind, IModifierStrategy> _map;
        private static bool _initialized;

        private static void EnsureInitialized()
        {
            if (_initialized)
                return;
            lock (_initLock)
            {
                if (_initialized)
                    return;

                _map ??= new Dictionary<Game.Procederal.MechanicKind, IModifierStrategy>();

                var strategyType = typeof(IModifierStrategy);
                var assemblies = AppDomain.CurrentDomain.GetAssemblies();
                foreach (var asm in assemblies)
                {
                    Type[] types;
                    try
                    {
                        types = asm.GetTypes();
                    }
                    catch (ReflectionTypeLoadException ex)
                    {
                        types = ex.Types.Where(t => t != null).ToArray();
                    }

                    foreach (var type in types)
                    {
                        if (type == null || type.IsInterface || type.IsAbstract)
                            continue;
                        if (!strategyType.IsAssignableFrom(type))
                            continue;

                        try
                        {
                            var instance = Activator.CreateInstance(type) as IModifierStrategy;
                            if (instance == null)
                                continue;

                            if (
                                _map.TryGetValue(instance.Kind, out var existing)
                                && existing != null
                                && existing.GetType() != type
                            )
                            {
                                Debug.LogWarning(
                                    $"[ModifierStrategies] Replacing strategy for {instance.Kind} from {existing.GetType().Name} to {type.Name}."
                                );
                            }

                            _map[instance.Kind] = instance;
                        }
                        catch (Exception ex)
                        {
                            Debug.LogWarning(
                                $"[ModifierStrategies] Failed to instantiate strategy {type.FullName}: {ex.Message}"
                            );
                        }
                    }
                }

                _initialized = true;
            }
        }

        public static IModifierStrategy Get(Game.Procederal.MechanicKind kind)
        {
            EnsureInitialized();
            _map.TryGetValue(kind, out var strategy);
            return strategy;
        }

        public static void Register(IModifierStrategy strategy)
        {
            if (strategy == null)
                return;
            EnsureInitialized();
            _map[strategy.Kind] = strategy;
        }
    }
}
