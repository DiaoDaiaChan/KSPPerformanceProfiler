using System;
using System.Collections.Generic;
using UnityEngine;

namespace KSPPhysProfiler
{
    public enum LanguageMode
    {
        Auto = 0,
        Chinese = 1,
        English = 2
    }

    public static class ProfilerI18n
    {
        public static LanguageMode CurrentMode { get; set; } = LanguageMode.Auto;

        private static readonly Dictionary<string, string> zhDict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            // App Title & Header
            { "app_title", "KSP 底层性能与物理瓶颈分析器 (KSPPhysProfiler)" },
            { "app_mini_title", "性能监控 HUD" },
            { "enabled", "已启用监控" },
            { "disabled", "已暂停监控" },
            { "reset_peak", "重置峰值" },
            { "export_dump", "导出诊断报告" },
            { "mode_mini", "迷你 HUD" },
            { "mode_full", "完整面板" },
            { "lang_label", "语言" },
            { "lang_auto", "自动" },
            { "lang_zh", "简体中文" },
            { "lang_en", "English" },

            // Metrics
            { "metric_ptr", "物理实速比 (PTR)" },
            { "metric_fps", "实时帧率 (FPS)" },
            { "metric_avg_fps", "平均帧率" },
            { "metric_1pct_low", "1% Low 帧率" },
            { "metric_01pct_low", "0.1% Low 帧率" },
            { "metric_jitter", "帧时间抖动" },
            { "metric_total_frame", "总帧时间" },
            { "metric_modules", "部件模块 (Modules)" },
            { "metric_plugins", "全局插件 (Plugins)" },
            { "metric_physx", "物理解算 (PhysX/Joints)" },
            { "metric_gpu_render", "GPU/渲染管线" },
            { "metric_camera", "相机渲染调用" },
            { "metric_overhead", "引擎调度与其他" },

            // Tabs
            { "tab_graph", "📈 FPS与掉帧波形" },
            { "tab_plugins", "🧩 全局插件管理器" },
            { "tab_modules", "⚙️ PartModule 模块耗时" },
            { "tab_parts", "🚀 零件与载具分析" },
            { "tab_help", "📖 瓶颈与排错指南" },
            { "tab_assembly", "📦 程序集占用" },

            // Table Columns & Controls
            { "col_plugin_class", "插件管理器类名" },
            { "col_module_type", "部件模块类型" },
            { "col_part_title", "零件名称" },
            { "col_vessel_name", "所属载具" },
            { "col_avg_ms", "平均耗时 (ms/帧)" },
            { "col_peak_ms", "峰值耗时 (ms)" },
            { "col_calls", "调用次数/帧" },
            { "col_pct_frame", "帧时间占比" },
            { "col_assembly", "程序集 (Mod DLL)" },
            { "search_placeholder", "🔍 搜索名称/程序集..." },
            { "clear_search", "清除" },
            { "no_data_plugins", "暂无全局插件监控数据。" },
            { "no_data_modules", "暂无部件模块耗时数据。" },
            { "no_data_parts", "暂无高耗时零件数据。" },
            { "no_data_assembly", "暂无程序集数据，请等待数据采集。" },
            { "col_assembly_name", "程序集名称" },
            { "col_active_types", "活跃类型数" },
            { "col_total_calls", "总调用次数" },
            { "col_namespace", "命名空间" },
            { "patched_stats", "已注入: {0} 个部件模块方法 | {1} 个插件方法" },
            { "subsystem_toggle_on", "📁 子系统分组: 开" },
            { "subsystem_toggle_off", "📁 子系统分组: 关" },
            { "subsys_timing", "⏱️ 时间与物理调度 (Timing & Dispatch)" },
            { "subsys_vessel", "🚀 载具与动力学核心 (Vessel & Part Dynamics)" },
            { "subsys_celestial", "🪐 天体与轨道系统 (Celestial & Orbit)" },
            { "subsys_kerbal", "👨‍🚀 乘员与舱外活动 (Kerbal & EVA)" },
            { "subsys_editor", "🛠️ 编辑器与装配 (Editor & Construction)" },
            { "subsys_controls", "🎮 飞行操纵与姿态 (Flight Controls)" },
            { "subsys_vfx", "🎨 视觉特效与相机 (VFX & Camera)" },
            { "subsys_system", "⚙️ 系统设置与存档 (System & Settings)" },
            { "subsys_other", "🧩 其他全局核心 (Other Global)" },
            { "col_method_name", "执行方法 / 委托回调" },
            { "dispatcher_tag", "⚡ 调度器穿透" },

            // Bottleneck Detector - Categories & Titles
            { "bn_title", "智能瓶颈诊断" },
            { "bn_top_culprits", "主要性能消耗源 (Top Offenders):" },
            { "bn_smooth_title", "🟢 运行极度流畅 (无明显瓶颈)" },
            { "bn_smooth_desc", "当前物理时钟满速 (PTR 100%)，帧率平稳且抖动极低，系统性能处于理想状态。" },
            { "bn_smooth_tip", "当前配置与载具规模表现优秀，可维持现有画质与部件规模。" },

            { "bn_physx_title", "🛑 物理引擎与关节过载 (PhysX & Joints Bound)" },
            { "bn_physx_desc", "物理计算严重超时 (耗时 {0:F1}ms)，物理实速比降至 {1:F1}% (游戏出现黄/红时钟减速)。" },
            { "bn_physx_tip", "主要原因：载具零件数过多、AutoStrut 刚化过度、复杂对接/碰撞解算。建议：精简零件数量、安装 KSP-Recall/KSPCommunityFixes 优化物理关节、避免过量自动刚化。" },

            { "bn_plugin_title", "🛑 全局 Mod 插件脚本过载 (Mod Plugin Overhead)" },
            { "bn_plugin_desc", "外部 Mod 插件管理器脚本每帧占用 {0:F1}ms (占总帧时间的 {1:F1}%)，严重拖累帧率。" },
            { "bn_plugin_tip", "请在下方【全局插件管理器】标签页查看耗时最高的 Mod，常见原因：Scatterer/EVE 大规模更新、Principia 高精度引力步进、未优化的后台计算插件。" },

            { "bn_module_title", "🛑 部件模块脚本过载 (PartModule Script Bound)" },
            { "bn_module_desc", "载具上的 PartModule 脚本每帧占用 {0:F1}ms (占总帧时间的 {1:F1}%)。" },
            { "bn_module_tip", "载具上某个部件模块（如 Waterfall 发动机特效、FAR 复杂气动计算、资源转化器）正在占用过多 CPU 周期。可查看【PartModule 耗时】标签页定位具体模块。" },

            { "bn_gpu_title", "🛑 GPU 显卡渲染或着色器瓶颈 (GPU / Render Bound)" },
            { "bn_gpu_desc", "GPU 渲染与显卡等待耗时 {0:F1}ms (占总帧时间的 {1:F1}%)，CPU 脚本耗时正常但显卡满载。" },
            { "bn_gpu_tip", "主要原因：分辨率过高、TUFX 后处理抗锯齿、Parallax 地表细分/置换贴图、体积云着色器。建议：适当调低抗锯齿、地表曲面细分质量或阴影级联距离。" },

            { "bn_tufx_title", "🛑 TUFX 画面后处理负载过高 (TUFX Post-Processing Bound)" },
            { "bn_tufx_desc", "TUFX 画面后处理耗时 {0:F1}ms (占总帧时间的 {1:F1}%)，当前运行了 {2} 个特效通道。" },
            { "bn_tufx_tip", "建议在 TUFX 设置中关闭重型计算通道（如 GTAO 环境光遮蔽、SSGI 屏幕全局光照、SSR 反射），或调低抗锯齿级别 (TAA/SMAA)；游戏中可随时按 F11 开启 Master Bypass 完全绕过 TUFX 对比基准。" },
            { "tufx_status_header", "TUFX 画面后处理通道状态" },
            { "tufx_active_passes", "激活通道数" },
            { "tufx_post_process_ms", "后处理耗时" },
            { "tufx_cpu_build_ms", "相机指令录制" },

            { "bn_stutter_title", "⚠️ 严重微卡顿与帧时间抖动 (Micro-Stutter / GC Spikes)" },
            { "bn_stutter_desc", "虽然平均帧率为 {0:F1} FPS，但 1% Low 跌至 {1:F1} FPS，帧抖动高达 {2:F1}ms。" },
            { "bn_stutter_tip", "主要原因：Unity Mono 堆内存频繁垃圾回收 (GC Collect)、突发磁盘 IO、或特定 Mod 每隔数秒执行大循环。建议：安装 MemGraph / HeapPadder 分配更大的堆内存缓冲区以延缓 GC。" },

            // Frame Budget Breakdown Bar
            { "budget_bar_title", "宏观帧时间预算占比 (Frame Time Budget)" },
            { "legend_modules", "部件模块" },
            { "legend_plugins", "全局插件" },
            { "legend_physx", "物理解算" },
            { "legend_gpu", "GPU与渲染" },
            { "legend_overhead", "引擎调度" },

            // Graph Labels
            { "graph_title", "实时帧生成时间历史 (最近 100 帧) - 尖峰表示微卡顿 (Spikes)" },
            { "graph_baseline_60", "60 FPS 理想线 (16.6ms)" },
            { "graph_baseline_30", "30 FPS 流畅线 (33.3ms)" },
            { "graph_legend_green", "🟢 平滑 (<33ms)" },
            { "graph_legend_yellow", "🟡 轻微波动 (33~50ms)" },
            { "graph_legend_red", "🔴 严重卡顿 (>50ms)" },
            { "graph_phase_metrics", "底层引擎各阶段耗时" },

            // Help & Troubleshooting Tab
            { "help_h1", "KSP 性能指标详解与排错指南" },
            { "help_ptr_q", "什么是 PTR (物理实速比 Physics Time Ratio)？" },
            { "help_ptr_a", "• 100% (绿色)：物理仿真 1 秒 = 真实世界 1 秒。\n• <80% (黄色)：物理计算开始吃力，游戏时钟变黄，出现慢动作。\n• <50% (红色)：CPU 单核物理线程严重过载，必须精简载具零件或减少碰撞体。" },
            { "help_1pct_q", "什么是 1% Low 和 0.1% Low 帧率？" },
            { "help_1pct_a", "• 1% Low：统计最近最慢的 1% 帧，反映日常游戏中的微卡顿 (Micro-Stutters)。若平均 FPS 很高但 1% Low 很低，画面会感觉非常不流畅、顿挫感强。\n• 0.1% Low：反映最严重的极值卡死 (Spikes)，通常由 Unity 垃圾回收 (GC) 或突发大量计算引起。" },
            { "help_jitter_q", "什么是 Jitter (帧时间抖动)？" },
            { "help_jitter_a", "• 衡量前后两帧耗时的方差标准差。低于 2ms 表示帧生成极其均匀平稳；高于 6ms 表示帧时间起伏巨大，画面撕裂与跳帧明显。" },
            { "help_mod_tuning_q", "常见 Mod 优化建议：" },
            { "help_mod_tuning_a", "1. 物理卡顿：使用 KSPCommunityFixes，减少每艘飞船上的自动刚化数量。\n2. 内存与 GC 卡顿：使用 HeapPadder 或 MemGraph 将 Mono 堆内存垫高 1~2GB，避免每几秒触发一次 GC。\n3. GPU 渲染卡顿：调低 TUFX / Scatterer 的阴影和抗锯齿倍率，关闭不必要的屏幕空间反射。" },

            // Export & Messages
            { "msg_report_saved", "性能诊断报告已成功保存至:\nGameData/KSPPhysProfiler/Logs/{0}" },
            { "msg_report_fail", "保存性能报告失败: {0}" },
            { "msg_ui_opened", "已打开性能分析器窗口" },
            { "msg_ui_closed", "已关闭性能分析器窗口" },
            { "msg_hud_opened", "已切换至迷你监控 HUD" }
        };

        private static readonly Dictionary<string, string> enDict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            // App Title & Header
            { "app_title", "KSP Low-Level Engine & Physics Profiler (KSPPhysProfiler)" },
            { "app_mini_title", "Profiler HUD" },
            { "enabled", "Profile Enabled" },
            { "disabled", "Profile Paused" },
            { "reset_peak", "Reset Peak Data" },
            { "export_dump", "Export Diagnostic Dump" },
            { "mode_mini", "Mini HUD" },
            { "mode_full", "Full Window" },
            { "lang_label", "Language" },
            { "lang_auto", "Auto" },
            { "lang_zh", "简体中文" },
            { "lang_en", "English" },

            // Metrics
            { "metric_ptr", "Physics Ratio (PTR)" },
            { "metric_fps", "Real-time FPS" },
            { "metric_avg_fps", "Avg FPS" },
            { "metric_1pct_low", "1% Low FPS" },
            { "metric_01pct_low", "0.1% Low FPS" },
            { "metric_jitter", "Frame Jitter" },
            { "metric_total_frame", "Total Frame Time" },
            { "metric_modules", "PartModules" },
            { "metric_plugins", "Global Plugins" },
            { "metric_physx", "PhysX / Joints" },
            { "metric_gpu_render", "GPU / Render Pipeline" },
            { "metric_camera", "Camera Render Calls" },
            { "metric_overhead", "Engine Overhead" },

            // Tabs
            { "tab_graph", "📈 FPS & Frame Time" },
            { "tab_plugins", "🧩 Global Plugins" },
            { "tab_modules", "⚙️ PartModules CPU" },
            { "tab_parts", "🚀 Parts & Vessels" },
            { "tab_help", "📖 Bottlenecks & Guide" },
            { "tab_assembly", "📦 Assembly Breakdown" },

            // Table Columns & Controls
            { "col_plugin_class", "Plugin Manager Class" },
            { "col_module_type", "PartModule Type" },
            { "col_part_title", "Part Title" },
            { "col_vessel_name", "Vessel Name" },
            { "col_avg_ms", "Avg Ms/Frame" },
            { "col_peak_ms", "Peak Ms" },
            { "col_calls", "Calls/Frame" },
            { "col_pct_frame", "% Frame Time" },
            { "col_assembly", "Assembly (Mod DLL)" },
            { "search_placeholder", "🔍 Search name / assembly..." },
            { "clear_search", "Clear" },
            { "no_data_plugins", "No plugin manager data recorded yet." },
            { "no_data_modules", "No PartModule timing data recorded yet." },
            { "no_data_parts", "No heavy part timing data available." },
            { "no_data_assembly", "No assembly data yet, waiting for profiling data." },
            { "col_assembly_name", "Assembly Name" },
            { "col_active_types", "Active Types" },
            { "col_total_calls", "Total Calls" },
            { "col_namespace", "Namespace" },
            { "patched_stats", "Patched: {0} Module methods | {1} Plugin methods" },
            { "subsystem_toggle_on", "📁 Subsystems: ON" },
            { "subsystem_toggle_off", "📁 Subsystems: OFF" },
            { "subsys_timing", "⏱️ Timing & Dispatch" },
            { "subsys_vessel", "🚀 Vessel & Part Dynamics" },
            { "subsys_celestial", "🪐 Celestial & Orbit" },
            { "subsys_kerbal", "👨‍🚀 Kerbal & EVA" },
            { "subsys_editor", "🛠️ Editor & Construction" },
            { "subsys_controls", "🎮 Flight Controls" },
            { "subsys_vfx", "🎨 VFX & Camera" },
            { "subsys_system", "⚙️ System & Settings" },
            { "subsys_other", "🧩 Other Global" },
            { "col_method_name", "Method / Sub-Invocation" },
            { "dispatcher_tag", "⚡ Dispatcher" },

            // Bottleneck Detector - Categories & Titles
            { "bn_title", "Smart Bottleneck Diagnostics" },
            { "bn_top_culprits", "Top Performance Consumers (Top Offenders):" },
            { "bn_smooth_title", "🟢 Perfectly Smooth (No Severe Bottleneck)" },
            { "bn_smooth_desc", "Physics simulation is at full rate (PTR 100%), frame rate is stable, and jitter is minimal." },
            { "bn_smooth_tip", "System performance is optimal under the current flight condition and vessel size." },

            { "bn_physx_title", "🛑 Physics Engine & Joint Overload (PhysX & Joints Bound)" },
            { "bn_physx_desc", "Physics calculation is exceeding frame budget ({0:F1}ms), PTR dropped to {1:F1}% (Yellow/Red simulation clock)." },
            { "bn_physx_tip", "Main causes: Too many parts on active vessel, excessive AutoStruts, complex docking/collision meshes. Suggestions: Simplify vessel parts, reduce autostrut usage, install KSPCommunityFixes." },

            { "bn_plugin_title", "🛑 Global Mod Plugin Script Overload (Mod Plugin Overhead)" },
            { "bn_plugin_desc", "External Mod Plugin Managers are consuming {0:F1}ms per frame ({1:F1}% of frame budget)." },
            { "bn_plugin_tip", "Inspect the 'Global Plugins' tab to locate the heaviest mods (e.g. Scatterer, EVE, Principia, background simulations)." },

            { "bn_module_title", "🛑 PartModule Script Overload (PartModule Script Bound)" },
            { "bn_module_desc", "Vessel PartModule scripts are taking {0:F1}ms per frame ({1:F1}% of frame budget)." },
            { "bn_module_tip", "Specific modules on your vessel (e.g., Waterfall visual effects, FAR aerodynamics, resource converters) are consuming significant CPU cycles." },

            { "bn_gpu_title", "🛑 GPU Graphics & Shaders Bottleneck (GPU / Render Bound)" },
            { "bn_gpu_desc", "GPU rendering and draw wait is taking {0:F1}ms ({1:F1}% of frame budget), while CPU scripts are lightweight." },
            { "bn_gpu_tip", "Main causes: High display resolution, heavy post-processing (TUFX/anti-aliasing), Parallax terrain shaders, volumetric clouds. Suggestions: Lower shadow cascade distance or antialiasing." },

            { "bn_tufx_title", "🛑 Heavy TUFX Post-Processing Overhead" },
            { "bn_tufx_desc", "TUFX post-processing takes {0:F1}ms ({1:F1}% of frame), running {2} active effect passes." },
            { "bn_tufx_tip", "Consider disabling heavy screen-space passes (GTAO, SSGI, SSR) or lowering AA level (TAA/SMAA) in TUFX profile settings. Press F11 anytime to toggle Master Bypass and benchmark vanilla performance." },
            { "tufx_status_header", "TUFX Post-Processing Pipeline Status" },
            { "tufx_active_passes", "Active Passes" },
            { "tufx_post_process_ms", "Post-Process Time" },
            { "tufx_cpu_build_ms", "Camera Record Time" },

            { "bn_stutter_title", "⚠️ Severe Micro-Stutters & Jitter (Micro-Stutter / GC Spikes)" },
            { "bn_stutter_desc", "Average FPS is {0:F1}, but 1% Low plummeted to {1:F1} FPS with high jitter ({2:F1}ms)." },
            { "bn_stutter_tip", "Main causes: Unity Mono garbage collection (GC) pauses, burst disk/audio I/O, or cyclic mod routines. Suggestions: Install MemGraph / HeapPadder to pad Mono heap and prevent frequent GC cycles." },

            // Frame Budget Breakdown Bar
            { "budget_bar_title", "Frame Time Budget Breakdown" },
            { "legend_modules", "PartModules" },
            { "legend_plugins", "Plugins" },
            { "legend_physx", "PhysX / Joints" },
            { "legend_gpu", "GPU / Render" },
            { "legend_overhead", "Overhead" },

            // Graph Labels
            { "graph_title", "Real-time Frame Time History (Last 100 Frames) - Spikes indicate micro-stutters" },
            { "graph_baseline_60", "60 FPS Target (16.6ms)" },
            { "graph_baseline_30", "30 FPS Minimum (33.3ms)" },
            { "graph_legend_green", "🟢 Smooth (<33ms)" },
            { "graph_legend_yellow", "🟡 Mild Variation (33~50ms)" },
            { "graph_legend_red", "🔴 Heavy Stutter (>50ms)" },
            { "graph_phase_metrics", "Low-Level Engine Phase Metrics" },

            // Help & Troubleshooting Tab
            { "help_h1", "Performance Metrics & Troubleshooting Guide" },
            { "help_ptr_q", "What is PTR (Physics Time Ratio)?" },
            { "help_ptr_a", "• 100% (Green): 1 sec in physics = 1 sec in real world.\n• <80% (Yellow): Physics is lagging; game clock turns yellow and slow-motion occurs.\n• <50% (Red): CPU physics thread is fully saturated; vessel simplification required." },
            { "help_1pct_q", "What are 1% Low and 0.1% Low FPS?" },
            { "help_1pct_a", "• 1% Low: The 99th percentile slowest frames. Indicates real-world stuttering. If Avg FPS is 60 but 1% Low is 15, the game will feel visibly jerky.\n• 0.1% Low: Severe freeze spikes (usually caused by GC sweeps or burst operations)." },
            { "help_jitter_q", "What is Frame Jitter?" },
            { "help_jitter_a", "• Standard deviation of frame times. Below 2ms is exceptionally smooth; above 6ms causes prominent tearing and uneven pacing." },
            { "help_mod_tuning_q", "Mod Optimization Tips:" },
            { "help_mod_tuning_a", "1. Physics Lag: Install KSPCommunityFixes and avoid excessive AutoStruts.\n2. GC Hitching: Install HeapPadder / MemGraph to allocate a larger heap cushion.\n3. GPU Lag: Tweak Scatterer / TUFX antialiasing and Parallax terrain tessellation settings." },

            // Export & Messages
            { "msg_report_saved", "Performance diagnostic report saved to:\nGameData/KSPPhysProfiler/Logs/{0}" },
            { "msg_report_fail", "Failed to save performance report: {0}" },
            { "msg_ui_opened", "Performance Profiler window opened" },
            { "msg_ui_closed", "Performance Profiler window closed" },
            { "msg_hud_opened", "Switched to Mini Profiler HUD" }
        };

        public static bool IsChinese
        {
            get
            {
                if (CurrentMode == LanguageMode.Chinese) return true;
                if (CurrentMode == LanguageMode.English) return false;

                // Auto detection
                try
                {
                    if (KSP.Localization.Localizer.CurrentLanguage != null)
                    {
                        string lang = KSP.Localization.Localizer.CurrentLanguage.ToLowerInvariant();
                        if (lang.Contains("zh") || lang.Contains("chinese") || lang.Contains("cn"))
                        {
                            return true;
                        }
                    }
                }
                catch
                {
                    // Fallback to system culture if KSP Localizer not ready
                }

                try
                {
                    string sysLang = System.Globalization.CultureInfo.CurrentUICulture.TwoLetterISOLanguageName;
                    if (sysLang.Equals("zh", StringComparison.OrdinalIgnoreCase)) return true;
                }
                catch { }

                return false;
            }
        }

        public static string Get(string key)
        {
            var dict = IsChinese ? zhDict : enDict;
            if (dict.TryGetValue(key, out string val))
            {
                return val;
            }
            if (enDict.TryGetValue(key, out string enVal))
            {
                return enVal;
            }
            return key;
        }

        public static string Format(string key, params object[] args)
        {
            string template = Get(key);
            try
            {
                return string.Format(template, args);
            }
            catch
            {
                return template;
            }
        }

        public static void ToggleNextLanguage()
        {
            if (CurrentMode == LanguageMode.Auto)
                CurrentMode = LanguageMode.Chinese;
            else if (CurrentMode == LanguageMode.Chinese)
                CurrentMode = LanguageMode.English;
            else
                CurrentMode = LanguageMode.Auto;
        }

        public static string GetCurrentLanguageButtonText()
        {
            switch (CurrentMode)
            {
                case LanguageMode.Chinese:
                    return "🌐 语言: 简体中文";
                case LanguageMode.English:
                    return "🌐 Lang: English";
                default:
                    return IsChinese ? "🌐 语言: 自动(中)" : "🌐 Lang: Auto(EN)";
            }
        }
    }
}
