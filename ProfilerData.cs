using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using UnityEngine;

namespace KSPPerformanceProfiler
{
    public class SubInvocationStats
    {
        public string AssemblyName;
        public string TypeName;
        public string MethodName;
        public string DisplayName;

        public long CurrentFrameTicks;
        public int CurrentFrameCalls;
        public double SmoothMs;
        public double PeakMs;
        public long TotalCalls;

        public double DisplayMs;
        public double DisplayPeakMs;

        public void ResetFrame()
        {
            CurrentFrameTicks = 0;
            CurrentFrameCalls = 0;
        }

        public void Record(long ticks)
        {
            CurrentFrameTicks += ticks;
            CurrentFrameCalls++;
            TotalCalls++;
        }

        public void FinalizeFrame(double frameMs, double alpha = 0.1)
        {
            SmoothMs = (SmoothMs * (1.0 - alpha)) + (frameMs * alpha);
            if (frameMs > PeakMs)
            {
                PeakMs = frameMs;
            }

            if (DisplayMs < 0.0001) DisplayMs = SmoothMs;
            else DisplayMs = (DisplayMs * 0.85) + (SmoothMs * 0.15);

            if (DisplayPeakMs < 0.0001) DisplayPeakMs = PeakMs;
            else DisplayPeakMs = (DisplayPeakMs * 0.95) + (PeakMs * 0.05);
        }
    }

    public class MethodStats
    {
        public string MethodName;
        public long CurrentFrameTicks;
        public int CurrentFrameCalls;
        public double SmoothMs;
        public double PeakMs;
        public long TotalCalls;

        public double DisplayMs;
        public double DisplayPeakMs;

        public Dictionary<string, SubInvocationStats> SubInvocations = new Dictionary<string, SubInvocationStats>(StringComparer.Ordinal);

        public MethodStats(string name)
        {
            MethodName = name;
        }

        public void ResetFrame()
        {
            CurrentFrameTicks = 0;
            CurrentFrameCalls = 0;
            foreach (var sub in SubInvocations.Values)
            {
                sub.ResetFrame();
            }
        }

        public void Record(long ticks)
        {
            CurrentFrameTicks += ticks;
            CurrentFrameCalls++;
            TotalCalls++;
        }

        public void RecordSubInvocation(string subKey, string asmName, string typeName, string methName, long ticks)
        {
            if (!SubInvocations.TryGetValue(subKey, out var sub))
            {
                sub = new SubInvocationStats
                {
                    AssemblyName = asmName,
                    TypeName = typeName,
                    MethodName = methName,
                    DisplayName = $"[{asmName}] {typeName}.{methName}"
                };
                SubInvocations[subKey] = sub;
            }
            sub.Record(ticks);
        }

        public void FinalizeFrame(double frameMs, double alpha = 0.1)
        {
            SmoothMs = (SmoothMs * (1.0 - alpha)) + (frameMs * alpha);
            if (frameMs > PeakMs)
            {
                PeakMs = frameMs;
            }

            if (DisplayMs < 0.0001) DisplayMs = SmoothMs;
            else DisplayMs = (DisplayMs * 0.85) + (SmoothMs * 0.15);

            if (DisplayPeakMs < 0.0001) DisplayPeakMs = PeakMs;
            else DisplayPeakMs = (DisplayPeakMs * 0.95) + (PeakMs * 0.05);

            foreach (var sub in SubInvocations.Values)
            {
                double subMs = (double)sub.CurrentFrameTicks / Stopwatch.Frequency * 1000.0;
                sub.FinalizeFrame(subMs, alpha);
            }
        }

        public List<SubInvocationStats> GetSortedSubInvocations()
        {
            var list = new List<SubInvocationStats>(SubInvocations.Values);
            list.Sort((a, b) => b.SmoothMs.CompareTo(a.SmoothMs));
            return list;
        }
    }

    public class ModuleStats
    {
        public Type TargetType;
        public string TypeName;
        public string AssemblyName;
        public string FriendlyName;
        public string Category;
        public string DisplayTag;
        public bool IsTUFX;

        public long CurrentFrameTicks;
        public int CurrentFrameCalls;

        public double SmoothMs;
        public double PeakMs;
        public long TotalCalls;

        public double DisplayMs;
        public double DisplayPeakMs;

        public Dictionary<string, MethodStats> Methods = new Dictionary<string, MethodStats>(StringComparer.Ordinal);

        public ModuleStats(Type type)
        {
            TargetType = type;
            TypeName = type.Name;
            AssemblyName = type.Assembly.GetName().Name;

            DetectSubsystems(type);
        }

        private void DetectSubsystems(Type type)
        {
            if (TypeName == "PostProcessLayer" || AssemblyName == "TUFX" || HasTUFXMarker(type))
            {
                IsTUFX = true;
                Category = "Post-Processing";
                DisplayTag = "[TUFX]";
                FriendlyName = TypeName == "PostProcessLayer" ? "PostProcessLayer (TUFX)" : TypeName;
            }
            else
            {
                Category = "Plugin";
                DisplayTag = "";
                FriendlyName = TypeName;
            }
        }

        private static bool HasTUFXMarker(Type type)
        {
            try
            {
                foreach (var attr in type.GetCustomAttributes(true))
                {
                    if (attr != null && attr.GetType().Name.IndexOf("TUFXMarker", StringComparison.OrdinalIgnoreCase) >= 0)
                        return true;
                }
            }
            catch { }
            return false;
        }

        public void ResetFrame()
        {
            CurrentFrameTicks = 0;
            CurrentFrameCalls = 0;
            foreach (var m in Methods.Values)
            {
                m.ResetFrame();
            }
        }

        public void Record(long ticks)
        {
            Record("Execute", ticks);
        }

        public void Record(string methodName, long ticks)
        {
            CurrentFrameTicks += ticks;
            CurrentFrameCalls++;
            TotalCalls++;

            if (!string.IsNullOrEmpty(methodName))
            {
                if (!Methods.TryGetValue(methodName, out var mStats))
                {
                    mStats = new MethodStats(methodName);
                    Methods[methodName] = mStats;
                }
                mStats.Record(ticks);
            }
        }

        public void RecordSubInvocation(string methodName, string subKey, string asmName, string typeName, string methName, long ticks)
        {
            if (string.IsNullOrEmpty(methodName)) methodName = "FixedUpdate";
            if (!Methods.TryGetValue(methodName, out var mStats))
            {
                mStats = new MethodStats(methodName);
                Methods[methodName] = mStats;
            }
            mStats.RecordSubInvocation(subKey, asmName, typeName, methName, ticks);
        }

        public void FinalizeFrame(double frameMs, double alpha = 0.1)
        {
            SmoothMs = (SmoothMs * (1.0 - alpha)) + (frameMs * alpha);
            if (frameMs > PeakMs)
            {
                PeakMs = frameMs;
            }

            if (DisplayMs < 0.0001) DisplayMs = SmoothMs;
            else DisplayMs = (DisplayMs * 0.85) + (SmoothMs * 0.15);

            if (DisplayPeakMs < 0.0001) DisplayPeakMs = PeakMs;
            else DisplayPeakMs = (DisplayPeakMs * 0.95) + (PeakMs * 0.05);

            foreach (var m in Methods.Values)
            {
                double mMs = (double)m.CurrentFrameTicks / Stopwatch.Frequency * 1000.0;
                m.FinalizeFrame(mMs, alpha);
            }
        }

        public List<MethodStats> GetSortedMethods()
        {
            var list = new List<MethodStats>(Methods.Values);
            list.Sort((a, b) => b.SmoothMs.CompareTo(a.SmoothMs));
            return list;
        }
    }

    public class PartStats
    {
        public string PartTitle;
        public string VesselName;
        public double SmoothMs;
        public long CurrentFrameTicks;
        public double DisplayMs;

        public void ResetFrame()
        {
            CurrentFrameTicks = 0;
        }

        public void Record(long ticks)
        {
            CurrentFrameTicks += ticks;
        }

        public void FinalizeFrame(double frameMs, double alpha = 0.1)
        {
            SmoothMs = (SmoothMs * (1.0 - alpha)) + (frameMs * alpha);
            if (DisplayMs < 0.0001) DisplayMs = SmoothMs;
            else DisplayMs = (DisplayMs * 0.85) + (SmoothMs * 0.15);
        }
    }

    public class SubsystemGroupStats
    {
        public string SubsystemId;
        public string DisplayName;
        public double SmoothMs;
        public double PeakMs;
        public int ActiveTypeCount;
        public int TotalCalls;
        public double PctOfFrame;
        public List<ModuleStats> Types;
        public double DisplayMs;
        public double DisplayPeakMs;
    }

    public class AssemblyStats
    {
        public string AssemblyName;
        public double SmoothMs;
        public double PeakMs;
        public int ActiveTypeCount;
        public int TotalCalls;
        public double PctOfFrame;
        public List<NamespaceStats> Namespaces;
        public double DisplayMs;
        public double DisplayPeakMs;
    }

    public class NamespaceStats
    {
        public string Namespace;
        public double SmoothMs;
        public double PeakMs;
        public int ActiveTypeCount;
        public int TotalCalls;
        public double PctOfFrame;
        public List<ModuleStats> Types;
        public List<SubsystemGroupStats> Subsystems;
        public double DisplayMs;
        public double DisplayPeakMs;
    }

    public static class SubsystemCategorizer
    {
        public static string Categorize(Type type)
        {
            if (type == null) return "subsys_other";
            string name = type.Name;

            if (name.StartsWith("Timing", StringComparison.OrdinalIgnoreCase))
                return "subsys_timing";

            if (name == "Vessel" || name == "Part" || name.StartsWith("PartModule") ||
                name.StartsWith("FlightIntegrator") || name.StartsWith("VesselPrecalculate") ||
                name == "FlightGlobals" || name.StartsWith("VesselAuto") || name.StartsWith("VesselDeltaV") ||
                name.StartsWith("VesselValues") || name.StartsWith("StageManager") || name.StartsWith("Module"))
                return "subsys_vessel";

            if (name.StartsWith("Orbit") || name.StartsWith("PatchedConic") ||
                name == "CelestialBody" || name.StartsWith("PQS") || name.StartsWith("PSystem") ||
                name == "Planetarium" || name == "FloatingOrigin" || name == "Krakensbane")
                return "subsys_celestial";

            if (name.StartsWith("Kerbal") || name == "ProtoCrewMember" || name.StartsWith("Experience"))
                return "subsys_kerbal";

            if (name.StartsWith("Editor") || name.StartsWith("PartLoader") || name.StartsWith("ShipConstruction") || name.StartsWith("UIPartAction"))
                return "subsys_editor";

            if (name.StartsWith("FlightInput") || name.StartsWith("FlightCtrl") || name.StartsWith("VesselSAS") || name.StartsWith("VesselControl"))
                return "subsys_controls";

            if (name.StartsWith("FlightCamera") || name.StartsWith("Camera") || name == "iTween" ||
                name.StartsWith("FX") || name.EndsWith("FX") || name.StartsWith("Explosion") || name.StartsWith("Audio"))
                return "subsys_vfx";

            if (name.StartsWith("GameSettings") || name.StartsWith("InputSettings") || name == "Game" ||
                name.StartsWith("GameEvents") || name.StartsWith("GamePersistence") || name == "HighLogic" || name.StartsWith("KSPLog") || name.StartsWith("KSPUtil"))
                return "subsys_system";

            return "subsys_other";
        }
    }

    public class SpikeCulprit
    {
        public string Name;
        public string AssemblyName;
        public string Category; // "PartModule", "Plugin", "PhysX", "Mono GC", "Overhead"
        public double FrameMs;
        public double PctOfFrame;
        public string TopMethod;
    }

    public class SpikeSnapshot
    {
        public int SpikeId;
        public DateTime Timestamp;
        public double FrameMs;
        public double NormalAvgMs;
        public double SpikeRatio;
        public int GcCollections;
        public bool IsGcPause;
        public List<SpikeCulprit> Culprits = new List<SpikeCulprit>();
    }

    public class TimelineSample
    {
        public double[] TotalMs;
        public double[] ModulesMs;
        public double[] PluginsMs;
        public double[] PhysXMs;
        public double[] GpuMs;
        public double[] OverheadMs;
        public double[] Fps;
        public double[] OnePctLow;
        public bool[] IsSpike;
        public int[] SpikeId;
        public int[] GcCollections;
        public int Count;
    }

    public static class ProfilerData
    {
        public static bool IsEnabled = true;

        // Freeze and Spike Sniffer State
        public static bool IsFrozen = false;
        public static bool AutoFreezeOnSpike = false;
        public static SpikeSnapshot LatestSpike;
        public static readonly List<SpikeSnapshot> SpikeHistory = new List<SpikeSnapshot>();
        public const int MAX_SPIKE_HISTORY = 10;
        private static int nextSpikeId = 1;
        private static int lastGcCount = -1;

        // Real-time Mono GC Monitoring
        public static int GcCollectionsThisFrame { get; private set; }
        public static int GcTotalCollections { get; private set; }
        public static int GcFramesSinceLastCollect { get; private set; }
        public static bool IsGcThisFrame { get; private set; }
        public static double GcCollectionsPerSec { get; private set; }
        private static int gcFrameCounter = 0;
        private static double gcRateAccum = 0;
        private static float gcRateTimer = 0f;

        private static readonly Dictionary<Type, ModuleStats> moduleStatsMap = new Dictionary<Type, ModuleStats>();
        private static readonly Dictionary<Type, ModuleStats> pluginStatsMap = new Dictionary<Type, ModuleStats>();
        private static readonly Dictionary<uint, PartStats> partStatsMap = new Dictionary<uint, PartStats>();

        // Ring buffer for FPS & 1% / 0.1% Low analysis (last 300 frames)
        public const int BUFFER_SIZE = 300;
        private static readonly double[] frameTimeHistory = new double[BUFFER_SIZE];
        private static readonly double[] sortedHistoryBuffer = new double[BUFFER_SIZE];
        private static int bufferIndex = 0;
        private static int historyCount = 0;

        // Extended multi-layer timeline ring buffers (up to 600 frames)
        public const int TIMELINE_BUFFER_SIZE = 600;
        private static readonly double[] timelineTotalMs = new double[TIMELINE_BUFFER_SIZE];
        private static readonly double[] timelineModulesMs = new double[TIMELINE_BUFFER_SIZE];
        private static readonly double[] timelinePluginsMs = new double[TIMELINE_BUFFER_SIZE];
        private static readonly double[] timelinePhysXMs = new double[TIMELINE_BUFFER_SIZE];
        private static readonly double[] timelineGpuMs = new double[TIMELINE_BUFFER_SIZE];
        private static readonly double[] timelineOverheadMs = new double[TIMELINE_BUFFER_SIZE];
        private static readonly double[] timelineFps = new double[TIMELINE_BUFFER_SIZE];
        private static readonly double[] timelineOnePctLow = new double[TIMELINE_BUFFER_SIZE];
        private static readonly bool[] timelineIsSpike = new bool[TIMELINE_BUFFER_SIZE];
        private static readonly int[] timelineSpikeId = new int[TIMELINE_BUFFER_SIZE];
        private static readonly int[] timelineGcCollections = new int[TIMELINE_BUFFER_SIZE];
        private static int timelineIndex = 0;
        private static int timelineCount = 0;

        public static void ClearSpikeHistory()
        {
            SpikeHistory.Clear();
            LatestSpike = null;
        }

        public static void ClearPartStats()
        {
            partStatsMap.Clear();
        }

        public static void ToggleFreeze()
        {
            IsFrozen = !IsFrozen;
        }

        // Macro Timings (Instantaneous & Smoothed)
        public static double TotalFrameMs { get; private set; }
        public static double SmoothTotalFrameMs { get; private set; }

        public static double PhysicsStepMs { get; private set; }
        public static double SmoothPhysicsStepMs { get; private set; }

        public static double TotalModuleScriptMs { get; private set; }
        public static double SmoothModuleScriptMs { get; private set; }

        public static double TotalPluginScriptMs { get; private set; }
        public static double SmoothPluginScriptMs { get; private set; }

        public static double PhysXJointsMs { get; private set; }
        public static double SmoothPhysXJointsMs { get; private set; }

        public static double RenderAndGpuMs { get; private set; }
        public static double SmoothRenderAndGpuMs { get; private set; }

        // FPS & Lows Metrics
        public static double CurrentFPS { get; private set; }
        public static double SmoothFPS { get; private set; }
        public static double AvgFPS { get; private set; }
        public static double OnePercentLowFPS { get; private set; }
        public static double PointOnePercentLowFPS { get; private set; }
        public static double FrameJitterMs { get; private set; }
        public static double CurrentPTR { get; private set; }
        public static double SmoothPTR { get; private set; }

        private static long physFrameStartTimestamp;
        private static long physFrameElapsedTicks;

        private static long renderStartTimestamp;
        public static double CameraRenderMs { get; private set; }

        public static void BeginPhysicsFrame()
        {
            if (!IsEnabled) return;
            physFrameStartTimestamp = Stopwatch.GetTimestamp();
        }

        public static void EndPhysicsFrame()
        {
            if (!IsEnabled) return;
            physFrameElapsedTicks = Stopwatch.GetTimestamp() - physFrameStartTimestamp;
        }

        public static void OnCameraPreRender()
        {
            if (!IsEnabled) return;
            renderStartTimestamp = Stopwatch.GetTimestamp();
        }

        public static void OnCameraPostRender()
        {
            if (!IsEnabled) return;
            long elapsed = Stopwatch.GetTimestamp() - renderStartTimestamp;
            double ms = (double)elapsed / Stopwatch.Frequency * 1000.0;
            CameraRenderMs = (CameraRenderMs * 0.9) + (ms * 0.1);
        }

        // TUFX Telemetry integration
        public static bool IsTUFXDetected { get; private set; }
        public static double TUFXPostProcessMs { get; private set; }
        public static double TUFXCpuBuildMs { get; private set; }
        public static int TUFXActivePassCount { get; private set; }
        public static string TUFXActivePassSummary { get; private set; } = "";

        private static Type tufxProfilerType;
        private static System.Reflection.PropertyInfo tufxAvgMsProp;
        private static System.Reflection.PropertyInfo tufxCpuMsProp;
        private static System.Reflection.PropertyInfo tufxPassCountProp;
        private static System.Reflection.MethodInfo tufxSummaryMethod;
        private static bool tufxQueried = false;

        public static void QueryTUFXTelemetry()
        {
            if (!tufxQueried)
            {
                tufxQueried = true;
                try
                {
                    foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
                    {
                        if (asm.GetName().Name == "TUFX")
                        {
                            tufxProfilerType = asm.GetType("TUFX.Performance.TUFXProfiler");
                            if (tufxProfilerType != null)
                            {
                                tufxAvgMsProp = tufxProfilerType.GetProperty("AvgTotalPostProcessTimeMs", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
                                tufxCpuMsProp = tufxProfilerType.GetProperty("SmoothCpuBuildTimeMs", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
                                tufxPassCountProp = tufxProfilerType.GetProperty("ActiveEffectsCount", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
                                tufxSummaryMethod = tufxProfilerType.GetMethod("GetActivePassesSummary", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
                                IsTUFXDetected = true;
                            }
                            break;
                        }
                    }
                }
                catch { }
            }

            if (IsTUFXDetected && tufxProfilerType != null)
            {
                try
                {
                    if (tufxAvgMsProp != null)
                        TUFXPostProcessMs = Convert.ToDouble(tufxAvgMsProp.GetValue(null, null));
                    if (tufxCpuMsProp != null)
                        TUFXCpuBuildMs = Convert.ToDouble(tufxCpuMsProp.GetValue(null, null));
                    if (tufxPassCountProp != null)
                        TUFXActivePassCount = Convert.ToInt32(tufxPassCountProp.GetValue(null, null));
                    if (tufxSummaryMethod != null)
                        TUFXActivePassSummary = (string)tufxSummaryMethod.Invoke(null, null) ?? "";
                }
                catch { }
            }
        }

        public static void UpdateFrameMetrics()
        {
            if (!IsEnabled) return;
            if (IsFrozen) return;

            QueryTUFXTelemetry();

            double unscaledDelta = Time.unscaledDeltaTime;
            TotalFrameMs = unscaledDelta * 1000.0;

            // Record to ring buffer
            frameTimeHistory[bufferIndex] = TotalFrameMs;
            bufferIndex = (bufferIndex + 1) % BUFFER_SIZE;
            if (historyCount < BUFFER_SIZE) historyCount++;

            // Calculate FPS & Lows
            CalculateFpsStatistics(unscaledDelta);

            // Calculate PTR
            double fixedDeltaSec = Time.fixedDeltaTime * Time.timeScale;
            CurrentPTR = unscaledDelta > 0.0001 ? Math.Min(1.0, fixedDeltaSec / unscaledDelta) : 1.0;

            // Detect Mono GC collections
            int gcNow = GC.CollectionCount(0);
            int gcDelta = (lastGcCount >= 0) ? Math.Max(0, gcNow - lastGcCount) : 0;
            lastGcCount = gcNow;

            // Real-time GC monitoring
            GcCollectionsThisFrame = gcDelta;
            IsGcThisFrame = gcDelta > 0;
            GcTotalCollections += gcDelta;
            if (gcDelta > 0)
            {
                gcFrameCounter = 0;
            }
            else
            {
                gcFrameCounter++;
            }
            GcFramesSinceLastCollect = gcFrameCounter;

            // GC rate per second (computed every second)
            gcRateAccum += gcDelta;
            gcRateTimer += Time.unscaledDeltaTime;
            if (gcRateTimer >= 1.0f)
            {
                GcCollectionsPerSec = gcRateAccum / gcRateTimer;
                gcRateAccum = 0;
                gcRateTimer = 0f;
            }

            // Spike / Micro-Stutter Detection (>2x average and >35ms, or >66.6ms)
            bool isSpike = historyCount >= 10 && ((TotalFrameMs > SmoothTotalFrameMs * 2.0 && TotalFrameMs > 35.0) || TotalFrameMs > 66.6);
            SpikeSnapshot spikeSnapshot = null;

            if (isSpike)
            {
                spikeSnapshot = new SpikeSnapshot
                {
                    SpikeId = nextSpikeId++,
                    Timestamp = DateTime.Now,
                    FrameMs = TotalFrameMs,
                    NormalAvgMs = SmoothTotalFrameMs > 0.001 ? SmoothTotalFrameMs : 16.6,
                    SpikeRatio = SmoothTotalFrameMs > 0.001 ? (TotalFrameMs / SmoothTotalFrameMs) : (TotalFrameMs / 16.6),
                    GcCollections = gcDelta,
                    IsGcPause = gcDelta > 0
                };

                var culprits = new List<SpikeCulprit>();

                foreach (var m in moduleStatsMap.Values)
                {
                    if (m.CurrentFrameTicks <= 0) continue;
                    double ms = (double)m.CurrentFrameTicks / Stopwatch.Frequency * 1000.0;
                    if (ms < 0.2) continue;

                    string topMeth = "";
                    long maxT = 0;
                    foreach (var meth in m.Methods.Values)
                    {
                        if (meth.CurrentFrameTicks > maxT)
                        {
                            maxT = meth.CurrentFrameTicks;
                            topMeth = meth.MethodName;
                        }
                    }

                    culprits.Add(new SpikeCulprit
                    {
                        Name = m.TypeName,
                        AssemblyName = m.AssemblyName,
                        Category = "PartModule",
                        FrameMs = ms,
                        PctOfFrame = (ms / TotalFrameMs) * 100.0,
                        TopMethod = topMeth
                    });
                }

                foreach (var p in pluginStatsMap.Values)
                {
                    if (p.CurrentFrameTicks <= 0) continue;
                    double ms = (double)p.CurrentFrameTicks / Stopwatch.Frequency * 1000.0;
                    if (ms < 0.2) continue;

                    string topMeth = "";
                    long maxT = 0;
                    foreach (var meth in p.Methods.Values)
                    {
                        if (meth.CurrentFrameTicks > maxT)
                        {
                            maxT = meth.CurrentFrameTicks;
                            topMeth = meth.MethodName;
                        }
                    }

                    culprits.Add(new SpikeCulprit
                    {
                        Name = p.TypeName,
                        AssemblyName = p.AssemblyName,
                        Category = "Plugin",
                        FrameMs = ms,
                        PctOfFrame = (ms / TotalFrameMs) * 100.0,
                        TopMethod = topMeth
                    });
                }

                if (spikeSnapshot.IsGcPause)
                {
                    culprits.Add(new SpikeCulprit
                    {
                        Name = ProfilerI18n.Get("culprit_mono_gc"),
                        AssemblyName = "mscorlib (Mono Runtime)",
                        Category = "Mono GC",
                        FrameMs = Math.Max(4.0, TotalFrameMs * 0.25),
                        PctOfFrame = Math.Min(50.0, (Math.Max(4.0, TotalFrameMs * 0.25) / TotalFrameMs) * 100.0),
                        TopMethod = "GC.Collect(0)"
                    });
                }

                culprits.Sort((a, b) => b.FrameMs.CompareTo(a.FrameMs));
                spikeSnapshot.Culprits = culprits.Take(6).ToList();

                SpikeHistory.Add(spikeSnapshot);
                if (SpikeHistory.Count > MAX_SPIKE_HISTORY)
                {
                    SpikeHistory.RemoveAt(0);
                }
                LatestSpike = spikeSnapshot;

                if (AutoFreezeOnSpike)
                {
                    IsFrozen = true;
                }
            }

            // Aggregate Scripts
            double sumModuleMs = 0.0;
            foreach (var kvp in moduleStatsMap.Values)
            {
                double ms = (double)kvp.CurrentFrameTicks / Stopwatch.Frequency * 1000.0;
                kvp.FinalizeFrame(ms);
                sumModuleMs += ms;
                kvp.ResetFrame();
            }

            double sumPluginMs = 0.0;
            foreach (var kvp in pluginStatsMap.Values)
            {
                double ms = (double)kvp.CurrentFrameTicks / Stopwatch.Frequency * 1000.0;
                kvp.FinalizeFrame(ms);
                sumPluginMs += ms;
                kvp.ResetFrame();
            }

            foreach (var kvp in partStatsMap.Values)
            {
                double ms = (double)kvp.CurrentFrameTicks / Stopwatch.Frequency * 1000.0;
                kvp.FinalizeFrame(ms);
                kvp.ResetFrame();
            }

            TotalModuleScriptMs = sumModuleMs;
            TotalPluginScriptMs = sumPluginMs;

            PhysicsStepMs = (double)physFrameElapsedTicks / Stopwatch.Frequency * 1000.0;
            PhysXJointsMs = Math.Max(0.0, PhysicsStepMs - TotalModuleScriptMs);

            double totalScripts = TotalModuleScriptMs + TotalPluginScriptMs;
            RenderAndGpuMs = Math.Max(0.0, TotalFrameMs - totalScripts - PhysXJointsMs);

            if (isSpike && spikeSnapshot != null && PhysXJointsMs > 12.0)
            {
                spikeSnapshot.Culprits.Add(new SpikeCulprit
                {
                    Name = ProfilerI18n.Get("culprit_physx_joints"),
                    AssemblyName = "UnityEngine.PhysicsModule",
                    Category = "PhysX",
                    FrameMs = PhysXJointsMs,
                    PctOfFrame = (PhysXJointsMs / TotalFrameMs) * 100.0,
                    TopMethod = "Physics.Simulate"
                });
                spikeSnapshot.Culprits.Sort((a, b) => b.FrameMs.CompareTo(a.FrameMs));
            }

            // Update Smoothed Exponential Moving Averages (EMA) for flicker-free display
            const double alpha = 0.15;
            if (SmoothTotalFrameMs < 0.0001)
            {
                SmoothTotalFrameMs = TotalFrameMs;
                SmoothFPS = CurrentFPS;
                SmoothPTR = CurrentPTR;
                SmoothPhysicsStepMs = PhysicsStepMs;
                SmoothModuleScriptMs = TotalModuleScriptMs;
                SmoothPluginScriptMs = TotalPluginScriptMs;
                SmoothPhysXJointsMs = PhysXJointsMs;
                SmoothRenderAndGpuMs = RenderAndGpuMs;
            }
            else
            {
                SmoothTotalFrameMs = (SmoothTotalFrameMs * (1.0 - alpha)) + (TotalFrameMs * alpha);
                SmoothFPS = (SmoothFPS * (1.0 - alpha)) + (CurrentFPS * alpha);
                SmoothPTR = (SmoothPTR * (1.0 - alpha)) + (CurrentPTR * alpha);
                SmoothPhysicsStepMs = (SmoothPhysicsStepMs * (1.0 - alpha)) + (PhysicsStepMs * alpha);
                SmoothModuleScriptMs = (SmoothModuleScriptMs * (1.0 - alpha)) + (TotalModuleScriptMs * alpha);
                SmoothPluginScriptMs = (SmoothPluginScriptMs * (1.0 - alpha)) + (TotalPluginScriptMs * alpha);
                SmoothPhysXJointsMs = (SmoothPhysXJointsMs * (1.0 - alpha)) + (PhysXJointsMs * alpha);
                SmoothRenderAndGpuMs = (SmoothRenderAndGpuMs * (1.0 - alpha)) + (RenderAndGpuMs * alpha);
            }

            // Record to timeline multi-layer ring buffers
            double overhead = Math.Max(0.0, TotalFrameMs - TotalModuleScriptMs - TotalPluginScriptMs - PhysXJointsMs - RenderAndGpuMs);
            timelineTotalMs[timelineIndex] = TotalFrameMs;
            timelineModulesMs[timelineIndex] = TotalModuleScriptMs;
            timelinePluginsMs[timelineIndex] = TotalPluginScriptMs;
            timelinePhysXMs[timelineIndex] = PhysXJointsMs;
            timelineGpuMs[timelineIndex] = RenderAndGpuMs;
            timelineOverheadMs[timelineIndex] = overhead;
            timelineFps[timelineIndex] = CurrentFPS;
            timelineOnePctLow[timelineIndex] = OnePercentLowFPS;
            timelineIsSpike[timelineIndex] = isSpike;
            timelineSpikeId[timelineIndex] = spikeSnapshot != null ? spikeSnapshot.SpikeId : 0;
            timelineGcCollections[timelineIndex] = gcDelta;

            timelineIndex = (timelineIndex + 1) % TIMELINE_BUFFER_SIZE;
            if (timelineCount < TIMELINE_BUFFER_SIZE) timelineCount++;
        }

        private static void CalculateFpsStatistics(double unscaledDelta)
        {
            CurrentFPS = unscaledDelta > 0.0001 ? 1.0 / unscaledDelta : 0.0;

            if (historyCount < 10)
            {
                AvgFPS = CurrentFPS;
                OnePercentLowFPS = CurrentFPS;
                PointOnePercentLowFPS = CurrentFPS;
                FrameJitterMs = 0.0;
                return;
            }

            // Copy and sort frame times (zero heap allocation via static reusable buffer)
            Array.Copy(frameTimeHistory, sortedHistoryBuffer, historyCount);
            Array.Sort(sortedHistoryBuffer, 0, historyCount);

            double sumMs = 0;
            for (int i = 0; i < historyCount; i++) sumMs += sortedHistoryBuffer[i];
            double avgMs = sumMs / historyCount;
            AvgFPS = avgMs > 0.0001 ? 1000.0 / avgMs : 0.0;

            // 1% Low (99th percentile slowest frame)
            int idx1Pct = (int)Math.Floor(historyCount * 0.99);
            idx1Pct = Math.Min(historyCount - 1, Math.Max(0, idx1Pct));
            double ms1Pct = sortedHistoryBuffer[idx1Pct];
            OnePercentLowFPS = ms1Pct > 0.0001 ? 1000.0 / ms1Pct : 0.0;

            // 0.1% Low (99.9th percentile slowest frame)
            int idx01Pct = (int)Math.Floor(historyCount * 0.999);
            idx01Pct = Math.Min(historyCount - 1, Math.Max(0, idx01Pct));
            double ms01Pct = sortedHistoryBuffer[idx01Pct];
            PointOnePercentLowFPS = ms01Pct > 0.0001 ? 1000.0 / ms01Pct : 0.0;

            // Jitter / Variance calculation
            double varianceSum = 0;
            for (int i = 0; i < historyCount; i++)
            {
                double diff = sortedHistoryBuffer[i] - avgMs;
                varianceSum += diff * diff;
            }
            FrameJitterMs = Math.Sqrt(varianceSum / historyCount);
        }

        private static readonly double[] cachedFrameHistoryBuffer = new double[BUFFER_SIZE];
        private static readonly TimelineSample cachedTimelineSample = new TimelineSample
        {
            TotalMs = new double[TIMELINE_BUFFER_SIZE],
            ModulesMs = new double[TIMELINE_BUFFER_SIZE],
            PluginsMs = new double[TIMELINE_BUFFER_SIZE],
            PhysXMs = new double[TIMELINE_BUFFER_SIZE],
            GpuMs = new double[TIMELINE_BUFFER_SIZE],
            OverheadMs = new double[TIMELINE_BUFFER_SIZE],
            Fps = new double[TIMELINE_BUFFER_SIZE],
            OnePctLow = new double[TIMELINE_BUFFER_SIZE],
            IsSpike = new bool[TIMELINE_BUFFER_SIZE],
            SpikeId = new int[TIMELINE_BUFFER_SIZE],
            GcCollections = new int[TIMELINE_BUFFER_SIZE],
            Count = 0
        };

        public static int GetFrameHistorySample(double[] outBuffer, int count = 100)
        {
            if (outBuffer == null) return 0;
            int n = Math.Min(count, Math.Min(historyCount, outBuffer.Length));
            int startIdx = (bufferIndex - n + BUFFER_SIZE) % BUFFER_SIZE;
            for (int i = 0; i < n; i++)
            {
                outBuffer[i] = frameTimeHistory[(startIdx + i) % BUFFER_SIZE];
            }
            return n;
        }

        public static double[] GetFrameHistorySample(int count = 100)
        {
            int n = Math.Min(count, historyCount);
            double[] sample = new double[n];
            int startIdx = (bufferIndex - n + BUFFER_SIZE) % BUFFER_SIZE;
            for (int i = 0; i < n; i++)
            {
                sample[i] = frameTimeHistory[(startIdx + i) % BUFFER_SIZE];
            }
            return sample;
        }

        public static TimelineSample GetTimelineSample(int count = 100)
        {
            int n = Math.Min(count, timelineCount);
            cachedTimelineSample.Count = n;
            if (n == 0) return cachedTimelineSample;

            int startIdx = (timelineIndex - n + TIMELINE_BUFFER_SIZE) % TIMELINE_BUFFER_SIZE;
            for (int i = 0; i < n; i++)
            {
                int idx = (startIdx + i) % TIMELINE_BUFFER_SIZE;
                cachedTimelineSample.TotalMs[i] = timelineTotalMs[idx];
                cachedTimelineSample.ModulesMs[i] = timelineModulesMs[idx];
                cachedTimelineSample.PluginsMs[i] = timelinePluginsMs[idx];
                cachedTimelineSample.PhysXMs[i] = timelinePhysXMs[idx];
                cachedTimelineSample.GpuMs[i] = timelineGpuMs[idx];
                cachedTimelineSample.OverheadMs[i] = timelineOverheadMs[idx];
                cachedTimelineSample.Fps[i] = timelineFps[idx];
                cachedTimelineSample.OnePctLow[i] = timelineOnePctLow[idx];
                cachedTimelineSample.IsSpike[i] = timelineIsSpike[idx];
                cachedTimelineSample.SpikeId[i] = timelineSpikeId[idx];
                cachedTimelineSample.GcCollections[i] = timelineGcCollections[idx];
            }
            return cachedTimelineSample;
        }

        public static void RecordModuleExecution(PartModule module, long elapsedTicks)
        {
            RecordModuleExecution(module, "Execute", elapsedTicks);
        }

        public static void RecordModuleExecution(PartModule module, string methodName, long elapsedTicks)
        {
            if (!IsEnabled || module == null) return;
            Type mType = module.GetType();
            if (!moduleStatsMap.TryGetValue(mType, out ModuleStats mStats))
            {
                mStats = new ModuleStats(mType);
                moduleStatsMap[mType] = mStats;
            }
            mStats.Record(methodName, elapsedTicks);

            if (module.part != null)
            {
                uint partId = module.part.flightID;
                if (partId == 0) partId = (uint)module.part.GetInstanceID();

                if (!partStatsMap.TryGetValue(partId, out PartStats pStats))
                {
                    pStats = new PartStats
                    {
                        PartTitle = module.part.partInfo != null ? module.part.partInfo.title : module.part.name,
                        VesselName = module.vessel != null ? module.vessel.vesselName : "Unknown Vessel"
                    };
                    partStatsMap[partId] = pStats;
                }
                pStats.Record(elapsedTicks);
            }
        }

        public static void RecordPluginExecution(MonoBehaviour plugin, long elapsedTicks)
        {
            RecordPluginExecution(plugin, "Execute", elapsedTicks);
        }

        public static void RecordPluginExecution(MonoBehaviour plugin, string methodName, long elapsedTicks)
        {
            if (!IsEnabled || plugin == null) return;
            Type pType = plugin.GetType();
            if (!pluginStatsMap.TryGetValue(pType, out ModuleStats pStats))
            {
                pStats = new ModuleStats(pType);
                pluginStatsMap[pType] = pStats;
            }
            pStats.Record(methodName, elapsedTicks);
        }

        private class MethodMetaCache
        {
            public string AsmName;
            public string TypeName;
            public string MethName;
            public string SubKey;
        }

        private static readonly Dictionary<System.Reflection.MethodInfo, MethodMetaCache> methodMetaCache = new Dictionary<System.Reflection.MethodInfo, MethodMetaCache>();

        public static void RecordDispatcherSubInvocation(Type dispatcherType, string methodName, System.Reflection.MethodInfo targetMethod, long elapsedTicks)
        {
            if (!IsEnabled || dispatcherType == null || targetMethod == null) return;
            if (!pluginStatsMap.TryGetValue(dispatcherType, out ModuleStats pStats))
            {
                pStats = new ModuleStats(dispatcherType);
                pluginStatsMap[dispatcherType] = pStats;
            }

            if (!methodMetaCache.TryGetValue(targetMethod, out MethodMetaCache meta))
            {
                string asm = targetMethod.DeclaringType != null ? targetMethod.DeclaringType.Assembly.GetName().Name : "Unknown";
                string type = targetMethod.DeclaringType != null ? targetMethod.DeclaringType.Name : "Global";
                string meth = targetMethod.Name;
                meta = new MethodMetaCache
                {
                    AsmName = asm,
                    TypeName = type,
                    MethName = meth,
                    SubKey = $"{asm}::{type}.{meth}"
                };
                methodMetaCache[targetMethod] = meta;
            }

            pStats.RecordSubInvocation(methodName, meta.SubKey, meta.AsmName, meta.TypeName, meta.MethName, elapsedTicks);
        }

        public static List<ModuleStats> GetTopModules(int limit = 50, string filter = null, int sortColumn = 1, bool sortAsc = false)
        {
            var list = new List<ModuleStats>(moduleStatsMap.Count);
            bool hasFilter = !string.IsNullOrEmpty(filter);

            foreach (var m in moduleStatsMap.Values)
            {
                if (hasFilter)
                {
                    if ((m.TypeName != null && m.TypeName.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0) ||
                        (m.AssemblyName != null && m.AssemblyName.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0))
                    {
                        list.Add(m);
                    }
                }
                else
                {
                    if (m.SmoothMs > 0.001 || m.CurrentFrameCalls > 0)
                    {
                        list.Add(m);
                    }
                }
            }

            Comparison<ModuleStats> comp;
            switch (sortColumn)
            {
                case 0: comp = (a, b) => sortAsc ? string.Compare(a.TypeName, b.TypeName, StringComparison.OrdinalIgnoreCase) : string.Compare(b.TypeName, a.TypeName, StringComparison.OrdinalIgnoreCase); break;
                case 1: comp = (a, b) => sortAsc ? a.SmoothMs.CompareTo(b.SmoothMs) : b.SmoothMs.CompareTo(a.SmoothMs); break;
                case 2: comp = (a, b) => sortAsc ? a.PeakMs.CompareTo(b.PeakMs) : b.PeakMs.CompareTo(a.PeakMs); break;
                case 3: comp = (a, b) => sortAsc ? a.CurrentFrameCalls.CompareTo(b.CurrentFrameCalls) : b.CurrentFrameCalls.CompareTo(a.CurrentFrameCalls); break;
                case 4: comp = (a, b) => sortAsc ? string.Compare(a.AssemblyName, b.AssemblyName, StringComparison.OrdinalIgnoreCase) : string.Compare(b.AssemblyName, a.AssemblyName, StringComparison.OrdinalIgnoreCase); break;
                default: comp = (a, b) => b.SmoothMs.CompareTo(a.SmoothMs); break;
            }

            list.Sort(comp);
            if (list.Count > limit)
            {
                list.RemoveRange(limit, list.Count - limit);
            }
            return list;
        }

        public static List<ModuleStats> GetTopPlugins(int limit = 50, string filter = null, int sortColumn = 1, bool sortAsc = false)
        {
            var list = new List<ModuleStats>(pluginStatsMap.Count);
            bool hasFilter = !string.IsNullOrEmpty(filter);

            foreach (var p in pluginStatsMap.Values)
            {
                if (hasFilter)
                {
                    if ((p.TypeName != null && p.TypeName.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0) ||
                        (p.AssemblyName != null && p.AssemblyName.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0))
                    {
                        list.Add(p);
                    }
                }
                else
                {
                    if (p.SmoothMs > 0.001 || p.CurrentFrameCalls > 0)
                    {
                        list.Add(p);
                    }
                }
            }

            Comparison<ModuleStats> comp;
            switch (sortColumn)
            {
                case 0: comp = (a, b) => sortAsc ? string.Compare(a.TypeName, b.TypeName, StringComparison.OrdinalIgnoreCase) : string.Compare(b.TypeName, a.TypeName, StringComparison.OrdinalIgnoreCase); break;
                case 1: comp = (a, b) => sortAsc ? a.SmoothMs.CompareTo(b.SmoothMs) : b.SmoothMs.CompareTo(a.SmoothMs); break;
                case 2: comp = (a, b) => sortAsc ? a.PeakMs.CompareTo(b.PeakMs) : b.PeakMs.CompareTo(a.PeakMs); break;
                case 3: comp = (a, b) => sortAsc ? a.CurrentFrameCalls.CompareTo(b.CurrentFrameCalls) : b.CurrentFrameCalls.CompareTo(a.CurrentFrameCalls); break;
                case 4: comp = (a, b) => sortAsc ? string.Compare(a.AssemblyName, b.AssemblyName, StringComparison.OrdinalIgnoreCase) : string.Compare(b.AssemblyName, a.AssemblyName, StringComparison.OrdinalIgnoreCase); break;
                default: comp = (a, b) => b.SmoothMs.CompareTo(a.SmoothMs); break;
            }

            list.Sort(comp);
            if (list.Count > limit)
            {
                list.RemoveRange(limit, list.Count - limit);
            }
            return list;
        }

        public static List<PartStats> GetTopParts(int limit = 40, string filter = null, int sortColumn = 2, bool sortAsc = false)
        {
            var list = new List<PartStats>(partStatsMap.Count);
            bool hasFilter = !string.IsNullOrEmpty(filter);

            foreach (var p in partStatsMap.Values)
            {
                if (hasFilter)
                {
                    if ((p.PartTitle != null && p.PartTitle.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0) ||
                        (p.VesselName != null && p.VesselName.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0))
                    {
                        list.Add(p);
                    }
                }
                else
                {
                    if (p.SmoothMs > 0.005)
                    {
                        list.Add(p);
                    }
                }
            }

            Comparison<PartStats> comp;
            switch (sortColumn)
            {
                case 0: comp = (a, b) => sortAsc ? string.Compare(a.PartTitle, b.PartTitle, StringComparison.OrdinalIgnoreCase) : string.Compare(b.PartTitle, a.PartTitle, StringComparison.OrdinalIgnoreCase); break;
                case 1: comp = (a, b) => sortAsc ? string.Compare(a.VesselName, b.VesselName, StringComparison.OrdinalIgnoreCase) : string.Compare(b.VesselName, a.VesselName, StringComparison.OrdinalIgnoreCase); break;
                case 2: comp = (a, b) => sortAsc ? a.SmoothMs.CompareTo(b.SmoothMs) : b.SmoothMs.CompareTo(a.SmoothMs); break;
                default: comp = (a, b) => b.SmoothMs.CompareTo(a.SmoothMs); break;
            }

            list.Sort(comp);
            if (list.Count > limit)
            {
                list.RemoveRange(limit, list.Count - limit);
            }
            return list;
        }

        public static void ResetAllPeakData()
        {
            foreach (var m in moduleStatsMap.Values) m.PeakMs = 0;
            foreach (var p in pluginStatsMap.Values) p.PeakMs = 0;
        }

        public static List<AssemblyStats> GetAssemblyBreakdown(string filter = null, int sortColumn = 1, bool sortAsc = false)
        {
            double totalFrameMs = Math.Max(0.001, SmoothTotalFrameMs);

            // Merge module + plugin stats into one stream
            var allStats = new List<ModuleStats>(moduleStatsMap.Count + pluginStatsMap.Count);
            allStats.AddRange(moduleStatsMap.Values);
            allStats.AddRange(pluginStatsMap.Values);

            // Group by assembly
            var asmGroups = new Dictionary<string, List<ModuleStats>>(StringComparer.OrdinalIgnoreCase);
            foreach (var s in allStats)
            {
                string asmName = s.AssemblyName ?? "<unknown>";
                if (!asmGroups.TryGetValue(asmName, out var list))
                {
                    list = new List<ModuleStats>();
                    asmGroups[asmName] = list;
                }
                list.Add(s);
            }

            var result = new List<AssemblyStats>(asmGroups.Count);
            foreach (var kvp in asmGroups)
            {
                string asmName = kvp.Key;

                // Apply filter
                if (!string.IsNullOrEmpty(filter))
                {
                    bool matchAsm = asmName.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0;
                    if (!matchAsm)
                    {
                        // Check if any type/namespace in this assembly matches
                        bool anyMatch = false;
                        foreach (var s in kvp.Value)
                        {
                            if ((s.TypeName != null && s.TypeName.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0) ||
                                (s.TargetType != null && s.TargetType.Namespace != null && s.TargetType.Namespace.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0))
                            {
                                anyMatch = true;
                                break;
                            }
                        }
                        if (!anyMatch) continue;
                    }
                }

                double asmSmoothMs = 0;
                double asmPeakMs = 0;
                int asmActiveTypes = 0;
                int asmCalls = 0;

                // Group by namespace within this assembly
                var nsGroups = new Dictionary<string, List<ModuleStats>>(StringComparer.Ordinal);
                foreach (var s in kvp.Value)
                {
                    string ns = (s.TargetType != null && !string.IsNullOrEmpty(s.TargetType.Namespace))
                        ? s.TargetType.Namespace
                        : "<global>";
                    if (!nsGroups.TryGetValue(ns, out var nsList))
                    {
                        nsList = new List<ModuleStats>();
                        nsGroups[ns] = nsList;
                    }
                    nsList.Add(s);

                    asmSmoothMs += s.SmoothMs;
                    if (s.PeakMs > asmPeakMs) asmPeakMs = s.PeakMs;
                    if (s.SmoothMs > 0.001 || s.CurrentFrameCalls > 0) asmActiveTypes++;
                    asmCalls += s.CurrentFrameCalls;
                }

                // Skip assemblies with no measurable activity (unless filtering)
                if (string.IsNullOrEmpty(filter) && asmSmoothMs < 0.001 && asmCalls == 0)
                    continue;

                // Build namespace stats
                var nsList2 = new List<NamespaceStats>(nsGroups.Count);
                foreach (var nsKvp in nsGroups)
                {
                    double nsSmoothMs = 0;
                    double nsPeakMs = 0;
                    int nsActive = 0;
                    int nsCalls = 0;
                    foreach (var s in nsKvp.Value)
                    {
                        nsSmoothMs += s.SmoothMs;
                        if (s.PeakMs > nsPeakMs) nsPeakMs = s.PeakMs;
                        if (s.SmoothMs > 0.001 || s.CurrentFrameCalls > 0) nsActive++;
                        nsCalls += s.CurrentFrameCalls;
                    }

                    // Sort types within namespace by SmoothMs desc
                    nsKvp.Value.Sort((a, b) => b.SmoothMs.CompareTo(a.SmoothMs));

                    List<SubsystemGroupStats> subStatsList = null;
                    if (nsKvp.Key == "<global>")
                    {
                        var subGroups = new Dictionary<string, List<ModuleStats>>(StringComparer.Ordinal);
                        foreach (var s in nsKvp.Value)
                        {
                            string subCat = SubsystemCategorizer.Categorize(s.TargetType);
                            if (!subGroups.TryGetValue(subCat, out var subList))
                            {
                                subList = new List<ModuleStats>();
                                subGroups[subCat] = subList;
                            }
                            subList.Add(s);
                        }

                        subStatsList = new List<SubsystemGroupStats>(subGroups.Count);
                        foreach (var subKvp in subGroups)
                        {
                            double subSmooth = 0;
                            double subPeak = 0;
                            int subActive = 0;
                            int subCalls = 0;
                            foreach (var s in subKvp.Value)
                            {
                                subSmooth += s.SmoothMs;
                                if (s.PeakMs > subPeak) subPeak = s.PeakMs;
                                if (s.SmoothMs > 0.001 || s.CurrentFrameCalls > 0) subActive++;
                                subCalls += s.CurrentFrameCalls;
                            }

                            subKvp.Value.Sort((a, b) => b.SmoothMs.CompareTo(a.SmoothMs));

                            subStatsList.Add(new SubsystemGroupStats
                            {
                                SubsystemId = subKvp.Key,
                                DisplayName = ProfilerI18n.Get(subKvp.Key),
                                SmoothMs = subSmooth,
                                PeakMs = subPeak,
                                DisplayMs = subSmooth,
                                DisplayPeakMs = subPeak,
                                ActiveTypeCount = subActive,
                                TotalCalls = subCalls,
                                PctOfFrame = (subSmooth / totalFrameMs) * 100.0,
                                Types = subKvp.Value
                            });
                        }
                        subStatsList.Sort((a, b) => b.SmoothMs.CompareTo(a.SmoothMs));
                    }

                    nsList2.Add(new NamespaceStats
                    {
                        Namespace = nsKvp.Key,
                        SmoothMs = nsSmoothMs,
                        PeakMs = nsPeakMs,
                        DisplayMs = nsSmoothMs,
                        DisplayPeakMs = nsPeakMs,
                        ActiveTypeCount = nsActive,
                        TotalCalls = nsCalls,
                        PctOfFrame = (nsSmoothMs / totalFrameMs) * 100.0,
                        Types = nsKvp.Value,
                        Subsystems = subStatsList
                    });
                }
                nsList2.Sort((a, b) => b.SmoothMs.CompareTo(a.SmoothMs));

                result.Add(new AssemblyStats
                {
                    AssemblyName = asmName,
                    SmoothMs = asmSmoothMs,
                    PeakMs = asmPeakMs,
                    DisplayMs = asmSmoothMs,
                    DisplayPeakMs = asmPeakMs,
                    ActiveTypeCount = asmActiveTypes,
                    TotalCalls = asmCalls,
                    PctOfFrame = (asmSmoothMs / totalFrameMs) * 100.0,
                    Namespaces = nsList2
                });
            }

            // Sort result
            switch (sortColumn)
            {
                case 0: // Name
                    result.Sort((a, b) => sortAsc ? string.Compare(a.AssemblyName, b.AssemblyName, StringComparison.OrdinalIgnoreCase) : string.Compare(b.AssemblyName, a.AssemblyName, StringComparison.OrdinalIgnoreCase));
                    break;
                case 1: // Avg Ms
                    result.Sort((a, b) => sortAsc ? a.SmoothMs.CompareTo(b.SmoothMs) : b.SmoothMs.CompareTo(a.SmoothMs));
                    break;
                case 2: // Peak Ms
                    result.Sort((a, b) => sortAsc ? a.PeakMs.CompareTo(b.PeakMs) : b.PeakMs.CompareTo(a.PeakMs));
                    break;
                case 3: // Active Types
                    result.Sort((a, b) => sortAsc ? a.ActiveTypeCount.CompareTo(b.ActiveTypeCount) : b.ActiveTypeCount.CompareTo(a.ActiveTypeCount));
                    break;
                case 4: // Calls
                    result.Sort((a, b) => sortAsc ? a.TotalCalls.CompareTo(b.TotalCalls) : b.TotalCalls.CompareTo(a.TotalCalls));
                    break;
                case 5: // % Frame
                    result.Sort((a, b) => sortAsc ? a.PctOfFrame.CompareTo(b.PctOfFrame) : b.PctOfFrame.CompareTo(a.PctOfFrame));
                    break;
                default:
                    result.Sort((a, b) => b.SmoothMs.CompareTo(a.SmoothMs));
                    break;
            }

            return result;
        }

        public static string ExportReport()
        {
            StringBuilder sb = new StringBuilder();
            var diag = BottleneckDetector.Analyze();

            sb.AppendLine("================================================================================");
            sb.AppendLine("              KSP LOW-LEVEL ENGINE & FPS PERFORMANCE DUMP (KSPPerformanceProfiler)     ");
            sb.AppendLine("================================================================================");
            sb.AppendLine($"Timestamp: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            sb.AppendLine($"PTR (Physics Time Ratio): {CurrentPTR * 100.0:F1}%");
            sb.AppendLine($"Current FPS: {CurrentFPS:F1} FPS  |  Avg FPS: {AvgFPS:F1} FPS");
            sb.AppendLine($"1% Low FPS: {OnePercentLowFPS:F1} FPS  |  0.1% Low FPS: {PointOnePercentLowFPS:F1} FPS");
            sb.AppendLine($"Frame Time Jitter: {FrameJitterMs:F2} ms");
            sb.AppendLine($"Total Frame Time: {TotalFrameMs:F2} ms");
            sb.AppendLine();
            sb.AppendLine("--------------------------------------------------------------------------------");
            sb.AppendLine("DIAGNOSTIC SUMMARY & BOTTLENECK ANALYSIS");
            sb.AppendLine("--------------------------------------------------------------------------------");
            sb.AppendLine($"Primary Bottleneck: {diag.Title}");
            sb.AppendLine($"Description: {diag.Description}");
            sb.AppendLine($"Suggested Advice: {diag.Advice}");
            sb.AppendLine();
            if (diag.TopCulprits != null && diag.TopCulprits.Count > 0)
            {
                sb.AppendLine("Top Offending Consumers:");
                for (int i = 0; i < diag.TopCulprits.Count; i++)
                {
                    var c = diag.TopCulprits[i];
                    sb.AppendLine($"  #{i + 1} [{c.SourceType}] {c.Name} ({c.AssemblyName}) => {c.SmoothMs:F3} ms/frame ({c.PctOfFrame:F1}%)");
                }
                sb.AppendLine();
            }

            sb.AppendLine("--------------------------------------------------------------------------------");
            sb.AppendLine("MACRO ENGINE PIPELINE BREAKDOWN");
            sb.AppendLine("--------------------------------------------------------------------------------");
            double totalMs = Math.Max(0.001, TotalFrameMs);
            sb.AppendLine($"  - PartModule Scripts:     {TotalModuleScriptMs:F2} ms ({(TotalModuleScriptMs / totalMs) * 100.0:F1}%)");
            sb.AppendLine($"  - Global Plugin Managers: {TotalPluginScriptMs:F2} ms ({(TotalPluginScriptMs / totalMs) * 100.0:F1}%)");
            sb.AppendLine($"  - PhysX Joints & Solver:  {PhysXJointsMs:F2} ms ({(PhysXJointsMs / totalMs) * 100.0:F1}%)");
            sb.AppendLine($"  - GPU Shaders & Render:   {RenderAndGpuMs:F2} ms ({(RenderAndGpuMs / totalMs) * 100.0:F1}%)");
            sb.AppendLine($"  - Camera Pipeline Step:   {CameraRenderMs:F2} ms");
            if (IsTUFXDetected)
            {
                sb.AppendLine($"  - TUFX Post-Processing:   {TUFXPostProcessMs:F2} ms ({TUFXActivePassCount} active passes: {TUFXActivePassSummary})");
                sb.AppendLine($"  - TUFX Camera Record CPU: {TUFXCpuBuildMs:F2} ms");
            }
            sb.AppendLine();

            sb.AppendLine("--------------------------------------------------------------------------------");
            sb.AppendLine("TOP GLOBAL PLUGIN MANAGERS");
            sb.AppendLine("--------------------------------------------------------------------------------");
            sb.AppendLine(string.Format("{0,-35} | {1,-10} | {2,-10} | {3,-8} | {4,-10} | {5,-20}", "Plugin Class", "Avg (ms)", "Peak (ms)", "Calls", "% Frame", "Assembly"));
            sb.AppendLine(new string('-', 98));

            foreach (var p in pluginStatsMap.Values.OrderByDescending(x => x.SmoothMs))
            {
                if (p.SmoothMs < 0.001 && p.PeakMs < 0.01) continue;
                double pct = (p.SmoothMs / totalMs) * 100.0;
                sb.AppendLine(string.Format("{0,-35} | {1,-10:F3} | {2,-10:F3} | {3,-8} | {4,-9:F1}% | {5,-20}",
                    p.TypeName.Length > 35 ? p.TypeName.Substring(0, 32) + "..." : p.TypeName,
                    p.SmoothMs,
                    p.PeakMs,
                    p.CurrentFrameCalls,
                    pct,
                    p.AssemblyName));
            }

            sb.AppendLine();
            sb.AppendLine("--------------------------------------------------------------------------------");
            sb.AppendLine("TOP CONSUMING PART MODULES");
            sb.AppendLine("--------------------------------------------------------------------------------");
            sb.AppendLine(string.Format("{0,-35} | {1,-10} | {2,-10} | {3,-8} | {4,-10} | {5,-20}", "Module Type", "Avg (ms)", "Peak (ms)", "Calls", "% Frame", "Assembly"));
            sb.AppendLine(new string('-', 98));

            foreach (var m in moduleStatsMap.Values.OrderByDescending(x => x.SmoothMs))
            {
                if (m.SmoothMs < 0.001 && m.PeakMs < 0.01) continue;
                double pct = (m.SmoothMs / totalMs) * 100.0;
                sb.AppendLine(string.Format("{0,-35} | {1,-10:F3} | {2,-10:F3} | {3,-8} | {4,-9:F1}% | {5,-20}",
                    m.TypeName.Length > 35 ? m.TypeName.Substring(0, 32) + "..." : m.TypeName,
                    m.SmoothMs,
                    m.PeakMs,
                    m.CurrentFrameCalls,
                    pct,
                    m.AssemblyName));
            }

            sb.AppendLine();
            sb.AppendLine("--------------------------------------------------------------------------------");
            sb.AppendLine("ASSEMBLY BREAKDOWN (Runtime CPU by DLL)");
            sb.AppendLine("--------------------------------------------------------------------------------");
            sb.AppendLine(string.Format("{0,-30} | {1,-10} | {2,-10} | {3,-8} | {4,-8} | {5,-8}", "Assembly", "Avg (ms)", "Peak (ms)", "Types", "Calls", "% Frame"));
            sb.AppendLine(new string('-', 86));

            var asmBreakdown = GetAssemblyBreakdown();
            foreach (var asm in asmBreakdown)
            {
                sb.AppendLine(string.Format("{0,-30} | {1,-10:F3} | {2,-10:F3} | {3,-8} | {4,-8} | {5,-7:F1}%",
                    asm.AssemblyName.Length > 30 ? asm.AssemblyName.Substring(0, 27) + "..." : asm.AssemblyName,
                    asm.SmoothMs,
                    asm.PeakMs,
                    asm.ActiveTypeCount,
                    asm.TotalCalls,
                    asm.PctOfFrame));

                if (asm.Namespaces != null)
                {
                    foreach (var ns in asm.Namespaces)
                    {
                        if (ns.SmoothMs < 0.001 && ns.TotalCalls == 0) continue;
                        sb.AppendLine(string.Format("  └─ {0,-26} | {1,-10:F3} | {2,-10:F3} | {3,-8} | {4,-8} | {5,-7:F1}%",
                            ns.Namespace.Length > 26 ? ns.Namespace.Substring(0, 23) + "..." : ns.Namespace,
                            ns.SmoothMs,
                            ns.PeakMs,
                            ns.ActiveTypeCount,
                            ns.TotalCalls,
                            ns.PctOfFrame));
                    }
                }
            }

            sb.AppendLine("================================================================================");
            return sb.ToString();
        }
    }
}
