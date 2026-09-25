using System;
using System.Collections.Generic;
using UnityEngine;

namespace KSPPhysProfiler
{
    public class ProfilerUI : MonoBehaviour
    {
        public static bool IsVisible = false;
        public static bool IsMiniHud = false;

        private Rect fullWindowRect;
        private Rect miniWindowRect;
        private int selectedTab = 0;

        private Vector2 scrollPosPlugins = Vector2.zero;
        private Vector2 scrollPosModules = Vector2.zero;
        private Vector2 scrollPosParts = Vector2.zero;
        private Vector2 scrollPosHelp = Vector2.zero;
        private Vector2 scrollPosAssembly = Vector2.zero;

        // Search Filters
        private string searchPlugins = "";
        private string searchModules = "";
        private string searchParts = "";
        private string searchAssembly = "";

        // Sorting (0: Name, 1: AvgMs, 2: PeakMs, 3: Calls, 4: Pct/Assembly)
        private int sortPluginsCol = 1;
        private bool sortPluginsAsc = false;

        private int sortModulesCol = 1;
        private bool sortModulesAsc = false;

        private int sortPartsCol = 2;
        private bool sortPartsAsc = false;

        private int sortAssemblyCol = 1;
        private bool sortAssemblyAsc = false;

        // Assembly tab expand/collapse state
        private HashSet<string> expandedAssemblies = new HashSet<string>();
        private HashSet<string> expandedNamespaces = new HashSet<string>();
        private HashSet<string> expandedSubsystems = new HashSet<string>();
        private HashSet<string> expandedTypes = new HashSet<string>();
        private HashSet<string> expandedMethods = new HashSet<string>();
        private bool groupBySubsystems = true;

        private bool showDetailedAdvice = true;

        // UI Refresh Throttling Caches & Timers (250ms interval)
        private const float UI_THROTTLE_INTERVAL = 0.25f;
        private float lastDiagUpdateTime = 0f;
        private DiagnosticResult cachedDiag = null;

        private float lastTableUpdateTime = 0f;
        private List<ModuleStats> cachedTopPlugins = new List<ModuleStats>();
        private List<ModuleStats> cachedTopModules = new List<ModuleStats>();
        private List<PartStats> cachedTopParts = new List<PartStats>();
        private string lastSearchPlugins = "";
        private string lastSearchModules = "";
        private string lastSearchParts = "";
        private int lastSortPluginsCol = -1;
        private bool lastSortPluginsAsc = false;
        private int lastSortModulesCol = -1;
        private bool lastSortModulesAsc = false;
        private int lastSortPartsCol = -1;
        private bool lastSortPartsAsc = false;
        private float lastAssemblyUpdateTime = 0f;
        private List<AssemblyStats> cachedAssemblyStats = new List<AssemblyStats>();
        private string lastSearchAssembly = "";
        private int lastSortAssemblyCol = -1;
        private bool lastSortAssemblyAsc = false;

        // UI Styles
        private GUIStyle headerStyle;
        private GUIStyle tableHeaderStyle;
        private GUIStyle tableHeaderBtnStyle;
        private GUIStyle ptrStyle;
        private GUIStyle fpsStyle;
        private GUIStyle cardStyle;
        private GUIStyle cardAccentStyle;
        private GUIStyle tipStyle;
        private GUIStyle miniHudStyle;
        private GUIStyle badgeStyle;
        private bool stylesInitialized = false;

        private Texture2D whitePixelTex;

        private void Awake()
        {
            float fullW = 1000f;
            float fullH = 680f;
            fullWindowRect = new Rect((Screen.width - fullW) / 2f, (Screen.height - fullH) / 2f, fullW, fullH);

            float miniW = 360f;
            float miniH = 180f;
            miniWindowRect = new Rect(Screen.width - miniW - 20f, 60f, miniW, miniH);

            whitePixelTex = new Texture2D(1, 1);
            whitePixelTex.SetPixel(0, 0, Color.white);
            whitePixelTex.Apply();
        }

        private void OnGUI()
        {
            if (!IsVisible) return;

            if (HighLogic.Skin != null)
            {
                GUI.skin = HighLogic.Skin;
            }

            InitStyles();

            if (IsMiniHud)
            {
                miniWindowRect = GUI.Window(948202, miniWindowRect, DrawMiniHudWindow, ProfilerI18n.Get("app_mini_title"));
            }
            else
            {
                fullWindowRect = GUI.Window(948201, fullWindowRect, DrawFullWindow, ProfilerI18n.Get("app_title"));
            }
        }

        private void InitStyles()
        {
            if (stylesInitialized) return;
            if (GUI.skin == null) return;

            headerStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 12,
                fontStyle = FontStyle.Bold,
                richText = true
            };

            tableHeaderStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 12,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleLeft,
                richText = true
            };

            tableHeaderBtnStyle = new GUIStyle(GUI.skin.button)
            {
                fontSize = 11,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleLeft,
                margin = new RectOffset(1, 1, 1, 1),
                padding = new RectOffset(4, 4, 3, 3)
            };

            ptrStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 14,
                fontStyle = FontStyle.Bold,
                richText = true
            };

            fpsStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 13,
                fontStyle = FontStyle.Bold,
                richText = true
            };

            cardStyle = new GUIStyle(GUI.skin.box)
            {
                padding = new RectOffset(8, 8, 6, 6),
                margin = new RectOffset(0, 0, 2, 2)
            };

            cardAccentStyle = new GUIStyle(GUI.skin.box)
            {
                padding = new RectOffset(10, 10, 8, 8),
                margin = new RectOffset(0, 0, 3, 3)
            };

            tipStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 11,
                fontStyle = FontStyle.Normal,
                richText = true,
                wordWrap = true
            };

            miniHudStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 12,
                fontStyle = FontStyle.Bold,
                richText = true
            };

            badgeStyle = new GUIStyle(GUI.skin.box)
            {
                fontSize = 11,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                padding = new RectOffset(6, 6, 2, 2)
            };

            stylesInitialized = true;
        }

        private DiagnosticResult GetCachedDiagnostic()
        {
            float now = Time.realtimeSinceStartup;
            if (cachedDiag == null || now - lastDiagUpdateTime >= UI_THROTTLE_INTERVAL)
            {
                cachedDiag = BottleneckDetector.Analyze();
                lastDiagUpdateTime = now;
            }
            return cachedDiag;
        }

        #region Full Window Rendering

        private void DrawFullWindow(int windowID)
        {
            GUILayout.BeginVertical();

            DrawTopMetricBar();
            DrawToolbar();
            DrawBottleneckDiagnosticCard();
            DrawMacroBudgetBar();

            GUILayout.Space(4);

            // Tab Bar
            string[] tabNames = new string[]
            {
                ProfilerI18n.Get("tab_graph"),
                ProfilerI18n.Get("tab_plugins"),
                ProfilerI18n.Get("tab_modules"),
                ProfilerI18n.Get("tab_parts"),
                ProfilerI18n.Get("tab_assembly"),
                ProfilerI18n.Get("tab_help")
            };

            selectedTab = GUILayout.Toolbar(selectedTab, tabNames, GUILayout.Height(26));
            GUILayout.Space(4);

            switch (selectedTab)
            {
                case 0:
                    DrawFpsGraphTab();
                    break;
                case 1:
                    DrawPluginsTab();
                    break;
                case 2:
                    DrawModulesTab();
                    break;
                case 3:
                    DrawPartsTab();
                    break;
                case 4:
                    DrawAssemblyTab();
                    break;
                case 5:
                    DrawHelpTab();
                    break;
            }

            GUILayout.EndVertical();
            GUI.DragWindow();
        }

        private void DrawTopMetricBar()
        {
            double ptr = ProfilerData.SmoothPTR * 100.0;
            string ptrColor = ptr >= 85.0 ? "#33FF33" : (ptr >= 60.0 ? "#FFFF33" : "#FF3333");
            string ptrStatusText = ptr >= 95.0 ? "100%" : $"{ptr:F1}%";

            double fps = ProfilerData.SmoothFPS;
            double avgFps = ProfilerData.AvgFPS;
            string fpsColor = fps >= 45.0 ? "#33FF33" : (fps >= 25.0 ? "#FFFF33" : "#FF3333");

            double fps1pct = ProfilerData.OnePercentLowFPS;
            string fps1Color = fps1pct >= 30.0 ? "#33FF33" : (fps1pct >= 15.0 ? "#FFFF33" : "#FF3333");

            double jitter = ProfilerData.FrameJitterMs;
            string jitterColor = jitter < 3.0 ? "#33FF33" : (jitter < 6.5 ? "#FFFF33" : "#FF3333");

            GUILayout.BeginHorizontal(cardStyle);

            // PTR Badge
            GUILayout.Label($"<b>{ProfilerI18n.Get("metric_ptr")}:</b> <color={ptrColor}><b>{ptrStatusText}</b></color>", ptrStyle, GUILayout.Width(170));

            // FPS & Avg FPS Badge
            GUILayout.Label($"<b>FPS:</b> <color={fpsColor}><b>{fps:F1}</b></color> (Avg: {avgFps:F1})", fpsStyle, GUILayout.Width(190));

            // 1% Low & 0.1% Low
            GUILayout.Label($"<b>1% Low:</b> <color={fps1Color}><b>{fps1pct:F1}</b></color> | <b>0.1% Low:</b> {ProfilerData.PointOnePercentLowFPS:F1}", fpsStyle, GUILayout.Width(250));

            // Jitter
            GUILayout.Label($"<b>{ProfilerI18n.Get("metric_jitter")}:</b> <color={jitterColor}><b>{jitter:F2} ms</b></color>", headerStyle, GUILayout.Width(180));

            // Total Frame
            GUILayout.Label($"<b>{ProfilerI18n.Get("metric_total_frame")}:</b> {ProfilerData.SmoothTotalFrameMs:F1} ms", headerStyle);

            GUILayout.EndHorizontal();
        }

        private void DrawToolbar()
        {
            GUILayout.BeginHorizontal();

            // Toggle profiling
            bool newEnabled = GUILayout.Toggle(ProfilerData.IsEnabled, $" {ProfilerI18n.Get(ProfilerData.IsEnabled ? "enabled" : "disabled")}", GUILayout.Width(140));
            if (newEnabled != ProfilerData.IsEnabled)
            {
                ProfilerData.IsEnabled = newEnabled;
            }

            // Reset peak
            if (GUILayout.Button(ProfilerI18n.Get("reset_peak"), GUILayout.Width(110)))
            {
                ProfilerData.ResetAllPeakData();
            }

            // Export Report
            if (GUILayout.Button(ProfilerI18n.Get("export_dump"), GUILayout.Width(140)))
            {
                PhysProfilerPlugin.Instance.ExportReportToFile();
            }

            // Language Switcher Button
            if (GUILayout.Button(ProfilerI18n.GetCurrentLanguageButtonText(), GUILayout.Width(150)))
            {
                ProfilerI18n.ToggleNextLanguage();
            }

            // Mini HUD Toggle Button
            if (GUILayout.Button($"🗖 {ProfilerI18n.Get("mode_mini")}", GUILayout.Width(110)))
            {
                IsMiniHud = true;
            }

            GUILayout.FlexibleSpace();

            // Patched counters info
            GUILayout.Label(string.Format(ProfilerI18n.Get("patched_stats"), HarmonyPatches.PatchedModuleCount, HarmonyPatches.PatchedPluginCount), tipStyle);

            // Close button
            if (GUILayout.Button("✕", GUILayout.Width(28), GUILayout.Height(22)))
            {
                PhysProfilerPlugin.Instance.SetUIVisibility(false);
            }

            GUILayout.EndHorizontal();
        }

        private void DrawBottleneckDiagnosticCard()
        {
            var diag = GetCachedDiagnostic();

            GUILayout.BeginVertical(cardAccentStyle);

            GUILayout.BeginHorizontal();
            GUILayout.Label($"<b>{ProfilerI18n.Get("bn_title")}:</b> <color={diag.StatusColorHex}><b>{diag.Title}</b></color>", headerStyle);
            GUILayout.FlexibleSpace();

            if (GUILayout.Button(showDetailedAdvice ? "▲ " + (ProfilerI18n.IsChinese ? "收起建议" : "Hide Advice") : "▼ " + (ProfilerI18n.IsChinese ? "查看建议" : "Show Advice"), GUILayout.Width(100)))
            {
                showDetailedAdvice = !showDetailedAdvice;
            }
            GUILayout.EndHorizontal();

            GUILayout.Label(diag.Description, tipStyle);

            // Top offenders tags
            if (diag.TopCulprits != null && diag.TopCulprits.Count > 0)
            {
                GUILayout.BeginHorizontal();
                GUILayout.Label($"<b>{ProfilerI18n.Get("bn_top_culprits")}</b>", tipStyle, GUILayout.Width(180));

                for (int i = 0; i < diag.TopCulprits.Count; i++)
                {
                    var c = diag.TopCulprits[i];
                    string colorStr = c.PctOfFrame > 20.0 ? "#FF5555" : (c.PctOfFrame > 10.0 ? "#FFAA22" : "#55FF88");
                    string badgeText = $"#{i + 1} [{c.SourceType}] <b>{c.Name}</b> (<color={colorStr}>{c.SmoothMs:F2}ms, {c.PctOfFrame:F1}%</color>)";
                    GUILayout.Label(badgeText, tipStyle);
                    GUILayout.Space(8);
                }

                GUILayout.FlexibleSpace();
                GUILayout.EndHorizontal();
            }

            if (showDetailedAdvice && !string.IsNullOrEmpty(diag.Advice))
            {
                GUILayout.BeginHorizontal("box");
                GUILayout.Label($"💡 <b>{(ProfilerI18n.IsChinese ? "优化建议" : "Optimization Advice")}:</b> {diag.Advice}", tipStyle);
                GUILayout.EndHorizontal();
            }

            GUILayout.EndVertical();
        }

        private void DrawMacroBudgetBar()
        {
            double totalMs = Math.Max(0.001, ProfilerData.SmoothTotalFrameMs);
            double modMs = ProfilerData.SmoothModuleScriptMs;
            double pluginMs = ProfilerData.SmoothPluginScriptMs;
            double physxMs = ProfilerData.SmoothPhysXJointsMs;
            double gpuMs = ProfilerData.SmoothRenderAndGpuMs;
            double overheadMs = Math.Max(0.0, totalMs - modMs - pluginMs - physxMs - gpuMs);

            float pMod = (float)(modMs / totalMs);
            float pPlugin = (float)(pluginMs / totalMs);
            float pPhysx = (float)(physxMs / totalMs);
            float pGpu = (float)(gpuMs / totalMs);
            float pOverhead = (float)(overheadMs / totalMs);

            GUILayout.BeginVertical(cardStyle);

            // Bar drawing
            Rect barRect = GUILayoutUtility.GetRect(960, 14);
            GUI.Box(barRect, "");

            float currentX = barRect.x + 2;
            float barY = barRect.y + 2;
            float totalBarW = barRect.width - 4;
            float barH = barRect.height - 4;

            // Colors
            Color colMod = new Color(0f, 0.82f, 0.83f, 0.95f);       // Cyan
            Color colPlugin = new Color(0.63f, 0.61f, 0.99f, 0.95f);  // Violet
            Color colPhysx = new Color(1f, 0.62f, 0.26f, 0.95f);     // Orange
            Color colGpu = new Color(1f, 0.8f, 0.34f, 0.95f);        // Gold
            Color colOverhead = new Color(0.51f, 0.58f, 0.65f, 0.8f); // Slate

            DrawBarSegment(ref currentX, barY, totalBarW * pMod, barH, colMod);
            DrawBarSegment(ref currentX, barY, totalBarW * pPlugin, barH, colPlugin);
            DrawBarSegment(ref currentX, barY, totalBarW * pPhysx, barH, colPhysx);
            DrawBarSegment(ref currentX, barY, totalBarW * pGpu, barH, colGpu);
            DrawBarSegment(ref currentX, barY, totalBarW * pOverhead, barH, colOverhead);

            GUI.color = Color.white;

            // Interactive Legend
            GUILayout.BeginHorizontal();
            GUILayout.Label($"<color=#00d2d3>■</color> <b>{ProfilerI18n.Get("legend_modules")}:</b> {modMs:F1}ms ({(pMod * 100f):F1}%)", tipStyle);
            GUILayout.Label($"<color=#a29bfe>■</color> <b>{ProfilerI18n.Get("legend_plugins")}:</b> {pluginMs:F1}ms ({(pPlugin * 100f):F1}%)", tipStyle);
            GUILayout.Label($"<color=#ff9f43>■</color> <b>{ProfilerI18n.Get("legend_physx")}:</b> {physxMs:F1}ms ({(pPhysx * 100f):F1}%)", tipStyle);
            GUILayout.Label($"<color=#feca57>■</color> <b>{ProfilerI18n.Get("legend_gpu")}:</b> {gpuMs:F1}ms ({(pGpu * 100f):F1}%)", tipStyle);
            GUILayout.Label($"<color=#8395a7>■</color> <b>{ProfilerI18n.Get("legend_overhead")}:</b> {overheadMs:F1}ms ({(pOverhead * 100f):F1}%)", tipStyle);
            GUILayout.EndHorizontal();

            GUILayout.EndVertical();
        }

        private void DrawBarSegment(ref float currentX, float y, float width, float height, Color color)
        {
            if (width < 0.5f) return;
            GUI.color = color;
            GUI.DrawTexture(new Rect(currentX, y, width, height), whitePixelTex);
            currentX += width;
        }

        #endregion

        #region Tab 0: FPS & Frame Time Graph

        private void DrawFpsGraphTab()
        {
            GUILayout.BeginVertical(cardStyle);
            GUILayout.Label($"<b>{ProfilerI18n.Get("graph_title")}</b>", headerStyle);

            Rect graphRect = GUILayoutUtility.GetRect(960, 180);
            GUI.Box(graphRect, "");

            double[] history = ProfilerData.GetFrameHistorySample(100);

            if (history != null && history.Length > 1)
            {
                float maxMs = 80f; // Scale graph up to 80ms
                float innerW = graphRect.width - 20;
                float innerH = graphRect.height - 20;
                float innerX = graphRect.x + 10;
                float innerY = graphRect.y + 10;

                // Draw Reference Guidelines (60 FPS = 16.6ms, 30 FPS = 33.3ms)
                float y60 = innerY + innerH - ((16.67f / maxMs) * innerH);
                float y30 = innerY + innerH - ((33.33f / maxMs) * innerH);

                // 60 FPS Target Line (Green)
                GUI.color = new Color(0.2f, 0.9f, 0.3f, 0.35f);
                GUI.DrawTexture(new Rect(innerX, y60, innerW, 1.5f), whitePixelTex);

                // 30 FPS Target Line (Orange)
                GUI.color = new Color(1f, 0.6f, 0.1f, 0.35f);
                GUI.DrawTexture(new Rect(innerX, y30, innerW, 1.5f), whitePixelTex);

                // Bars
                int spikeCount = 0;
                for (int i = 0; i < history.Length; i++)
                {
                    float ms = (float)history[i];
                    if (ms > 50f) spikeCount++;

                    float barHeight = Mathf.Clamp((ms / maxMs) * innerH, 2f, innerH);
                    float barWidth = innerW / history.Length;
                    float xPos = innerX + (i * barWidth);
                    float yPos = innerY + innerH - barHeight;

                    Color barColor = ms > 50f ? new Color(1f, 0.25f, 0.25f, 0.95f) :
                                     (ms > 33.3f ? new Color(1f, 0.75f, 0.15f, 0.95f) :
                                     new Color(0.2f, 0.9f, 0.35f, 0.85f));

                    GUI.color = barColor;
                    GUI.DrawTexture(new Rect(xPos, yPos, Math.Max(1.5f, barWidth - 1f), barHeight), whitePixelTex);
                }
                GUI.color = Color.white;
            }

            // Legend & Guidelines
            GUILayout.BeginHorizontal();
            GUILayout.Label($"<color=#33ff55>―</color> {ProfilerI18n.Get("graph_baseline_60")}", tipStyle);
            GUILayout.Label($"<color=#ffaa22>―</color> {ProfilerI18n.Get("graph_baseline_30")}", tipStyle);
            GUILayout.Space(20);
            GUILayout.Label(ProfilerI18n.Get("graph_legend_green"), tipStyle);
            GUILayout.Label(ProfilerI18n.Get("graph_legend_yellow"), tipStyle);
            GUILayout.Label(ProfilerI18n.Get("graph_legend_red"), tipStyle);
            GUILayout.EndHorizontal();

            GUILayout.Space(6);

            // Phase Breakdown Sub-card
            GUILayout.Label($"<b>{ProfilerI18n.Get("graph_phase_metrics")}</b>", headerStyle);
            GUILayout.BeginHorizontal("box");
            GUILayout.Label($"<b>{ProfilerI18n.Get("metric_physx")}:</b> {ProfilerData.SmoothPhysicsStepMs:F2} ms", tipStyle, GUILayout.Width(190));
            GUILayout.Label($"<b>{ProfilerI18n.Get("metric_camera")}:</b> {ProfilerData.CameraRenderMs:F2} ms", tipStyle, GUILayout.Width(190));
            GUILayout.Label($"<b>{ProfilerI18n.Get("metric_modules")}:</b> {ProfilerData.SmoothModuleScriptMs:F2} ms", tipStyle, GUILayout.Width(190));
            GUILayout.Label($"<b>{ProfilerI18n.Get("metric_plugins")}:</b> {ProfilerData.SmoothPluginScriptMs:F2} ms", tipStyle, GUILayout.Width(190));
            if (ProfilerData.IsTUFXDetected)
            {
                GUILayout.Label($"<b><color=#00e5ff>TUFX:</color></b> {ProfilerData.TUFXPostProcessMs:F2} ms", tipStyle, GUILayout.Width(180));
            }
            GUILayout.EndHorizontal();

            GUILayout.EndVertical();
        }

        #endregion

        #region Tab 1: Global Plugins Table

        private void DrawPluginsTab()
        {
            GUILayout.BeginVertical(cardStyle);

            if (ProfilerData.IsTUFXDetected)
            {
                GUILayout.BeginHorizontal("box");
                GUILayout.Label($"<b><color=#00e5ff>[TUFX]</color> {ProfilerI18n.Get("tufx_status_header")}:</b> {ProfilerI18n.Get("tufx_active_passes")}: <color=yellow>{ProfilerData.TUFXActivePassCount}</color> | {ProfilerI18n.Get("tufx_post_process_ms")}: <color=#00e5ff>{ProfilerData.TUFXPostProcessMs:F2} ms</color> | {ProfilerI18n.Get("tufx_cpu_build_ms")}: <color=lime>{ProfilerData.TUFXCpuBuildMs:F2} ms</color>", tipStyle);
                GUILayout.EndHorizontal();
                GUILayout.Space(2);
            }

            // Search Bar
            GUILayout.BeginHorizontal();
            GUILayout.Label(ProfilerI18n.Get("search_placeholder"), GUILayout.Width(150));
            searchPlugins = GUILayout.TextField(searchPlugins, GUILayout.Width(260));
            if (!string.IsNullOrEmpty(searchPlugins) && GUILayout.Button(ProfilerI18n.Get("clear_search"), GUILayout.Width(60)))
            {
                searchPlugins = "";
            }
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();

            GUILayout.Space(4);

            // Table Header with Sortable Buttons
            GUILayout.BeginHorizontal();
            if (GUILayout.Button(GetHeaderTitle("col_plugin_class", 0, sortPluginsCol, sortPluginsAsc), tableHeaderBtnStyle, GUILayout.Width(350)))
            {
                ToggleSort(0, ref sortPluginsCol, ref sortPluginsAsc);
            }
            if (GUILayout.Button(GetHeaderTitle("col_avg_ms", 1, sortPluginsCol, sortPluginsAsc), tableHeaderBtnStyle, GUILayout.Width(110)))
            {
                ToggleSort(1, ref sortPluginsCol, ref sortPluginsAsc);
            }
            if (GUILayout.Button(GetHeaderTitle("col_peak_ms", 2, sortPluginsCol, sortPluginsAsc), tableHeaderBtnStyle, GUILayout.Width(100)))
            {
                ToggleSort(2, ref sortPluginsCol, ref sortPluginsAsc);
            }
            if (GUILayout.Button(GetHeaderTitle("col_calls", 3, sortPluginsCol, sortPluginsAsc), tableHeaderBtnStyle, GUILayout.Width(100)))
            {
                ToggleSort(3, ref sortPluginsCol, ref sortPluginsAsc);
            }
            if (GUILayout.Button(GetHeaderTitle("col_pct_frame", 1, sortPluginsCol, sortPluginsAsc), tableHeaderBtnStyle, GUILayout.Width(110)))
            {
                ToggleSort(1, ref sortPluginsCol, ref sortPluginsAsc);
            }
            if (GUILayout.Button(GetHeaderTitle("col_assembly", 4, sortPluginsCol, sortPluginsAsc), tableHeaderBtnStyle, GUILayout.Width(170)))
            {
                ToggleSort(4, ref sortPluginsCol, ref sortPluginsAsc);
            }
            GUILayout.EndHorizontal();

            scrollPosPlugins = GUILayout.BeginScrollView(scrollPosPlugins);

            float now = Time.realtimeSinceStartup;
            bool forceRefresh = cachedTopPlugins.Count == 0 ||
                                now - lastTableUpdateTime >= UI_THROTTLE_INTERVAL ||
                                searchPlugins != lastSearchPlugins ||
                                sortPluginsCol != lastSortPluginsCol ||
                                sortPluginsAsc != lastSortPluginsAsc;

            if (forceRefresh)
            {
                cachedTopPlugins = ProfilerData.GetTopPlugins(60, searchPlugins, sortPluginsCol, sortPluginsAsc);
                lastSearchPlugins = searchPlugins;
                lastSortPluginsCol = sortPluginsCol;
                lastSortPluginsAsc = sortPluginsAsc;
                lastTableUpdateTime = now;
            }

            var topPlugins = cachedTopPlugins;
            double totalFrameMs = Math.Max(0.001, ProfilerData.SmoothTotalFrameMs);

            for (int i = 0; i < topPlugins.Count; i++)
            {
                ModuleStats p = topPlugins[i];
                double pct = (p.SmoothMs / totalFrameMs) * 100.0;
                string colorStr = p.SmoothMs > 4.0 ? "#FF4444" : (p.SmoothMs > 1.2 ? "#FFAA22" : "#FFFFFF");
                string displayTitle = p.IsTUFX
                    ? $"<color=#00e5ff>[TUFX]</color> <color={colorStr}>{p.FriendlyName}</color>"
                    : $"<color={colorStr}>{p.TypeName}</color>";

                string typeKey = "tab1::" + p.TypeName;
                bool isExp = expandedTypes.Contains(typeKey);
                string expIcon = p.Methods.Count > 0 ? (isExp ? "▼ " : "▶ ") : "  ";

                GUILayout.BeginHorizontal(i % 2 == 0 ? "box" : GUIStyle.none);
                if (GUILayout.Button(expIcon + displayTitle, headerStyle, GUILayout.Width(350)))
                {
                    if (p.Methods.Count > 0)
                    {
                        if (isExp) expandedTypes.Remove(typeKey);
                        else expandedTypes.Add(typeKey);
                    }
                }
                GUILayout.Label($"{p.SmoothMs:F3} ms", GUILayout.Width(110));
                GUILayout.Label($"{p.PeakMs:F2} ms", GUILayout.Width(100));
                GUILayout.Label($"{p.CurrentFrameCalls}", GUILayout.Width(100));
                GUILayout.Label($"{pct:F1}%", GUILayout.Width(110));
                GUILayout.Label($"{p.AssemblyName}", GUILayout.Width(170));
                GUILayout.EndHorizontal();

                if (isExp && p.Methods.Count > 0)
                {
                    foreach (var meth in p.GetSortedMethods())
                    {
                        if (meth.SmoothMs < 0.0005 && meth.CurrentFrameCalls == 0) continue;
                        string mColor = meth.SmoothMs > 2.0 ? "#FF5555" : (meth.SmoothMs > 0.5 ? "#FFBB33" : "#88BBDD");
                        GUILayout.BeginHorizontal();
                        GUILayout.Space(24);
                        GUILayout.Label($"<color={mColor}>· {meth.MethodName}()</color>", tipStyle, GUILayout.Width(326));
                        GUILayout.Label($"{meth.SmoothMs:F3} ms", GUILayout.Width(110));
                        GUILayout.Label($"{meth.PeakMs:F2} ms", GUILayout.Width(100));
                        GUILayout.Label($"{meth.CurrentFrameCalls}", GUILayout.Width(100));
                        GUILayout.EndHorizontal();
                    }
                }
            }

            if (topPlugins.Count == 0)
            {
                GUILayout.Label(ProfilerI18n.Get("no_data_plugins"));
            }

            GUILayout.EndScrollView();
            GUILayout.EndVertical();
        }

        #endregion

        #region Tab 2: PartModules Table

        private void DrawModulesTab()
        {
            GUILayout.BeginVertical(cardStyle);

            // Search Bar
            GUILayout.BeginHorizontal();
            GUILayout.Label(ProfilerI18n.Get("search_placeholder"), GUILayout.Width(150));
            searchModules = GUILayout.TextField(searchModules, GUILayout.Width(260));
            if (!string.IsNullOrEmpty(searchModules) && GUILayout.Button(ProfilerI18n.Get("clear_search"), GUILayout.Width(60)))
            {
                searchModules = "";
            }
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();

            GUILayout.Space(4);

            // Table Header with Sortable Buttons
            GUILayout.BeginHorizontal();
            if (GUILayout.Button(GetHeaderTitle("col_module_type", 0, sortModulesCol, sortModulesAsc), tableHeaderBtnStyle, GUILayout.Width(350)))
            {
                ToggleSort(0, ref sortModulesCol, ref sortModulesAsc);
            }
            if (GUILayout.Button(GetHeaderTitle("col_avg_ms", 1, sortModulesCol, sortModulesAsc), tableHeaderBtnStyle, GUILayout.Width(110)))
            {
                ToggleSort(1, ref sortModulesCol, ref sortModulesAsc);
            }
            if (GUILayout.Button(GetHeaderTitle("col_peak_ms", 2, sortModulesCol, sortModulesAsc), tableHeaderBtnStyle, GUILayout.Width(100)))
            {
                ToggleSort(2, ref sortModulesCol, ref sortModulesAsc);
            }
            if (GUILayout.Button(GetHeaderTitle("col_calls", 3, sortModulesCol, sortModulesAsc), tableHeaderBtnStyle, GUILayout.Width(100)))
            {
                ToggleSort(3, ref sortModulesCol, ref sortModulesAsc);
            }
            if (GUILayout.Button(GetHeaderTitle("col_pct_frame", 1, sortModulesCol, sortModulesAsc), tableHeaderBtnStyle, GUILayout.Width(110)))
            {
                ToggleSort(1, ref sortModulesCol, ref sortModulesAsc);
            }
            if (GUILayout.Button(GetHeaderTitle("col_assembly", 4, sortModulesCol, sortModulesAsc), tableHeaderBtnStyle, GUILayout.Width(170)))
            {
                ToggleSort(4, ref sortModulesCol, ref sortModulesAsc);
            }
            GUILayout.EndHorizontal();

            scrollPosModules = GUILayout.BeginScrollView(scrollPosModules);

            float now = Time.realtimeSinceStartup;
            bool forceRefresh = cachedTopModules.Count == 0 ||
                                now - lastTableUpdateTime >= UI_THROTTLE_INTERVAL ||
                                searchModules != lastSearchModules ||
                                sortModulesCol != lastSortModulesCol ||
                                sortModulesAsc != lastSortModulesAsc;

            if (forceRefresh)
            {
                cachedTopModules = ProfilerData.GetTopModules(60, searchModules, sortModulesCol, sortModulesAsc);
                lastSearchModules = searchModules;
                lastSortModulesCol = sortModulesCol;
                lastSortModulesAsc = sortModulesAsc;
                lastTableUpdateTime = now;
            }

            var topModules = cachedTopModules;
            double totalFrameMs = Math.Max(0.001, ProfilerData.SmoothTotalFrameMs);

            for (int i = 0; i < topModules.Count; i++)
            {
                ModuleStats m = topModules[i];
                double pct = (m.SmoothMs / totalFrameMs) * 100.0;
                string colorStr = m.SmoothMs > 4.0 ? "#FF4444" : (m.SmoothMs > 1.2 ? "#FFAA22" : "#FFFFFF");

                string typeKey = "tab2::" + m.TypeName;
                bool isExp = expandedTypes.Contains(typeKey);
                string expIcon = m.Methods.Count > 0 ? (isExp ? "▼ " : "▶ ") : "  ";

                GUILayout.BeginHorizontal(i % 2 == 0 ? "box" : GUIStyle.none);
                if (GUILayout.Button($"{expIcon}<color={colorStr}>{m.TypeName}</color>", headerStyle, GUILayout.Width(350)))
                {
                    if (m.Methods.Count > 0)
                    {
                        if (isExp) expandedTypes.Remove(typeKey);
                        else expandedTypes.Add(typeKey);
                    }
                }
                GUILayout.Label($"{m.SmoothMs:F3} ms", GUILayout.Width(110));
                GUILayout.Label($"{m.PeakMs:F2} ms", GUILayout.Width(100));
                GUILayout.Label($"{m.CurrentFrameCalls}", GUILayout.Width(100));
                GUILayout.Label($"{pct:F1}%", GUILayout.Width(110));
                GUILayout.Label($"{m.AssemblyName}", GUILayout.Width(170));
                GUILayout.EndHorizontal();

                if (isExp && m.Methods.Count > 0)
                {
                    foreach (var meth in m.GetSortedMethods())
                    {
                        if (meth.SmoothMs < 0.0005 && meth.CurrentFrameCalls == 0) continue;
                        string mColor = meth.SmoothMs > 2.0 ? "#FF5555" : (meth.SmoothMs > 0.5 ? "#FFBB33" : "#88BBDD");
                        GUILayout.BeginHorizontal();
                        GUILayout.Space(24);
                        GUILayout.Label($"<color={mColor}>· {meth.MethodName}()</color>", tipStyle, GUILayout.Width(326));
                        GUILayout.Label($"{meth.SmoothMs:F3} ms", GUILayout.Width(110));
                        GUILayout.Label($"{meth.PeakMs:F2} ms", GUILayout.Width(100));
                        GUILayout.Label($"{meth.CurrentFrameCalls}", GUILayout.Width(100));
                        GUILayout.EndHorizontal();
                    }
                }
            }

            if (topModules.Count == 0)
            {
                GUILayout.Label(ProfilerI18n.Get("no_data_modules"));
            }

            GUILayout.EndScrollView();
            GUILayout.EndVertical();
        }

        #endregion

        #region Tab 3: Parts & Vessels Table

        private void DrawPartsTab()
        {
            GUILayout.BeginVertical(cardStyle);

            // Search Bar
            GUILayout.BeginHorizontal();
            GUILayout.Label(ProfilerI18n.Get("search_placeholder"), GUILayout.Width(150));
            searchParts = GUILayout.TextField(searchParts, GUILayout.Width(260));
            if (!string.IsNullOrEmpty(searchParts) && GUILayout.Button(ProfilerI18n.Get("clear_search"), GUILayout.Width(60)))
            {
                searchParts = "";
            }
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();

            GUILayout.Space(4);

            // Table Header with Sortable Buttons
            GUILayout.BeginHorizontal();
            if (GUILayout.Button(GetHeaderTitle("col_part_title", 0, sortPartsCol, sortPartsAsc), tableHeaderBtnStyle, GUILayout.Width(460)))
            {
                ToggleSort(0, ref sortPartsCol, ref sortPartsAsc);
            }
            if (GUILayout.Button(GetHeaderTitle("col_vessel_name", 1, sortPartsCol, sortPartsAsc), tableHeaderBtnStyle, GUILayout.Width(320)))
            {
                ToggleSort(1, ref sortPartsCol, ref sortPartsAsc);
            }
            if (GUILayout.Button(GetHeaderTitle("col_avg_ms", 2, sortPartsCol, sortPartsAsc), tableHeaderBtnStyle, GUILayout.Width(160)))
            {
                ToggleSort(2, ref sortPartsCol, ref sortPartsAsc);
            }
            GUILayout.EndHorizontal();

            scrollPosParts = GUILayout.BeginScrollView(scrollPosParts);

            float now = Time.realtimeSinceStartup;
            bool forceRefresh = cachedTopParts.Count == 0 ||
                                now - lastTableUpdateTime >= UI_THROTTLE_INTERVAL ||
                                searchParts != lastSearchParts ||
                                sortPartsCol != lastSortPartsCol ||
                                sortPartsAsc != lastSortPartsAsc;

            if (forceRefresh)
            {
                cachedTopParts = ProfilerData.GetTopParts(50, searchParts, sortPartsCol, sortPartsAsc);
                lastSearchParts = searchParts;
                lastSortPartsCol = sortPartsCol;
                lastSortPartsAsc = sortPartsAsc;
                lastTableUpdateTime = now;
            }

            var topParts = cachedTopParts;

            for (int i = 0; i < topParts.Count; i++)
            {
                PartStats p = topParts[i];
                string colorStr = p.SmoothMs > 2.0 ? "#FF4444" : (p.SmoothMs > 0.8 ? "#FFAA22" : "#FFFFFF");

                GUILayout.BeginHorizontal(i % 2 == 0 ? "box" : GUIStyle.none);
                GUILayout.Label($"<color={colorStr}>{p.PartTitle}</color>", headerStyle, GUILayout.Width(460));
                GUILayout.Label(p.VesselName, GUILayout.Width(320));
                GUILayout.Label($"{p.SmoothMs:F3} ms", GUILayout.Width(160));
                GUILayout.EndHorizontal();
            }

            if (topParts.Count == 0)
            {
                GUILayout.Label(ProfilerI18n.Get("no_data_parts"));
            }

            GUILayout.EndScrollView();
            GUILayout.EndVertical();
        }

        #endregion

        #region Tab 4: Assembly Breakdown

        // Pre-allocated assembly bar colors
        private static readonly Color[] asmBarColors = new Color[]
        {
            new Color(0.20f, 0.80f, 0.95f, 0.95f),  // Cyan
            new Color(0.63f, 0.61f, 0.99f, 0.95f),  // Violet
            new Color(1.00f, 0.62f, 0.26f, 0.95f),  // Orange
            new Color(0.20f, 0.90f, 0.35f, 0.95f),  // Green
            new Color(1.00f, 0.80f, 0.34f, 0.95f),  // Gold
            new Color(0.96f, 0.44f, 0.63f, 0.95f),  // Pink
            new Color(0.55f, 0.84f, 0.51f, 0.95f),  // Lime
            new Color(0.51f, 0.58f, 0.65f, 0.80f),  // Slate (other)
        };

        private static readonly string[] asmBarColorHex = new string[]
        {
            "#33ccf2", "#a29bfe", "#ff9f43", "#33e659", "#ffcc57", "#f570a1", "#8cd782", "#8395a7"
        };

        private void DrawAssemblyTab()
        {
            GUILayout.BeginVertical(cardStyle);

            // Refresh cache
            float now = Time.realtimeSinceStartup;
            bool forceRefresh = cachedAssemblyStats.Count == 0 ||
                                now - lastAssemblyUpdateTime >= UI_THROTTLE_INTERVAL ||
                                searchAssembly != lastSearchAssembly ||
                                sortAssemblyCol != lastSortAssemblyCol ||
                                sortAssemblyAsc != lastSortAssemblyAsc;

            if (forceRefresh)
            {
                cachedAssemblyStats = ProfilerData.GetAssemblyBreakdown(searchAssembly, sortAssemblyCol, sortAssemblyAsc);
                lastSearchAssembly = searchAssembly;
                lastSortAssemblyCol = sortAssemblyCol;
                lastSortAssemblyAsc = sortAssemblyAsc;
                lastAssemblyUpdateTime = now;
            }

            var asmList = cachedAssemblyStats;
            double totalFrameMs = Math.Max(0.001, ProfilerData.SmoothTotalFrameMs);

            // === Assembly Distribution Bar ===
            GUILayout.Label($"<b>{ProfilerI18n.Get("asm_bar_title")}</b>", headerStyle);
            Rect barRect = GUILayoutUtility.GetRect(960, 16);
            GUI.Box(barRect, "");

            float currentX = barRect.x + 2;
            float barY = barRect.y + 2;
            float totalBarW = barRect.width - 4;
            float barH = barRect.height - 4;

            // Draw segments for top assemblies
            int segCount = Math.Min(asmList.Count, asmBarColors.Length);
            double drawnMs = 0;
            for (int i = 0; i < segCount; i++)
            {
                float pct = (float)(asmList[i].SmoothMs / totalFrameMs);
                float segW = totalBarW * pct;
                if (segW < 0.5f) continue;
                Color c = (i < asmBarColors.Length - 1) ? asmBarColors[i] : asmBarColors[asmBarColors.Length - 1];
                GUI.color = c;
                GUI.DrawTexture(new Rect(currentX, barY, segW, barH), whitePixelTex);
                currentX += segW;
                drawnMs += asmList[i].SmoothMs;
            }
            // Remaining unaccounted
            float remainPct = (float)((totalFrameMs - drawnMs) / totalFrameMs);
            if (remainPct > 0.01f)
            {
                GUI.color = new Color(0.35f, 0.35f, 0.40f, 0.6f);
                GUI.DrawTexture(new Rect(currentX, barY, totalBarW * remainPct, barH), whitePixelTex);
            }
            GUI.color = Color.white;

            // Bar legend
            GUILayout.BeginHorizontal();
            for (int i = 0; i < Math.Min(asmList.Count, 7); i++)
            {
                string hex = (i < asmBarColorHex.Length) ? asmBarColorHex[i] : asmBarColorHex[asmBarColorHex.Length - 1];
                string shortName = asmList[i].AssemblyName;
                if (shortName.Length > 18) shortName = shortName.Substring(0, 15) + "...";
                GUILayout.Label($"<color={hex}>■</color> <b>{shortName}:</b> {asmList[i].SmoothMs:F1}ms ({asmList[i].PctOfFrame:F1}%)", tipStyle);
            }
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();

            GUILayout.Space(4);

            // === Search Bar ===
            GUILayout.BeginHorizontal();
            GUILayout.Label(ProfilerI18n.Get("search_placeholder"), GUILayout.Width(150));
            searchAssembly = GUILayout.TextField(searchAssembly, GUILayout.Width(240));
            if (!string.IsNullOrEmpty(searchAssembly) && GUILayout.Button(ProfilerI18n.Get("clear_search"), GUILayout.Width(60)))
            {
                searchAssembly = "";
            }
            GUILayout.Space(10);
            if (GUILayout.Button(groupBySubsystems ? ProfilerI18n.Get("subsystem_toggle_on") : ProfilerI18n.Get("subsystem_toggle_off"), GUILayout.Width(170)))
            {
                groupBySubsystems = !groupBySubsystems;
            }
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();

            GUILayout.Space(4);

            // === Table Header ===
            GUILayout.BeginHorizontal();
            if (GUILayout.Button(GetHeaderTitle("col_assembly_name", 0, sortAssemblyCol, sortAssemblyAsc), tableHeaderBtnStyle, GUILayout.Width(240)))
            {
                ToggleSort(0, ref sortAssemblyCol, ref sortAssemblyAsc);
            }
            if (GUILayout.Button(GetHeaderTitle("col_avg_ms", 1, sortAssemblyCol, sortAssemblyAsc), tableHeaderBtnStyle, GUILayout.Width(110)))
            {
                ToggleSort(1, ref sortAssemblyCol, ref sortAssemblyAsc);
            }
            if (GUILayout.Button(GetHeaderTitle("col_peak_ms", 2, sortAssemblyCol, sortAssemblyAsc), tableHeaderBtnStyle, GUILayout.Width(100)))
            {
                ToggleSort(2, ref sortAssemblyCol, ref sortAssemblyAsc);
            }
            if (GUILayout.Button(GetHeaderTitle("col_active_types", 3, sortAssemblyCol, sortAssemblyAsc), tableHeaderBtnStyle, GUILayout.Width(100)))
            {
                ToggleSort(3, ref sortAssemblyCol, ref sortAssemblyAsc);
            }
            if (GUILayout.Button(GetHeaderTitle("col_total_calls", 4, sortAssemblyCol, sortAssemblyAsc), tableHeaderBtnStyle, GUILayout.Width(100)))
            {
                ToggleSort(4, ref sortAssemblyCol, ref sortAssemblyAsc);
            }
            if (GUILayout.Button(GetHeaderTitle("col_pct_frame", 5, sortAssemblyCol, sortAssemblyAsc), tableHeaderBtnStyle, GUILayout.Width(100)))
            {
                ToggleSort(5, ref sortAssemblyCol, ref sortAssemblyAsc);
            }
            GUILayout.EndHorizontal();

            // === Scrollable Assembly Table ===
            scrollPosAssembly = GUILayout.BeginScrollView(scrollPosAssembly);

            for (int i = 0; i < asmList.Count; i++)
            {
                var asm = asmList[i];
                string colorStr = asm.SmoothMs > 5.0 ? "#FF4444" : (asm.SmoothMs > 1.5 ? "#FFAA22" : "#FFFFFF");
                bool isExpanded = expandedAssemblies.Contains(asm.AssemblyName);

                string expandIcon = isExpanded ? "▼" : "▶";

                GUILayout.BeginHorizontal(i % 2 == 0 ? "box" : GUIStyle.none);

                // Clickable expand/collapse assembly name
                if (GUILayout.Button($"{expandIcon} <color={colorStr}><b>{asm.AssemblyName}</b></color>", headerStyle, GUILayout.Width(240)))
                {
                    if (isExpanded)
                        expandedAssemblies.Remove(asm.AssemblyName);
                    else
                        expandedAssemblies.Add(asm.AssemblyName);
                }
                GUILayout.Label($"{asm.SmoothMs:F3} ms", GUILayout.Width(110));
                GUILayout.Label($"{asm.PeakMs:F2} ms", GUILayout.Width(100));
                GUILayout.Label($"{asm.ActiveTypeCount}", GUILayout.Width(100));
                GUILayout.Label($"{asm.TotalCalls}", GUILayout.Width(100));
                GUILayout.Label($"{asm.PctOfFrame:F1}%", GUILayout.Width(100));
                GUILayout.EndHorizontal();

                // === Expanded: Namespace sub-rows ===
                if (isExpanded && asm.Namespaces != null)
                {
                    for (int j = 0; j < asm.Namespaces.Count; j++)
                    {
                        var ns = asm.Namespaces[j];
                        if (ns.SmoothMs < 0.001 && ns.TotalCalls == 0) continue;

                        string nsColorStr = ns.SmoothMs > 3.0 ? "#FF6666" : (ns.SmoothMs > 0.8 ? "#FFCC44" : "#AACCFF");
                        string nsKey = asm.AssemblyName + "::" + ns.Namespace;
                        bool nsExpanded = expandedNamespaces.Contains(nsKey);
                        string nsIcon = nsExpanded ? "  ▼" : "  ▶";

                        GUILayout.BeginHorizontal();
                        GUILayout.Space(20);

                        // Clickable namespace row
                        string nsDisplayName = ns.Namespace;
                        if (nsDisplayName.Length > 30) nsDisplayName = nsDisplayName.Substring(0, 27) + "...";

                        if (GUILayout.Button($"{nsIcon} <color={nsColorStr}>{nsDisplayName}</color>", tipStyle, GUILayout.Width(220)))
                        {
                            if (nsExpanded)
                                expandedNamespaces.Remove(nsKey);
                            else
                                expandedNamespaces.Add(nsKey);
                        }
                        GUILayout.Label($"{ns.SmoothMs:F3} ms", GUILayout.Width(110));
                        GUILayout.Label($"{ns.PeakMs:F2} ms", GUILayout.Width(100));
                        GUILayout.Label($"{ns.ActiveTypeCount}", GUILayout.Width(100));
                        GUILayout.Label($"{ns.TotalCalls}", GUILayout.Width(100));
                        GUILayout.Label($"{ns.PctOfFrame:F1}%", GUILayout.Width(100));
                        GUILayout.EndHorizontal();

                        if (nsExpanded)
                        {
                            // Subsystem grouping for <global>
                            if (ns.Namespace == "<global>" && groupBySubsystems && ns.Subsystems != null && ns.Subsystems.Count > 0)
                            {
                                for (int sIdx = 0; sIdx < ns.Subsystems.Count; sIdx++)
                                {
                                    var sub = ns.Subsystems[sIdx];
                                    if (sub.SmoothMs < 0.0005 && sub.TotalCalls == 0) continue;

                                    string subKey = nsKey + "::" + sub.SubsystemId;
                                    bool subExp = expandedSubsystems.Contains(subKey);
                                    string subIcon = subExp ? "    ▼ " : "    ▶ ";
                                    string subColor = sub.SmoothMs > 2.0 ? "#FF8888" : (sub.SmoothMs > 0.5 ? "#FFDD66" : "#CCDDEE");

                                    GUILayout.BeginHorizontal();
                                    GUILayout.Space(36);
                                    if (GUILayout.Button($"<color={subColor}><b>{subIcon}{sub.DisplayName}</b></color>", tipStyle, GUILayout.Width(204)))
                                    {
                                        if (subExp) expandedSubsystems.Remove(subKey);
                                        else expandedSubsystems.Add(subKey);
                                    }
                                    GUILayout.Label($"{sub.SmoothMs:F3} ms", GUILayout.Width(110));
                                    GUILayout.Label($"{sub.PeakMs:F2} ms", GUILayout.Width(100));
                                    GUILayout.Label($"{sub.ActiveTypeCount}", GUILayout.Width(100));
                                    GUILayout.Label($"{sub.TotalCalls}", GUILayout.Width(100));
                                    GUILayout.Label($"{sub.PctOfFrame:F1}%", GUILayout.Width(100));
                                    GUILayout.EndHorizontal();

                                    if (subExp && sub.Types != null)
                                    {
                                        for (int k = 0; k < sub.Types.Count; k++)
                                        {
                                            DrawTypeRowWithMethods(sub.Types[k], 54, subKey);
                                        }
                                    }
                                }
                            }
                            else if (ns.Types != null)
                            {
                                for (int k = 0; k < ns.Types.Count; k++)
                                {
                                    DrawTypeRowWithMethods(ns.Types[k], 40, nsKey);
                                }
                            }
                        }
                    }
                }
            }

            if (asmList.Count == 0)
            {
                GUILayout.Label(ProfilerI18n.Get("no_data_assembly"));
            }

            GUILayout.EndScrollView();
            GUILayout.EndVertical();
        }

        private void DrawTypeRowWithMethods(ModuleStats t, int indent, string parentKey)
        {
            if (t.SmoothMs < 0.0005 && t.CurrentFrameCalls == 0) return;

            string typeKey = parentKey + "::" + t.TypeName;
            bool typeExpanded = expandedTypes.Contains(typeKey);
            var methods = t.GetSortedMethods();
            bool hasMethods = methods.Count > 0;
            string typeIcon = hasMethods ? (typeExpanded ? "▼ " : "▶ ") : "· ";

            string tColorStr = t.SmoothMs > 2.0 ? "#FF5555" : (t.SmoothMs > 0.5 ? "#FFBB33" : "#88BBDD");

            GUILayout.BeginHorizontal();
            GUILayout.Space(indent);

            int nameWidth = Math.Max(120, 240 - indent);
            if (GUILayout.Button($"<color={tColorStr}>{typeIcon}{t.TypeName}</color>", tipStyle, GUILayout.Width(nameWidth)))
            {
                if (hasMethods)
                {
                    if (typeExpanded) expandedTypes.Remove(typeKey);
                    else expandedTypes.Add(typeKey);
                }
            }
            GUILayout.Label($"{t.SmoothMs:F3} ms", GUILayout.Width(110));
            GUILayout.Label($"{t.PeakMs:F2} ms", GUILayout.Width(100));
            GUILayout.Label($"{t.CurrentFrameCalls} calls", GUILayout.Width(100));
            GUILayout.EndHorizontal();

            // Expanded methods
            if (typeExpanded && hasMethods)
            {
                for (int mIdx = 0; mIdx < methods.Count; mIdx++)
                {
                    var meth = methods[mIdx];
                    if (meth.SmoothMs < 0.0005 && meth.CurrentFrameCalls == 0) continue;

                    string methKey = typeKey + "::" + meth.MethodName;
                    bool methExpanded = expandedMethods.Contains(methKey);
                    var subs = meth.GetSortedSubInvocations();
                    bool hasSubs = subs.Count > 0;
                    string methIcon = hasSubs ? (methExpanded ? "  ▼ " : "  ▶ ") : "  · ";
                    string mColor = meth.SmoothMs > 1.5 ? "#FFAA22" : "#99DDFF";

                    GUILayout.BeginHorizontal();
                    GUILayout.Space(indent + 16);

                    int methWidth = Math.Max(100, 240 - indent - 16);
                    if (GUILayout.Button($"<color={mColor}>{methIcon}{meth.MethodName}()</color>", tipStyle, GUILayout.Width(methWidth)))
                    {
                        if (hasSubs)
                        {
                            if (methExpanded) expandedMethods.Remove(methKey);
                            else expandedMethods.Add(methKey);
                        }
                    }
                    GUILayout.Label($"{meth.SmoothMs:F3} ms", GUILayout.Width(110));
                    GUILayout.Label($"{meth.PeakMs:F2} ms", GUILayout.Width(100));
                    GUILayout.Label($"{meth.CurrentFrameCalls} calls", GUILayout.Width(100));
                    GUILayout.EndHorizontal();

                    // Expanded Dispatcher Sub-Invocations (e.g. Principia inside TimingPre)
                    if (methExpanded && hasSubs)
                    {
                        for (int sIdx = 0; sIdx < subs.Count; sIdx++)
                        {
                            var sub = subs[sIdx];
                            if (sub.SmoothMs < 0.0005 && sub.CurrentFrameCalls == 0) continue;

                            string sColor = sub.SmoothMs > 1.0 ? "#FF5555" : (sub.SmoothMs > 0.3 ? "#FFAA22" : "#55FF88");

                            GUILayout.BeginHorizontal();
                            GUILayout.Space(indent + 32);

                            string subDisplay = $"⚡ <color=#00e5ff>[{sub.AssemblyName}]</color> {sub.TypeName}.{sub.MethodName}";
                            int subWidth = Math.Max(140, 360 - indent - 32);
                            GUILayout.Label($"<color={sColor}>{subDisplay}</color>", tipStyle, GUILayout.Width(subWidth));
                            GUILayout.Label($"{sub.SmoothMs:F3} ms", GUILayout.Width(110));
                            GUILayout.Label($"{sub.PeakMs:F2} ms", GUILayout.Width(100));
                            GUILayout.Label($"{sub.CurrentFrameCalls} calls", GUILayout.Width(100));
                            GUILayout.EndHorizontal();
                        }
                    }
                }
            }
        }

        #endregion

        #region Tab 5: Help & Guide Tab

        private void DrawHelpTab()
        {
            GUILayout.BeginVertical(cardStyle);
            scrollPosHelp = GUILayout.BeginScrollView(scrollPosHelp);

            GUILayout.Label($"<b>{ProfilerI18n.Get("help_h1")}</b>", headerStyle);
            GUILayout.Space(6);

            // PTR Explanation
            GUILayout.BeginVertical("box");
            GUILayout.Label($"<b>{ProfilerI18n.Get("help_ptr_q")}</b>", headerStyle);
            GUILayout.Label(ProfilerI18n.Get("help_ptr_a"), tipStyle);
            GUILayout.EndVertical();

            GUILayout.Space(4);

            // 1% Low Explanation
            GUILayout.BeginVertical("box");
            GUILayout.Label($"<b>{ProfilerI18n.Get("help_1pct_q")}</b>", headerStyle);
            GUILayout.Label(ProfilerI18n.Get("help_1pct_a"), tipStyle);
            GUILayout.EndVertical();

            GUILayout.Space(4);

            // Jitter Explanation
            GUILayout.BeginVertical("box");
            GUILayout.Label($"<b>{ProfilerI18n.Get("help_jitter_q")}</b>", headerStyle);
            GUILayout.Label(ProfilerI18n.Get("help_jitter_a"), tipStyle);
            GUILayout.EndVertical();

            GUILayout.Space(4);

            // Mod Tuning Tips
            GUILayout.BeginVertical("box");
            GUILayout.Label($"<b>{ProfilerI18n.Get("help_mod_tuning_q")}</b>", headerStyle);
            GUILayout.Label(ProfilerI18n.Get("help_mod_tuning_a"), tipStyle);
            GUILayout.EndVertical();

            GUILayout.EndScrollView();
            GUILayout.EndVertical();
        }

        #endregion

        #region Mini HUD Window Rendering

        private void DrawMiniHudWindow(int windowID)
        {
            var diag = GetCachedDiagnostic();

            GUILayout.BeginVertical();

            double ptr = ProfilerData.SmoothPTR * 100.0;
            string ptrColor = ptr >= 85.0 ? "#33FF33" : (ptr >= 60.0 ? "#FFFF33" : "#FF3333");
            double fps = ProfilerData.SmoothFPS;
            string fpsColor = fps >= 45.0 ? "#33FF33" : (fps >= 25.0 ? "#FFFF33" : "#FF3333");
            double fps1pct = ProfilerData.OnePercentLowFPS;

            // Row 1: Metrics
            GUILayout.BeginHorizontal();
            GUILayout.Label($"<b>PTR:</b> <color={ptrColor}>{ptr:F0}%</color>", miniHudStyle, GUILayout.Width(90));
            GUILayout.Label($"<b>FPS:</b> <color={fpsColor}>{fps:F1}</color>", miniHudStyle, GUILayout.Width(90));
            GUILayout.Label($"<b>1% Low:</b> {fps1pct:F0}", miniHudStyle, GUILayout.Width(90));

            if (GUILayout.Button("🗖", GUILayout.Width(26), GUILayout.Height(20)))
            {
                IsMiniHud = false;
            }
            if (GUILayout.Button("✕", GUILayout.Width(26), GUILayout.Height(20)))
            {
                PhysProfilerPlugin.Instance.SetUIVisibility(false);
            }
            GUILayout.EndHorizontal();

            // Row 2: Bottleneck Badge
            GUILayout.Label($"<b><color={diag.StatusColorHex}>{diag.Title}</color></b>", miniHudStyle);

            // Row 3: Mini Sparkline
            Rect sparkRect = GUILayoutUtility.GetRect(340, 50);
            GUI.Box(sparkRect, "");

            double[] history = ProfilerData.GetFrameHistorySample(50);
            if (history != null && history.Length > 1)
            {
                float maxMs = 70f;
                float innerW = sparkRect.width - 8;
                float innerH = sparkRect.height - 8;
                float innerX = sparkRect.x + 4;
                float innerY = sparkRect.y + 4;

                for (int i = 0; i < history.Length; i++)
                {
                    float ms = (float)history[i];
                    float barH = Mathf.Clamp((ms / maxMs) * innerH, 2f, innerH);
                    float barW = innerW / history.Length;
                    float x = innerX + (i * barW);
                    float y = innerY + innerH - barH;

                    Color barCol = ms > 50f ? new Color(1f, 0.25f, 0.25f, 0.9f) :
                                   (ms > 33.3f ? new Color(1f, 0.75f, 0.15f, 0.9f) :
                                   new Color(0.2f, 0.9f, 0.35f, 0.8f));
                    GUI.color = barCol;
                    GUI.DrawTexture(new Rect(x, y, Math.Max(1f, barW - 1f), barH), whitePixelTex);
                }
                GUI.color = Color.white;
            }

            GUILayout.EndVertical();
            GUI.DragWindow();
        }

        #endregion

        #region Helpers

        private string GetHeaderTitle(string key, int colIdx, int activeCol, bool isAsc)
        {
            string title = ProfilerI18n.Get(key);
            if (colIdx == activeCol)
            {
                return $"{title} {(isAsc ? "▲" : "▼")}";
            }
            return title;
        }

        private void ToggleSort(int colIdx, ref int targetCol, ref bool targetAsc)
        {
            if (targetCol == colIdx)
            {
                targetAsc = !targetAsc;
            }
            else
            {
                targetCol = colIdx;
                targetAsc = false;
            }
        }

        #endregion
    }
}
