using System;
using System.Collections.Generic;
using System.Linq;

namespace KSPPhysProfiler
{
    public enum BottleneckType
    {
        BalancedSmooth,
        PhysicsJointsBound,
        PluginScriptBound,
        ModuleScriptBound,
        GpuRenderBound,
        StutterJitterBound
    }

    public enum SeverityLevel
    {
        Normal,
        Warning,
        Critical
    }

    public class CulpritItem
    {
        public string Name;
        public string SourceType; // "Module", "Plugin", "Engine", "GPU"
        public double SmoothMs;
        public double PctOfFrame;
        public string AssemblyName;
    }

    public class DiagnosticResult
    {
        public BottleneckType Type;
        public SeverityLevel Severity;
        public string Title;
        public string Description;
        public string Advice;
        public string StatusColorHex;
        public List<CulpritItem> TopCulprits = new List<CulpritItem>();
    }

    public static class BottleneckDetector
    {
        public static DiagnosticResult Analyze()
        {
            DiagnosticResult res = new DiagnosticResult();

            double totalMs = Math.Max(0.001, ProfilerData.SmoothTotalFrameMs);
            double ptr = ProfilerData.SmoothPTR;
            double fps = ProfilerData.SmoothFPS;
            double avgFps = ProfilerData.AvgFPS;
            double onePctLow = ProfilerData.OnePercentLowFPS;
            double jitter = ProfilerData.FrameJitterMs;

            double modMs = ProfilerData.SmoothModuleScriptMs;
            double pluginMs = ProfilerData.SmoothPluginScriptMs;
            double physxMs = ProfilerData.SmoothPhysXJointsMs;
            double physicsStepMs = ProfilerData.SmoothPhysicsStepMs;
            double gpuMs = ProfilerData.SmoothRenderAndGpuMs;

            // Collect top individual offenders
            var topPlugins = ProfilerData.GetTopPlugins(3);
            var topModules = ProfilerData.GetTopModules(3);

            List<CulpritItem> candidates = new List<CulpritItem>();

            foreach (var p in topPlugins)
            {
                if (p.SmoothMs > 0.3)
                {
                    candidates.Add(new CulpritItem
                    {
                        Name = !string.IsNullOrEmpty(p.FriendlyName) ? p.FriendlyName : p.TypeName,
                        SourceType = p.IsTUFX ? "TUFX" : "Plugin",
                        SmoothMs = p.SmoothMs,
                        PctOfFrame = (p.SmoothMs / totalMs) * 100.0,
                        AssemblyName = p.AssemblyName
                    });
                }
            }

            if (ProfilerData.IsTUFXDetected && (ProfilerData.TUFXPostProcessMs > 0.8 || ProfilerData.TUFXCpuBuildMs > 0.8))
            {
                double tufxScore = Math.Max(ProfilerData.TUFXPostProcessMs, ProfilerData.TUFXCpuBuildMs);
                candidates.Add(new CulpritItem
                {
                    Name = $"TUFX Pipeline ({ProfilerData.TUFXActivePassCount} passes: {ProfilerData.TUFXActivePassSummary})",
                    SourceType = "TUFX",
                    SmoothMs = tufxScore,
                    PctOfFrame = (tufxScore / totalMs) * 100.0,
                    AssemblyName = "TUFX"
                });
            }

            foreach (var m in topModules)
            {
                if (m.SmoothMs > 0.3)
                {
                    candidates.Add(new CulpritItem
                    {
                        Name = m.TypeName,
                        SourceType = "Module",
                        SmoothMs = m.SmoothMs,
                        PctOfFrame = (m.SmoothMs / totalMs) * 100.0,
                        AssemblyName = m.AssemblyName
                    });
                }
            }

            if (physxMs > 2.0)
            {
                candidates.Add(new CulpritItem
                {
                    Name = "PhysX Joints & Solver",
                    SourceType = "Engine",
                    SmoothMs = physxMs,
                    PctOfFrame = (physxMs / totalMs) * 100.0,
                    AssemblyName = "UnityEngine.Physics"
                });
            }

            if (gpuMs > 5.0)
            {
                candidates.Add(new CulpritItem
                {
                    Name = "GPU Shaders & Render Wait",
                    SourceType = "GPU",
                    SmoothMs = gpuMs,
                    PctOfFrame = (gpuMs / totalMs) * 100.0,
                    AssemblyName = "Graphics Driver"
                });
            }

            res.TopCulprits = candidates.OrderByDescending(c => c.SmoothMs).Take(3).ToList();

            // Diagnosis Logic
            // 1. Check Physics / PTR Degradation first (The hallmark of KSP single-thread CPU lag)
            if (ptr < 0.85 || physicsStepMs > 22.0 || (physxMs > 12.0 && physxMs > totalMs * 0.4))
            {
                res.Type = BottleneckType.PhysicsJointsBound;
                res.Severity = ptr < 0.6 ? SeverityLevel.Critical : SeverityLevel.Warning;
                res.StatusColorHex = res.Severity == SeverityLevel.Critical ? "#FF3333" : "#FFAA00";
                res.Title = ProfilerI18n.Get("bn_physx_title");
                res.Description = ProfilerI18n.Format("bn_physx_desc", physicsStepMs, ptr * 100.0);
                res.Advice = ProfilerI18n.Get("bn_physx_tip");
                return res;
            }

            // 2. Check Mod Plugin overhead
            if (pluginMs > 6.0 && (pluginMs > totalMs * 0.25 || pluginMs > modMs * 1.5))
            {
                res.Type = BottleneckType.PluginScriptBound;
                res.Severity = pluginMs > 12.0 ? SeverityLevel.Critical : SeverityLevel.Warning;
                res.StatusColorHex = res.Severity == SeverityLevel.Critical ? "#FF3333" : "#FFAA00";
                res.Title = ProfilerI18n.Get("bn_plugin_title");
                res.Description = ProfilerI18n.Format("bn_plugin_desc", pluginMs, (pluginMs / totalMs) * 100.0);
                res.Advice = ProfilerI18n.Get("bn_plugin_tip");
                return res;
            }

            // 3. Check PartModule script overhead
            if (modMs > 8.0 && modMs > totalMs * 0.28)
            {
                res.Type = BottleneckType.ModuleScriptBound;
                res.Severity = modMs > 15.0 ? SeverityLevel.Critical : SeverityLevel.Warning;
                res.StatusColorHex = res.Severity == SeverityLevel.Critical ? "#FF3333" : "#FFAA00";
                res.Title = ProfilerI18n.Get("bn_module_title");
                res.Description = ProfilerI18n.Format("bn_module_desc", modMs, (modMs / totalMs) * 100.0);
                res.Advice = ProfilerI18n.Get("bn_module_tip");
                return res;
            }

            // 4. Check TUFX Post-Processing overload
            if (ProfilerData.IsTUFXDetected && (ProfilerData.TUFXPostProcessMs > 6.0 || (ProfilerData.TUFXPostProcessMs > totalMs * 0.25 && ProfilerData.TUFXPostProcessMs > 3.5)))
            {
                res.Type = BottleneckType.GpuRenderBound;
                res.Severity = ProfilerData.TUFXPostProcessMs > 14.0 ? SeverityLevel.Critical : SeverityLevel.Warning;
                res.StatusColorHex = res.Severity == SeverityLevel.Critical ? "#FF3333" : "#FFAA00";
                res.Title = ProfilerI18n.Get("bn_tufx_title");
                res.Description = ProfilerI18n.Format("bn_tufx_desc", ProfilerData.TUFXPostProcessMs, (ProfilerData.TUFXPostProcessMs / totalMs) * 100.0, ProfilerData.TUFXActivePassCount);
                res.Advice = ProfilerI18n.Get("bn_tufx_tip");
                return res;
            }

            // 5. Check Micro-Stutter / 1% Low drops / Jitter spikes
            if ((onePctLow < avgFps * 0.55 && avgFps > 22.0 && onePctLow < 25.0) || (jitter > 8.0 && avgFps > 20.0))
            {
                res.Type = BottleneckType.StutterJitterBound;
                res.Severity = SeverityLevel.Warning;
                res.StatusColorHex = "#FFAA00";
                res.Title = ProfilerI18n.Get("bn_stutter_title");
                res.Description = ProfilerI18n.Format("bn_stutter_desc", avgFps, onePctLow, jitter);
                res.Advice = ProfilerI18n.Get("bn_stutter_tip");
                return res;
            }

            // 6. Check GPU Render Bound
            if (gpuMs > 14.0 && (modMs + pluginMs < totalMs * 0.35))
            {
                res.Type = BottleneckType.GpuRenderBound;
                res.Severity = gpuMs > 25.0 ? SeverityLevel.Critical : SeverityLevel.Warning;
                res.StatusColorHex = res.Severity == SeverityLevel.Critical ? "#FF3333" : "#FFAA00";
                res.Title = ProfilerI18n.Get("bn_gpu_title");
                res.Description = ProfilerI18n.Format("bn_gpu_desc", gpuMs, (gpuMs / totalMs) * 100.0);
                res.Advice = ProfilerI18n.Get("bn_gpu_tip");
                return res;
            }

            // 6. Balanced & Smooth
            res.Type = BottleneckType.BalancedSmooth;
            res.Severity = SeverityLevel.Normal;
            res.StatusColorHex = "#33FF33";
            res.Title = ProfilerI18n.Get("bn_smooth_title");
            res.Description = ProfilerI18n.Get("bn_smooth_desc");
            res.Advice = ProfilerI18n.Get("bn_smooth_tip");
            return res;
        }
    }
}
