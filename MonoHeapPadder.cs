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

        // Permanent padding blocks - kept alive to prevent Mono from shrinking the heap
        private static byte[][] padBlocks = null;
        public static int PadBlockCount => padBlocks != null ? padBlocks.Length : 0;
        public static long PadBlockTotalBytes => padBlocks != null ? (long)padBlocks.Length * CHUNK_SIZE : 0;
        private const int CHUNK_SIZE = 16 * 1024 * 1024; // 16 MB per block

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

        public static double PaddedMb
        {
            get { return PadBlockTotalBytes / (1024.0 * 1024.0); }
        }

        public static double GameUsedMb
        {
            get { return Math.Max(0.0, CurrentUsedMb - PaddedMb); }
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
                if (ram >= 7000) return 1024;
                return 512;
            }
        }

        public static int MaxSafePadMb
        {
            get
            {
                int ram = SystemRamMb;
                int max75 = (int)(ram * 0.75);
                int maxReserve4Gb = Math.Max(512, ram - 4096);
                return Math.Min(max75, maxReserve4Gb);
            }
        }

        public static bool IsPadded
        {
            get { return padBlocks != null && padBlocks.Length > 0; }
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

                if (AutoPadOnSceneChange && !IsPadded)
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

            if (AutoPadOnSceneChange && TargetPadMb > 0 && !IsPadded)
            {
                Pad(TargetPadMb, silent: true);
            }
        }

        /// <summary>
        /// Expands the managed Mono heap by allocating permanent memory blocks held in a static field.
        /// Blocks are NEVER released to GC, ensuring the heap stays expanded permanently across any garbage collection.
        /// This is the proven MemGraph/HeapPadder approach.
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

                int chunksCount = Math.Max(1, (targetMegaBytes * 1024 * 1024) / CHUNK_SIZE);

                // If already padded with this exact amount, keep it
                if (padBlocks != null && padBlocks.Length == chunksCount)
                {
                    LastStatusMessage = ProfilerI18n.Format("pad_status_success", CurrentHeapMb, PaddedMb);
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

                // If re-padding with different size, release previous blocks first
                padBlocks = null;
                GC.Collect();
                GC.WaitForPendingFinalizers();

                // Allocate blocks and KEEP them alive permanently in the static field
                padBlocks = new byte[chunksCount][];
                for (int i = 0; i < chunksCount; i++)
                {
                    padBlocks[i] = new byte[CHUNK_SIZE];
                    // Touch first and last byte to commit pages into physical RAM
                    padBlocks[i][0] = 0x5A;
                    padBlocks[i][CHUNK_SIZE - 1] = 0xA5;
                }

                LastPadTime = DateTime.Now;
                LastStatusMessage = ProfilerI18n.Format("pad_status_success", CurrentHeapMb, PaddedMb);

                UnityEngine.Debug.Log($"[KSPPerformanceProfiler] MonoHeapPadder: Heap padded with {chunksCount} x 16MB blocks ({chunksCount * 16} MB). Total heap: {CurrentHeapMb:F0} MB (Padded: {PaddedMb:F0} MB).");

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

        /// <summary>
        /// Releases all padding blocks back to GC, allowing the heap to shrink on next collection.
        /// </summary>
        public static void ReleasePadding()
        {
            int blockCount = PadBlockCount;
            long freedMb = PadBlockTotalBytes / (1024 * 1024);
            padBlocks = null;
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();

            LastStatusMessage = ProfilerI18n.Format("pad_status_released", blockCount, freedMb);
            UnityEngine.Debug.Log($"[KSPPerformanceProfiler] MonoHeapPadder: Released {blockCount} pad blocks ({freedMb} MB).");
            ScreenMessages.PostScreenMessage(
                $"[KSPPerformanceProfiler] {LastStatusMessage}",
                3.5f,
                ScreenMessageStyle.UPPER_CENTER
            );
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
