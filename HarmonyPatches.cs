using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace KSPPhysProfiler
{
    public static class HarmonyPatches
    {
        private static Harmony harmonyInstance;
        public static int PatchedModuleCount { get; private set; }
        public static int PatchedPluginCount { get; private set; }

        public static void ApplyPatches()
        {
            if (harmonyInstance != null) return;

            try
            {
                harmonyInstance = new Harmony("com.kspphysprofiler.patch");

                MethodInfo prefixModule = typeof(HarmonyPatches).GetMethod(nameof(PartModule_Prefix), BindingFlags.Static | BindingFlags.NonPublic);
                MethodInfo postfixModule = typeof(HarmonyPatches).GetMethod(nameof(PartModule_Postfix), BindingFlags.Static | BindingFlags.NonPublic);

                MethodInfo prefixPlugin = typeof(HarmonyPatches).GetMethod(nameof(Plugin_Prefix), BindingFlags.Static | BindingFlags.NonPublic);
                MethodInfo postfixPlugin = typeof(HarmonyPatches).GetMethod(nameof(Plugin_Postfix), BindingFlags.Static | BindingFlags.NonPublic);

                MethodInfo prefixDispatcher = typeof(HarmonyPatches).GetMethod(nameof(TimingDispatcher_Prefix), BindingFlags.Static | BindingFlags.NonPublic);

                HarmonyMethod prefixModuleHM = new HarmonyMethod(prefixModule);
                HarmonyMethod postfixModuleHM = new HarmonyMethod(postfixModule);

                HarmonyMethod prefixPluginHM = new HarmonyMethod(prefixPlugin);
                HarmonyMethod postfixPluginHM = new HarmonyMethod(postfixPlugin);

                HarmonyMethod prefixDispatcherHM = new HarmonyMethod(prefixDispatcher);

                int moduleCount = 0;
                int pluginCount = 0;

                Assembly[] assemblies = AppDomain.CurrentDomain.GetAssemblies();

                foreach (Assembly assembly in assemblies)
                {
                    string name = assembly.GetName().Name;
                    if (name.StartsWith("System") || name.StartsWith("UnityEngine") || name.StartsWith("mscorlib") || name.StartsWith("Mono.") || name == "KSPPhysProfiler")
                    {
                        continue;
                    }

                    Type[] types;
                    try
                    {
                        types = assembly.GetTypes();
                    }
                    catch (ReflectionTypeLoadException ex)
                    {
                        types = ex.Types;
                    }
                    catch
                    {
                        continue;
                    }

                    if (types == null) continue;

                    foreach (Type t in types)
                    {
                        if (t == null || t.IsAbstract || !typeof(MonoBehaviour).IsAssignableFrom(t))
                        {
                            continue;
                        }

                        bool isPartModule = typeof(PartModule).IsAssignableFrom(t);
                        bool isTimingDispatcher = (assembly.GetName().Name == "Assembly-CSharp" && (
                            t.Name == "Timing0" || t.Name == "Timing1" || t.Name == "TimingPre" ||
                            t.Name == "Timing2" || t.Name == "Timing3" || t.Name == "Timing4" ||
                            t.Name == "TimingFI" || t.Name == "Timing5" || t.Name == "TimingManager"));

                        // Hook FixedUpdate, Update, LateUpdate for all, plus camera/render lifecycle methods for plugins (TUFX, Scatterer, etc.)
                        string[] methodsToHook = isPartModule
                            ? new string[] { "FixedUpdate", "Update", "LateUpdate" }
                            : new string[] { "FixedUpdate", "Update", "LateUpdate", "OnPreCull", "OnPreRender", "OnPostRender", "OnRenderImage" };

                        foreach (string methodName in methodsToHook)
                        {
                            MethodInfo mi = t.GetMethod(methodName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
                            if (mi != null && !mi.IsAbstract && !mi.ContainsGenericParameters)
                            {
                                try
                                {
                                    if (isPartModule)
                                    {
                                        harmonyInstance.Patch(mi, prefixModuleHM, postfixModuleHM);
                                        moduleCount++;
                                    }
                                    else if (isTimingDispatcher && (methodName == "FixedUpdate" || methodName == "Update" || methodName == "LateUpdate"))
                                    {
                                        // For timing dispatchers, penetrate the delegate invocation chain
                                        harmonyInstance.Patch(mi, prefixDispatcherHM, null);
                                        pluginCount++;
                                    }
                                    else
                                    {
                                        harmonyInstance.Patch(mi, prefixPluginHM, postfixPluginHM);
                                        pluginCount++;
                                    }
                                }
                                catch
                                {
                                    // Ignore individual method patch failures
                                }
                            }
                        }
                    }
                }

                PatchedModuleCount = moduleCount;
                PatchedPluginCount = pluginCount;
                UnityEngine.Debug.Log($"[KSPPhysProfiler] Patched {moduleCount} PartModule methods and {pluginCount} standalone Plugin methods!");
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogError($"[KSPPhysProfiler] Failed to apply Harmony patches: {ex}");
            }
        }

        private static void PartModule_Prefix(out long __state)
        {
            __state = ProfilerData.IsEnabled ? Stopwatch.GetTimestamp() : 0;
        }

        private static void PartModule_Postfix(PartModule __instance, MethodBase __originalMethod, long __state)
        {
            if (ProfilerData.IsEnabled && __state > 0 && __instance != null)
            {
                long elapsed = Stopwatch.GetTimestamp() - __state;
                string methodName = __originalMethod != null ? __originalMethod.Name : "Execute";
                ProfilerData.RecordModuleExecution(__instance, methodName, elapsed);
            }
        }

        private static void Plugin_Prefix(out long __state)
        {
            __state = ProfilerData.IsEnabled ? Stopwatch.GetTimestamp() : 0;
        }

        private static void Plugin_Postfix(MonoBehaviour __instance, MethodBase __originalMethod, long __state)
        {
            if (ProfilerData.IsEnabled && __state > 0 && __instance != null)
            {
                long elapsed = Stopwatch.GetTimestamp() - __state;
                string methodName = __originalMethod != null ? __originalMethod.Name : "Execute";
                ProfilerData.RecordPluginExecution(__instance, methodName, elapsed);
            }
        }

        private struct DispatcherCache
        {
            public PropertyInfo Prop;
            public Delegate CachedDelegate;
            public Delegate[] CachedList;
        }

        private static readonly Dictionary<string, DispatcherCache> dispatcherCache = new Dictionary<string, DispatcherCache>(StringComparer.Ordinal);

        private static bool TimingDispatcher_Prefix(MonoBehaviour __instance, MethodBase __originalMethod)
        {
            if (!ProfilerData.IsEnabled || __instance == null || __originalMethod == null) return true;

            string methodName = __originalMethod.Name;
            string propName = methodName == "FixedUpdate" ? "onFixedUpdate" : (methodName == "Update" ? "onUpdate" : "onLateUpdate");
            Type t = __instance.GetType();

            string cacheKey = t.FullName + "." + propName;
            dispatcherCache.TryGetValue(cacheKey, out DispatcherCache cache);

            if (cache.Prop == null)
            {
                cache.Prop = t.GetProperty(propName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                dispatcherCache[cacheKey] = cache;
            }

            if (cache.Prop == null) return true;

            Delegate del = cache.Prop.GetValue(__instance, null) as Delegate;
            if (del == null)
            {
                ProfilerData.RecordPluginExecution(__instance, methodName, 0);
                return false;
            }

            Delegate[] invocationList;
            if (object.ReferenceEquals(del, cache.CachedDelegate) && cache.CachedList != null)
            {
                invocationList = cache.CachedList;
            }
            else
            {
                invocationList = del.GetInvocationList();
                cache.CachedDelegate = del;
                cache.CachedList = invocationList;
                dispatcherCache[cacheKey] = cache;
            }

            if (invocationList == null || invocationList.Length == 0)
            {
                ProfilerData.RecordPluginExecution(__instance, methodName, 0);
                return false;
            }

            long totalStart = Stopwatch.GetTimestamp();

            for (int i = 0; i < invocationList.Length; i++)
            {
                var item = invocationList[i];
                if (item == null) continue;

                MethodInfo targetMethod = item.Method;
                long itemStart = Stopwatch.GetTimestamp();
                try
                {
                    var act = item as TimingManager.UpdateAction;
                    if (act != null)
                    {
                        act();
                    }
                    else
                    {
                        item.DynamicInvoke(null);
                    }
                }
                catch (Exception ex)
                {
                    UnityEngine.Debug.LogError($"[KSPPhysProfiler] Error in {t.Name}.{methodName} callback {targetMethod?.DeclaringType?.FullName}.{targetMethod?.Name}: {ex}");
                }
                long itemElapsed = Stopwatch.GetTimestamp() - itemStart;

                if (targetMethod != null)
                {
                    ProfilerData.RecordDispatcherSubInvocation(t, methodName, targetMethod, itemElapsed);
                }
            }

            long totalElapsed = Stopwatch.GetTimestamp() - totalStart;
            ProfilerData.RecordPluginExecution(__instance, methodName, totalElapsed);

            return false;
        }
    }
}
