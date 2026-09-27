using System;
using System.IO;
using UnityEngine;
using UnityEngine.Profiling;

namespace KSPPerformanceProfiler
{
    /// <summary>
    /// Built-in Mono Heap Padder & GC Stutter Eliminator (Native Padheap)
    /// Emulates and modernizes MemGraph/HeapPadder logic for Unity 2019 / KSP 1.12.
    /// Pre-allocates and expands the managed virtual heap headroom to delay Mono GC Stop-The-World pauses.
    /// </summary>
    public static class MonoHeapPadder
    {
        public static int TargetPadMb = 2048;
        public static bool AutoPadOnSceneChange = true;
        public static bool EnableHotkey = true;
        public static string LastStatusMessage = "";
        public static DateTime LastPadTime = DateTime.MinValue;

        private static bool isInitialized = false;
        private static string configFilePath = "";

        public static double CurrentHeapMb
        {
            get
            {
                try { return Profiler.GetMonoHeapSizeLong() / (1024.0 * 1024.0); }
                catch { return GC.GetTotalMemory(false) / (1024.0 * 1024.0); }
            }
        }

        public static double CurrentUsedMb
        {
            get
            {
                try { return Profiler.GetMonoUsedSizeLong() / (1024.0 * 1024.0); }
                catch { return GC.GetTotalMemory(false) / (1024.0 * 1024.0); }
            }
        }

        public static double CurrentFreeMb
        {
            get { return Math.Max(0.0, CurrentHeapMb - CurrentUsedMb); }
        }

        public static int SystemRamMb
        {
            get
            {
                try { return SystemInfo.systemMemorySize; }
                catch { return 16384; }
            }
        }

        public static int RecommendedPadMb
        {
            get
            {
                int ram = SystemRamMb;
                if (ram >= 30000) return 4096;
                if (ram >= 14000) return 2048;
                return 1024;
            }
        }

        public static bool IsPadded
        {
            get { return TargetPadMb > 0 && CurrentHeapMb >= (TargetPadMb * 0.85); }
        }

        public static void Initialize()
        {
            if (isInitialized) return;
            isInitialized = true;

            try
            {
                string dir = Path.Combine(KSPUtil.ApplicationRootPath, "GameData/KSPPerformanceProfiler/PluginData");
                if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
                configFilePath = Path.Combine(dir, "padheap.cfg");
                LoadConfig();

                TargetPadMb = Math.Max(1024, TargetPadMb == 0 ? RecommendedPadMb : TargetPadMb);

                if (AutoPadOnSceneChange && CurrentHeapMb < (TargetPadMb * 0.85))
                {
                    Pad(TargetPadMb, silent: true);
                }
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogWarning($"[KSPPerformanceProfiler] MonoHeapPadder init warning: {ex.Message}");
            }
        }

        public static void OnSceneLoaded()
        {
            if (!isInitialized) Initialize();

            if (AutoPadOnSceneChange && TargetPadMb > 0 && CurrentHeapMb < (TargetPadMb * 0.85))
            {
                Pad(TargetPadMb, silent: true);
            }
        }

        /// <summary>
        /// Expands the managed Mono virtual heap by allocating temporary memory blocks,
        /// committing physical pages, and then releasing them to Mono's internal freelist.
        /// </summary>
        public static bool Pad(int targetMegaBytes, bool silent = false)
        {
            if (targetMegaBytes <= 0) return false;

            try
            {
                // Safety: Do not pad more than 75% of total system RAM
                int maxSafeMb = (int)(SystemRamMb * 0.75);
                if (targetMegaBytes > maxSafeMb && maxSafeMb > 1024)
                {
                    targetMegaBytes = maxSafeMb;
                }

                TargetPadMb = targetMegaBytes;
                SaveConfig();

                long curHeapBytes = Profiler.GetMonoHeapSizeLong();
                long targetBytes = (long)targetMegaBytes * 1024L * 1024L;

                if (curHeapBytes >= targetBytes)
                {
                    LastStatusMessage = ProfilerI18n.Format("pad_status_success", (int)CurrentHeapMb, (int)CurrentFreeMb);
                    if (!silent)
                    {
                        ScreenMessages.PostScreenMessage(
                            $"[KSPPerformanceProfiler] {LastStatusMessage}",
                            3.5f,
                            ScreenMessageStyle.UPPER_CENTER
                        );
                    }
                    return true;
                }

                long bytesNeeded = targetBytes - curHeapBytes;
                const int CHUNK_SIZE = 16 * 1024 * 1024; // 16 MB chunks
                int chunksCount = (int)(bytesNeeded / CHUNK_SIZE) + 1;

                // Allocate blocks to expand the OS virtual heap
                byte[][] tempAlloc = new byte[chunksCount][];
                for (int i = 0; i < chunksCount; i++)
                {
                    tempAlloc[i] = new byte[CHUNK_SIZE];
                    // Touch first and last byte to commit pages into physical RAM
                    tempAlloc[i][0] = 0x5A;
                    tempAlloc[i][CHUNK_SIZE - 1] = 0xA5;
                }

                // Dereference temp arrays
                tempAlloc = null;

                // Single garbage collection to move newly committed pages into Mono's freelist
                GC.Collect();
                GC.WaitForPendingFinalizers();

                LastPadTime = DateTime.Now;
                LastStatusMessage = ProfilerI18n.Format("pad_status_success", (int)CurrentHeapMb, (int)CurrentFreeMb);

                UnityEngine.Debug.Log($"[KSPPerformanceProfiler] MonoHeapPadder: Heap successfully expanded to {CurrentHeapMb:F0} MB (Free headroom: {CurrentFreeMb:F0} MB).");

                if (!silent)
                {
                    ScreenMessages.PostScreenMessage(
                        $"[KSPPerformanceProfiler] 🚀 {LastStatusMessage}",
                        4.0f,
                        ScreenMessageStyle.UPPER_CENTER
                    );
                }

                return true;
            }
            catch (Exception ex)
            {
                LastStatusMessage = $"Pad heap error: {ex.Message}";
                UnityEngine.Debug.LogError($"[KSPPerformanceProfiler] MonoHeapPadder failed: {ex}");
                return false;
            }
        }

        public static void ForceGarbageCollection()
        {
            try
            {
                long before = GC.GetTotalMemory(false);
                GC.Collect();
                GC.WaitForPendingFinalizers();
                GC.Collect();
                long after = GC.GetTotalMemory(false);
                long freedMb = Math.Max(0, (before - after) / (1024 * 1024));

                ScreenMessages.PostScreenMessage(
                    ProfilerI18n.Format("pad_msg_gc_freed", freedMb, CurrentFreeMb),
                    3.0f,
                    ScreenMessageStyle.LOWER_CENTER
                );
            }
            catch { }
        }

        public static void LoadConfig()
        {
            if (string.IsNullOrEmpty(configFilePath) || !File.Exists(configFilePath)) return;

            try
            {
                string[] lines = File.ReadAllLines(configFilePath);
                foreach (var line in lines)
                {
                    if (string.IsNullOrEmpty(line) || line.StartsWith("#") || line.StartsWith("//")) continue;
                    var parts = line.Split('=');
                    if (parts.Length != 2) continue;

                    string key = parts[0].Trim();
                    string val = parts[1].Trim();

                    if (key.Equals("TargetPadMb", StringComparison.OrdinalIgnoreCase))
                    {
                        if (int.TryParse(val, out int mb)) TargetPadMb = mb;
                    }
                    else if (key.Equals("AutoPadOnSceneChange", StringComparison.OrdinalIgnoreCase))
                    {
                        if (bool.TryParse(val, out bool b)) AutoPadOnSceneChange = b;
                    }
                    else if (key.Equals("EnableHotkey", StringComparison.OrdinalIgnoreCase))
                    {
                        if (bool.TryParse(val, out bool b)) EnableHotkey = b;
                    }
                }
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogWarning($"[KSPPerformanceProfiler] Load padheap.cfg warning: {ex.Message}");
            }
        }

        public static void SaveConfig()
        {
            if (string.IsNullOrEmpty(configFilePath)) return;

            try
            {
                string content = $"// KSPPerformanceProfiler Native Mono Heap Padder Configuration\n" +
                                 $"TargetPadMb = {TargetPadMb}\n" +
                                 $"AutoPadOnSceneChange = {AutoPadOnSceneChange}\n" +
                                 $"EnableHotkey = {EnableHotkey}\n";
                File.WriteAllText(configFilePath, content);
            }
            catch { }
        }
    }
}
