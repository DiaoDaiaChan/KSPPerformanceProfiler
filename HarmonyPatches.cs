using System;
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

                HarmonyMethod prefixModuleHM = new HarmonyMethod(prefixModule);
                HarmonyMethod postfixModuleHM = new HarmonyMethod(postfixModule);

                HarmonyMethod prefixPluginHM = new HarmonyMethod(prefixPlugin);
                HarmonyMethod postfixPluginHM = new HarmonyMethod(postfixPlugin);

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

        private static void PartModule_Postfix(PartModule __instance, long __state)
        {
            if (ProfilerData.IsEnabled && __state > 0 && __instance != null)
            {
                long elapsed = Stopwatch.GetTimestamp() - __state;
                ProfilerData.RecordModuleExecution(__instance, elapsed);
            }
        }

        private static void Plugin_Prefix(out long __state)
        {
            __state = ProfilerData.IsEnabled ? Stopwatch.GetTimestamp() : 0;
        }

        private static void Plugin_Postfix(MonoBehaviour __instance, long __state)
        {
            if (ProfilerData.IsEnabled && __state > 0 && __instance != null)
            {
                long elapsed = Stopwatch.GetTimestamp() - __state;
                ProfilerData.RecordPluginExecution(__instance, elapsed);
            }
        }
    }
}
