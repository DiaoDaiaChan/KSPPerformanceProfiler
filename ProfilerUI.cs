using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace KSPPerformanceProfiler
{
    public class ProfilerUI : MonoBehaviour
    {
        public static bool IsVisible = false;
        public static bool IsMiniHud = false;

        private Rect fullWindowRect;
        private Rect miniWindowRect;
        private int selectedTab = 0;

        private Vector2 scrollPosGraph = Vector2.zero;
        private Vector2 scrollPosPlugins = Vector2.zero;
        private Vector2 scrollPosModules = Vector2.zero;
        private Vector2 scrollPosParts = Vector2.zero;
        private Vector2 scrollPosHelp = Vector2.zero;
        private Vector2 scrollPosAssembly = Vector2.zero;

        // Sort lock & Anti-Flicker state
        private bool isSortLocked = false;

        // Graph options
        private int graphMode = 0; // 0: Multi-layer Stacked Area, 1: FPS Curve
        private int graphTimeRange = 100; // 100, 300, 600 frames
        private int selectedSpikeIndex = -1;

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

        // UI Cache to prevent IMGUI per-frame heap allocations
        private readonly string[] cachedTabNames = new string[5];
        private readonly string[] cachedSubTabNames = new string[2];
        private int cachedTabNamesLangPack = -2;
        private readonly double[] miniSparklineBuffer = new double[50];
        private int subTabScripts = 0;

        // Window Resizing & Adaptive Layout
        private bool isResizing = false;
        private Vector2 resizeStartMouse;
        private Vector2 resizeStartSize;
        private const float MIN_WINDOW_WIDTH = 850f;
        private const float MIN_WINDOW_HEIGHT = 520f;
        private const float RESIZE_HANDLE_SIZE = 24f;
        private GUIStyle resizeGripStyle;

        // Interactive Timeline Graph Filters & Inspection
        private bool showLayerModules = true;
        private bool showLayerPlugins = true;
        private bool showLayerPhysx = true;
        private bool showLayerGpu = true;
        private bool showLayerOverhead = true;
        private int hoveredTimelineIdx = -1;
        private int lockedTimelineIdx = -1;

        // Custom Heap Padding State
        private string customPadMbInput = "";
        private string padValidationMsg = "";

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
            LoadWindowConfig();

            whitePixelTex = new Texture2D(1, 1);
            whitePixelTex.SetPixel(0, 0, Color.white);
            whitePixelTex.Apply();
        }

        private void OnDisable()
        {
            SaveWindowConfig();
        }

        private void OnDestroy()
        {
            SaveWindowConfig();
        }

        private void LoadWindowConfig()
        {
            float defaultW = Mathf.Clamp(1020f, MIN_WINDOW_WIDTH, Screen.width - 20f);
            float defaultH = Mathf.Clamp(700f, MIN_WINDOW_HEIGHT, Screen.height - 40f);
            float defaultX = (Screen.width - defaultW) / 2f;
            float defaultY = (Screen.height - defaultH) / 2f;
            fullWindowRect = new Rect(defaultX, defaultY, defaultW, defaultH);

            float miniW = 360f;
            float miniH = 180f;
            miniWindowRect = new Rect(Screen.width - miniW - 20f, 60f, miniW, miniH);

            try
            {
                string dir = Path.Combine(KSPUtil.ApplicationRootPath, "GameData/KSPPerformanceProfiler/PluginData");
                string file = Path.Combine(dir, "window_layout.cfg");
                if (File.Exists(file))
                {
                    string[] lines = File.ReadAllLines(file);
                    float x = defaultX, y = defaultY, w = defaultW, h = defaultH;
                    foreach (var line in lines)
                    {
                        if (string.IsNullOrEmpty(line) || line.StartsWith("#") || line.StartsWith("//")) continue;
                        var parts = line.Split('=');
                        if (parts.Length != 2) continue;
                        string k = parts[0].Trim();
                        string v = parts[1].Trim();
                        if (k.Equals("WindowWidth", StringComparison.OrdinalIgnoreCase) && float.TryParse(v, out float vw)) w = vw;
                        else if (k.Equals("WindowHeight", StringComparison.OrdinalIgnoreCase) && float.TryParse(v, out float vh)) h = vh;
                        else if (k.Equals("WindowX", StringComparison.OrdinalIgnoreCase) && float.TryParse(v, out float vx)) x = vx;
                        else if (k.Equals("WindowY", StringComparison.OrdinalIgnoreCase) && float.TryParse(v, out float vy)) y = vy;
                    }

                    w = Mathf.Clamp(w, MIN_WINDOW_WIDTH, Screen.width - 20f);
                    h = Mathf.Clamp(h, MIN_WINDOW_HEIGHT, Screen.height - 40f);
                    x = Mathf.Clamp(x, 0f, Screen.width - w);
                    y = Mathf.Clamp(y, 0f, Screen.height - h);
                    fullWindowRect = new Rect(x, y, w, h);
                }
            }
            catch { }
        }

        private void SaveWindowConfig()
        {
            try
            {
                string dir = Path.Combine(KSPUtil.ApplicationRootPath, "GameData/KSPPerformanceProfiler/PluginData");
                if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
                string file = Path.Combine(dir, "window_layout.cfg");
                string content = $"// KSPPerformanceProfiler Window Layout\n" +
                                 $"WindowWidth = {fullWindowRect.width:F0}\n" +
                                 $"WindowHeight = {fullWindowRect.height:F0}\n" +
                                 $"WindowX = {fullWindowRect.x:F0}\n" +
                                 $"WindowY = {fullWindowRect.y:F0}\n";
                File.WriteAllText(file, content);
            }
            catch { }
        }

        private void OnGUI()
        {
            if (!IsVisible) return;

            if (HighLogic.Skin != null)
            {
                GUI.skin = HighLogic.Skin;
            }

            InitStyles();

            // Handle window resizing globally across the entire screen
            Event e = Event.current;
            if (isResizing)
            {
                if (e.type == EventType.MouseDrag)
                {
                    Vector2 curMouse = GUIUtility.GUIToScreenPoint(e.mousePosition);
                    float newW = Mathf.Clamp(resizeStartSize.x + (curMouse.x - resizeStartMouse.x), MIN_WINDOW_WIDTH, Screen.width - 20f);
                    float newH = Mathf.Clamp(resizeStartSize.y + (curMouse.y - resizeStartMouse.y), MIN_WINDOW_HEIGHT, Screen.height - 40f);
                    fullWindowRect.width = newW;
                    fullWindowRect.height = newH;
                    e.Use();
                }
                else if (e.rawType == EventType.MouseUp || e.type == EventType.MouseUp)
                {
                    isResizing = false;
                    SaveWindowConfig();
                    e.Use();
                }
            }

            if (IsMiniHud)
            {
                miniWindowRect = GUI.Window(948202, miniWindowRect, DrawMiniHudWindow, ProfilerI18n.Get("app_mini_title"));
                miniWindowRect.x = Mathf.Clamp(miniWindowRect.x, 0, Screen.width - 60f);
                miniWindowRect.y = Mathf.Clamp(miniWindowRect.y, 0, Screen.height - 40f);
            }
            else
            {
                fullWindowRect = GUI.Window(948201, fullWindowRect, DrawFullWindow, ProfilerI18n.Get("app_title"));
                fullWindowRect.x = Mathf.Clamp(fullWindowRect.x, 0, Screen.width - 60f);
                fullWindowRect.y = Mathf.Clamp(fullWindowRect.y, 0, Screen.height - 40f);
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

            resizeGripStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 16,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.LowerRight,
                padding = new RectOffset(0, 2, 0, 2)
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

            DrawHeaderHUD();
            DrawHeroKillerCard();

            GUILayout.Space(2);

            // Tab Bar (cached array to avoid per-frame allocation)
            if (cachedTabNames[0] == null || cachedTabNamesLangPack != ProfilerI18n.CurrentPackIndex)
            {
                cachedTabNamesLangPack = ProfilerI18n.CurrentPackIndex;
                cachedTabNames[0] = ProfilerI18n.Get("tab_dashboard");
                cachedTabNames[1] = ProfilerI18n.Get("tab_scripts");
                cachedTabNames[2] = ProfilerI18n.Get("tab_parts");
                cachedTabNames[3] = ProfilerI18n.Get("tab_assembly");
                cachedTabNames[4] = ProfilerI18n.Get("tab_settings");

                cachedSubTabNames[0] = ProfilerI18n.Get("subtab_plugins");
                cachedSubTabNames[1] = ProfilerI18n.Get("subtab_modules");
            }

            selectedTab = GUILayout.Toolbar(selectedTab, cachedTabNames, GUILayout.Height(26));
            GUILayout.Space(2);

            switch (selectedTab)
            {
                case 0:
                    DrawDashboardTab();
                    break;
                case 1:
                    DrawScriptsTab();
                    break;
                case 2:
                    DrawPartsTab();
                    break;
                case 3:
                    DrawAssemblyTab();
                    break;
                case 4:
                    DrawSettingsAndHelpTab();
                    break;
            }

            GUILayout.EndVertical();

            // Bottom-right corner resize grip handle
            DrawWindowResizeGrip();

            if (!isResizing)
            {
                // Drag window via top header area (36px)
                GUI.DragWindow(new Rect(0, 0, fullWindowRect.width, 36f));
            }
        }

        private void DrawWindowResizeGrip()
        {
            Rect gripRect = new Rect(fullWindowRect.width - RESIZE_HANDLE_SIZE, fullWindowRect.height - RESIZE_HANDLE_SIZE, RESIZE_HANDLE_SIZE, RESIZE_HANDLE_SIZE);
            Event curEvt = Event.current;

            if (curEvt.type == EventType.MouseDown && curEvt.button == 0 && gripRect.Contains(curEvt.mousePosition))
            {
                isResizing = true;
                resizeStartMouse = GUIUtility.GUIToScreenPoint(curEvt.mousePosition);
                resizeStartSize = new Vector2(fullWindowRect.width, fullWindowRect.height);
                curEvt.Use();
            }

            // Visual corner grip
            Color prevCol = GUI.color;
            bool isHovered = gripRect.Contains(curEvt.mousePosition);
            GUI.color = isResizing ? new Color(0.2f, 0.9f, 0.5f, 1f) :
                        (isHovered ? new Color(1f, 0.85f, 0.3f, 1f) : new Color(0.6f, 0.65f, 0.75f, 0.65f));

            GUI.Label(gripRect, "◢", resizeGripStyle);
            GUI.color = prevCol;
        }

        private void DrawHeaderHUD()
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

            // Mod Brand & Status Dot
            string statusDot = ProfilerData.IsEnabled ? "<color=#33FF33>●</color>" : "<color=#888888>○</color>";
            GUILayout.Label($"{statusDot} <b>KSPPerformanceProfiler</b>", headerStyle, GUILayout.Width(140));

            // Core Telemetry Badges
            GUILayout.Label($"<b>FPS:</b> <color={fpsColor}><b>{fps:F1}</b></color> (Avg: {avgFps:F1})", fpsStyle, GUILayout.Width(140));
            GUILayout.Label($"<b>1% Low:</b> <color={fps1Color}><b>{fps1pct:F1}</b></color>", fpsStyle, GUILayout.Width(100));
            GUILayout.Label($"<b>PTR:</b> <color={ptrColor}><b>{ptrStatusText}</b></color>", ptrStyle, GUILayout.Width(95));
            GUILayout.Label($"<b>{ProfilerI18n.Get("metric_total_frame")}:</b> {ProfilerData.SmoothTotalFrameMs:F1}ms (<color={jitterColor}>{jitter:F1}ms</color>)", headerStyle, GUILayout.Width(170));

            // Real-time Mono GC Monitor Badge
            string gcBadge;
            if (ProfilerData.IsGcThisFrame)
            {
                gcBadge = $"<color=#ff3333><b>🗑️ GC!</b> ({ProfilerData.GcCollectionsThisFrame}x)</color>";
            }
            else if (ProfilerData.GcCollectionsPerSec > 0.5)
            {
                gcBadge = $"<color=#ffaa22>🗑️ GC: {ProfilerData.GcCollectionsPerSec:F1}/s</color>";
            }
            else
            {
                string gcColor = ProfilerData.GcFramesSinceLastCollect > 300 ? "#33FF33" : "#aaaaaa";
                gcBadge = $"<color={gcColor}>GC: {ProfilerData.GcTotalCollections}</color>";
            }
            GUILayout.Label(gcBadge, headerStyle, GUILayout.Width(105));

            GUILayout.FlexibleSpace();

            // Window size indicator
            string sizeText = ProfilerI18n.Format("window_size_info", fullWindowRect.width, fullWindowRect.height);
            GUILayout.Label(sizeText, tipStyle, GUILayout.Width(80));
            GUILayout.Space(4);

            // Toggle Profiling button
            string toggleText = ProfilerData.IsEnabled ? ProfilerI18n.Get("enabled") : ProfilerI18n.Get("disabled");
            Color oldCol = GUI.color;
            if (!ProfilerData.IsEnabled) GUI.color = new Color(1f, 0.6f, 0.6f, 1f);
            if (GUILayout.Button(toggleText, GUILayout.Width(105), GUILayout.Height(22)))
            {
                ProfilerData.IsEnabled = !ProfilerData.IsEnabled;
            }
            GUI.color = oldCol;

            // Mini HUD Button
            if (GUILayout.Button($"🗖 {ProfilerI18n.Get("mode_mini")}", GUILayout.Width(85), GUILayout.Height(22)))
            {
                IsMiniHud = true;
            }

            // Close Button
            if (GUILayout.Button("✕", GUILayout.Width(26), GUILayout.Height(22)))
            {
                PhysProfilerPlugin.Instance.SetUIVisibility(false);
            }

            GUILayout.EndHorizontal();
        }

        private void DrawHeroKillerCard()
        {
            var diag = GetCachedDiagnostic();

            GUILayout.BeginVertical(cardAccentStyle);

            // Row 1: Diagnosis Hero Banner & Auto-Freeze Trigger
            GUILayout.BeginHorizontal();

            if (ProfilerData.IsFrozen)
            {
                GUI.color = new Color(1f, 0.85f, 0.3f, 1f);
                GUILayout.Label($"<b>{ProfilerI18n.Get("hero_frozen_banner")}</b>", headerStyle);
                GUI.color = Color.white;
            }
            else
            {
                GUILayout.Label($"<b>{ProfilerI18n.Get("hero_killer_title")}:</b> <color={diag.StatusColorHex}><b>{diag.Title}</b></color>", headerStyle);
            }

            GUILayout.FlexibleSpace();

            // Resume button if frozen
            if (ProfilerData.IsFrozen)
            {
                Color prevCol = GUI.backgroundColor;
                GUI.backgroundColor = new Color(1f, 0.85f, 0.2f, 1f);
                if (GUILayout.Button($"<b>{ProfilerI18n.Get("hero_unfreeze_btn")}</b>", GUILayout.Width(130), GUILayout.Height(23)))
                {
                    ProfilerData.IsFrozen = false;
                }
                GUI.backgroundColor = prevCol;
                GUILayout.Space(6);
            }

            // Quick Pad Button if unpadded, low headroom, or stutter bound
            if (!MonoHeapPadder.IsPadded || diag.Type == BottleneckType.StutterJitterBound || MonoHeapPadder.CurrentFreeMb < 400.0)
            {
                Color prevPadCol = GUI.backgroundColor;
                GUI.backgroundColor = new Color(0.2f, 0.9f, 0.5f, 1f);
                string quickPadLabel = ProfilerI18n.Get("pad_hero_btn");
                if (GUILayout.Button(quickPadLabel, GUILayout.Width(140), GUILayout.Height(23)))
                {
                    MonoHeapPadder.Pad(MonoHeapPadder.TargetPadMb);
                }
                GUI.backgroundColor = prevPadCol;
                GUILayout.Space(6);
            }

            // KILLER FEATURE: Auto-Freeze Toggle Button
            string freezeBtnText = ProfilerData.AutoFreezeOnSpike ? ProfilerI18n.Get("hero_auto_freeze_on") : ProfilerI18n.Get("hero_auto_freeze_off");
            Color prevBg = GUI.backgroundColor;
            if (ProfilerData.AutoFreezeOnSpike)
            {
                GUI.backgroundColor = new Color(0.2f, 0.85f, 0.4f, 1f);
            }
            if (GUILayout.Button(freezeBtnText, GUILayout.Width(160), GUILayout.Height(23)))
            {
                ProfilerData.AutoFreezeOnSpike = !ProfilerData.AutoFreezeOnSpike;
            }
            GUI.backgroundColor = prevBg;

            GUILayout.Space(6);

            // Detailed Advice Toggle
            string adviceToggle = showDetailedAdvice ? ProfilerI18n.Get("bn_btn_hide_advice") : ProfilerI18n.Get("bn_btn_show_advice");
            if (GUILayout.Button(adviceToggle, GUILayout.Width(85), GUILayout.Height(23)))
            {
                showDetailedAdvice = !showDetailedAdvice;
            }

            GUILayout.EndHorizontal();

            // Row 2: Top Offenders Direct Exposure
            if (diag.TopCulprits != null && diag.TopCulprits.Count > 0)
            {
                GUILayout.BeginHorizontal();
                GUILayout.Label($"<b>{ProfilerI18n.Get("hero_top_culprits")}</b>", tipStyle, GUILayout.Width(105));

                for (int i = 0; i < diag.TopCulprits.Count; i++)
                {
                    var c = diag.TopCulprits[i];
                    string colorStr = c.PctOfFrame > 20.0 ? "#FF5555" : (c.PctOfFrame > 10.0 ? "#FFAA22" : "#55FF88");
                    string badgeText = $"<b>#{i + 1}</b> [{c.SourceType}] <b>{c.Name}</b> (<color={colorStr}>{c.SmoothMs:F1}ms, {c.PctOfFrame:F1}%</color>)";
                    GUILayout.Label(badgeText, tipStyle);
                    GUILayout.Space(8);
                }

                GUILayout.FlexibleSpace();
                GUILayout.EndHorizontal();
            }

            // Row 3: Actionable Prescription (if expanded)
            if (showDetailedAdvice && !string.IsNullOrEmpty(diag.Advice))
            {
                GUILayout.BeginHorizontal("box");
                GUILayout.Label($"💡 <b>{ProfilerI18n.Get("bn_advice_label")}:</b> {diag.Advice}", tipStyle);

                if (!MonoHeapPadder.IsPadded || MonoHeapPadder.CurrentFreeMb < 500.0 || diag.Type == BottleneckType.StutterJitterBound)
                {
                    Color prevBg2 = GUI.backgroundColor;
                    GUI.backgroundColor = new Color(0.2f, 0.9f, 0.5f, 1f);
                    string quickPadLabel = ProfilerI18n.Get("pad_hero_btn");
                    if (GUILayout.Button(quickPadLabel, GUILayout.Width(140), GUILayout.Height(22)))
                    {
                        MonoHeapPadder.Pad(MonoHeapPadder.TargetPadMb);
                    }
                    GUI.backgroundColor = prevBg2;
                }

                GUILayout.EndHorizontal();
            }

            // Row 4: Macro Budget Bar Strip
            DrawMacroBudgetBarStrip();

            GUILayout.EndVertical();
        }

        private void DrawMacroBudgetBarStrip()
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

            Rect barRect = GUILayoutUtility.GetRect(0, 10, GUILayout.ExpandWidth(true));
            GUI.Box(barRect, "");

            float currentX = barRect.x + 1;
            float barY = barRect.y + 1;
            float totalBarW = barRect.width - 2;
            float barH = barRect.height - 2;

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

            GUILayout.BeginHorizontal();
            GUILayout.Label($"<color=#00d2d3>■</color> {ProfilerI18n.Get("legend_modules")}: {modMs:F1}ms ({(pMod * 100f):F0}%)", tipStyle);
            GUILayout.Label($"<color=#a29bfe>■</color> {ProfilerI18n.Get("legend_plugins")}: {pluginMs:F1}ms ({(pPlugin * 100f):F0}%)", tipStyle);
            GUILayout.Label($"<color=#ff9f43>■</color> {ProfilerI18n.Get("legend_physx")}: {physxMs:F1}ms ({(pPhysx * 100f):F0}%)", tipStyle);
            GUILayout.Label($"<color=#feca57>■</color> {ProfilerI18n.Get("legend_gpu")}: {gpuMs:F1}ms ({(pGpu * 100f):F0}%)", tipStyle);
            GUILayout.Label($"<color=#8395a7>■</color> {ProfilerI18n.Get("legend_overhead")}: {overheadMs:F1}ms ({(pOverhead * 100f):F0}%)", tipStyle);
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();
        }

        private void DrawBarSegment(ref float currentX, float y, float width, float height, Color color)
        {
            if (width < 0.5f) return;
            GUI.color = color;
            GUI.DrawTexture(new Rect(currentX, y, width, height), whitePixelTex);
            currentX += width;
        }

        private void DrawMicroBar(double ms, double maxMs = 20.0, float width = 45f, float height = 8f)
        {
            Rect r = GUILayoutUtility.GetRect(width, height, GUILayout.Width(width), GUILayout.Height(height));
            r.y += 3f;

            // Background track
            GUI.color = new Color(0.18f, 0.22f, 0.28f, 0.7f);
            GUI.DrawTexture(r, whitePixelTex);

            // Fill
            float fillPct = Mathf.Clamp01((float)(ms / Math.Max(0.001, maxMs)));
            float fillW = r.width * fillPct;
            if (fillW > 0.5f)
            {
                Color fillCol = ms > 4.0 ? new Color(1f, 0.3f, 0.3f, 0.95f) :
                               (ms > 1.2 ? new Color(1f, 0.75f, 0.2f, 0.95f) :
                               new Color(0.2f, 0.85f, 0.5f, 0.9f));
                GUI.color = fillCol;
                GUI.DrawTexture(new Rect(r.x, r.y, fillW, r.height), whitePixelTex);
            }
            GUI.color = Color.white;
        }

        #endregion

        #region Tab 0: Dashboard & Spike Sniffer

        private void DrawDashboardTab()
        {
            scrollPosGraph = GUILayout.BeginScrollView(scrollPosGraph, false, false, GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));
            GUILayout.BeginVertical();

            // 1. Spike Sniffer (Hero Stutter Inspector - 置顶杀手级抓拍)
            DrawSpikeSnifferCard();

            GUILayout.Space(4);

            // 2. Interactive Timeline Waveform Graph (自适应与交互式)
            DrawTimelineGraphCard();

            GUILayout.EndVertical();
            GUILayout.EndScrollView();
        }

        private void DrawTimelineGraphCard()
        {
            GUILayout.BeginVertical(cardStyle);

            // Controls Toolbar Row 1: Title on left, Mode buttons on right
            GUILayout.BeginHorizontal();
            GUILayout.Label($"<b>{ProfilerI18n.Get("graph_title")}</b>", headerStyle);
            GUILayout.FlexibleSpace();

            if (GUILayout.Button(ProfilerI18n.Get("graph_mode_stacked"), graphMode == 0 ? tableHeaderBtnStyle : GUI.skin.button, GUILayout.Width(140), GUILayout.Height(22)))
            {
                graphMode = 0;
            }
            if (GUILayout.Button(ProfilerI18n.Get("graph_mode_curve"), graphMode == 1 ? tableHeaderBtnStyle : GUI.skin.button, GUILayout.Width(170), GUILayout.Height(22)))
            {
                graphMode = 1;
            }
            GUILayout.EndHorizontal();

            // Controls Toolbar Row 2: Interactive Legend on left, Time Span buttons on right
            GUILayout.BeginHorizontal();
            if (graphMode == 0)
            {
                // Clickable Layer Filter Pills
                string modPill = showLayerModules ? "<color=#00d2d3>■</color>" : "<color=#555555>□</color>";
                if (GUILayout.Button($"{modPill} {ProfilerI18n.Get("legend_modules")}", tipStyle))
                {
                    showLayerModules = !showLayerModules;
                }
                GUILayout.Space(6);

                string plugPill = showLayerPlugins ? "<color=#a29bfe>■</color>" : "<color=#555555>□</color>";
                if (GUILayout.Button($"{plugPill} {ProfilerI18n.Get("legend_plugins")}", tipStyle))
                {
                    showLayerPlugins = !showLayerPlugins;
                }
                GUILayout.Space(6);

                string physxPill = showLayerPhysx ? "<color=#ff9f43>■</color>" : "<color=#555555>□</color>";
                if (GUILayout.Button($"{physxPill} {ProfilerI18n.Get("legend_physx")}", tipStyle))
                {
                    showLayerPhysx = !showLayerPhysx;
                }
                GUILayout.Space(6);

                string gpuPill = showLayerGpu ? "<color=#feca57>■</color>" : "<color=#555555>□</color>";
                if (GUILayout.Button($"{gpuPill} {ProfilerI18n.Get("legend_gpu")}", tipStyle))
                {
                    showLayerGpu = !showLayerGpu;
                }
                GUILayout.Space(6);

                string ovhPill = showLayerOverhead ? "<color=#8395a7>■</color>" : "<color=#555555>□</color>";
                if (GUILayout.Button($"{ovhPill} {ProfilerI18n.Get("legend_overhead")}", tipStyle))
                {
                    showLayerOverhead = !showLayerOverhead;
                }

                GUILayout.Space(8);
                GUILayout.Label("<color=#ff5555>▼</color> 尖峰", tipStyle);
                GUILayout.Space(4);
                GUILayout.Label("<color=#ff8800>◆</color> GC", tipStyle);
            }
            else
            {
                GUILayout.Label($"<color=#33ff55>―</color> 60FPS  <color=#ffaa22>―</color> 30FPS  <color=#33ff55>■</color> {ProfilerI18n.Get("graph_curve_fps")}  <color=#ffaa22>■</color> {ProfilerI18n.Get("graph_curve_1pct")}", tipStyle);
            }

            GUILayout.FlexibleSpace();
            GUILayout.Label(ProfilerI18n.Get("graph_scale_label"), tipStyle);

            if (GUILayout.Button(ProfilerI18n.Get("graph_frames_100"), graphTimeRange == 100 ? tableHeaderBtnStyle : GUI.skin.button, GUILayout.Width(90), GUILayout.Height(20)))
            {
                graphTimeRange = 100;
            }
            if (GUILayout.Button(ProfilerI18n.Get("graph_frames_300"), graphTimeRange == 300 ? tableHeaderBtnStyle : GUI.skin.button, GUILayout.Width(90), GUILayout.Height(20)))
            {
                graphTimeRange = 300;
            }
            if (GUILayout.Button(ProfilerI18n.Get("graph_frames_600"), graphTimeRange == 600 ? tableHeaderBtnStyle : GUI.skin.button, GUILayout.Width(90), GUILayout.Height(20)))
            {
                graphTimeRange = 600;
            }
            GUILayout.EndHorizontal();

            // Graph Canvas (Fluid Width)
            Rect graphRect = GUILayoutUtility.GetRect(0, 195, GUILayout.ExpandWidth(true));
            GUI.Box(graphRect, "");

            var timeline = ProfilerData.GetTimelineSample(graphTimeRange);

            if (timeline != null && timeline.Count > 1)
            {
                float axisW = 65f;
                float innerW = graphRect.width - 24 - axisW;
                float innerH = graphRect.height - 24;
                float innerX = graphRect.x + 12;
                float innerY = graphRect.y + 12;
                float axisX = innerX + innerW + 4;

                Event currentEvt = Event.current;
                Vector2 mousePos = currentEvt.mousePosition;
                bool mouseInChart = new Rect(innerX, innerY, innerW, innerH).Contains(mousePos);
                float barWidth = innerW / timeline.Count;

                // Mouse Hover & Click Detection
                if (mouseInChart && timeline.Count > 0)
                {
                    float relX = mousePos.x - innerX;
                    int curHover = Mathf.Clamp((int)(relX / barWidth), 0, timeline.Count - 1);
                    hoveredTimelineIdx = curHover;

                    if (currentEvt.type == EventType.MouseDown && currentEvt.button == 0)
                    {
                        if (lockedTimelineIdx == curHover)
                        {
                            lockedTimelineIdx = -1; // unlock
                        }
                        else
                        {
                            lockedTimelineIdx = curHover; // lock
                            if (timeline.IsSpike[curHover])
                            {
                                int sId = timeline.SpikeId[curHover];
                                int foundIdx = ProfilerData.SpikeHistory.FindIndex(s => s.SpikeId == sId);
                                if (foundIdx >= 0) selectedSpikeIndex = foundIdx;
                            }
                        }
                        currentEvt.Use();
                    }
                }
                else if (currentEvt.type == EventType.Repaint && lockedTimelineIdx < 0)
                {
                    hoveredTimelineIdx = -1;
                }

                if (graphMode == 0)
                {
                    // === Mode 0: Multi-layer Stacked Area Graph ===
                    double peakMs = 45.0;
                    for (int i = 0; i < timeline.Count; i++)
                    {
                        if (timeline.TotalMs[i] > peakMs) peakMs = timeline.TotalMs[i];
                    }
                    float maxMs = Mathf.Clamp((float)peakMs * 1.15f, 40f, 160f);

                    // Guidelines across innerW
                    float y60 = innerY + innerH - ((16.67f / maxMs) * innerH);
                    float y30 = innerY + innerH - ((33.33f / maxMs) * innerH);
                    float y20 = innerY + innerH - ((50.0f / maxMs) * innerH);

                    // 60 FPS (Green)
                    if (y60 >= innerY && y60 <= innerY + innerH)
                    {
                        GUI.color = new Color(0.2f, 0.9f, 0.35f, 0.35f);
                        GUI.DrawTexture(new Rect(innerX, y60, innerW, 1.5f), whitePixelTex);
                        GUI.Label(new Rect(axisX, y60 - 8, axisW, 16), $"<color=#33ff55>{ProfilerI18n.Get("graph_axis_60")}</color>", tipStyle);
                    }

                    // 30 FPS (Orange)
                    if (y30 >= innerY && y30 <= innerY + innerH)
                    {
                        GUI.color = new Color(1f, 0.65f, 0.2f, 0.35f);
                        GUI.DrawTexture(new Rect(innerX, y30, innerW, 1.5f), whitePixelTex);
                        GUI.Label(new Rect(axisX, y30 - 8, axisW, 16), $"<color=#ffaa22>{ProfilerI18n.Get("graph_axis_30")}</color>", tipStyle);
                    }

                    // 20 FPS (Red)
                    if (y20 >= innerY && y20 <= innerY + innerH)
                    {
                        GUI.color = new Color(1f, 0.3f, 0.3f, 0.35f);
                        GUI.DrawTexture(new Rect(innerX, y20, innerW, 1.5f), whitePixelTex);
                        GUI.Label(new Rect(axisX, y20 - 8, axisW, 16), $"<color=#ff5555>{ProfilerI18n.Get("graph_axis_20")}</color>", tipStyle);
                    }

                    // Top Peak label
                    GUI.Label(new Rect(axisX, innerY - 4, axisW, 16), $"<color=#888888>{maxMs:F0}ms</color>", tipStyle);

                    // Stack colors
                    Color colMod = new Color(0f, 0.82f, 0.83f, 0.95f);       // Cyan
                    Color colPlugin = new Color(0.63f, 0.61f, 0.99f, 0.95f);  // Violet
                    Color colPhysx = new Color(1f, 0.62f, 0.26f, 0.95f);     // Orange
                    Color colGpu = new Color(1f, 0.8f, 0.34f, 0.95f);        // Gold
                    Color colOverhead = new Color(0.51f, 0.58f, 0.65f, 0.85f); // Slate

                    for (int i = 0; i < timeline.Count; i++)
                    {
                        float xPos = innerX + (i * barWidth);
                        float currentY = innerY + innerH;
                        float w = Math.Max(1.0f, barWidth - (timeline.Count > 200 ? 0f : 0.5f));

                        // 1. PartModules
                        float hMod = showLayerModules ? Mathf.Clamp((float)(timeline.ModulesMs[i] / maxMs) * innerH, 0f, innerH) : 0f;
                        currentY -= hMod;
                        if (hMod > 0.5f)
                        {
                            GUI.color = colMod;
                            GUI.DrawTexture(new Rect(xPos, currentY, w, hMod), whitePixelTex);
                        }

                        // 2. Plugins
                        float hPlugin = showLayerPlugins ? Mathf.Clamp((float)(timeline.PluginsMs[i] / maxMs) * innerH, 0f, innerH) : 0f;
                        currentY -= hPlugin;
                        if (hPlugin > 0.5f)
                        {
                            GUI.color = colPlugin;
                            GUI.DrawTexture(new Rect(xPos, currentY, w, hPlugin), whitePixelTex);
                        }

                        // 3. PhysX
                        float hPhysx = showLayerPhysx ? Mathf.Clamp((float)(timeline.PhysXMs[i] / maxMs) * innerH, 0f, innerH) : 0f;
                        currentY -= hPhysx;
                        if (hPhysx > 0.5f)
                        {
                            GUI.color = colPhysx;
                            GUI.DrawTexture(new Rect(xPos, currentY, w, hPhysx), whitePixelTex);
                        }

                        // 4. GPU / Render
                        float hGpu = showLayerGpu ? Mathf.Clamp((float)(timeline.GpuMs[i] / maxMs) * innerH, 0f, innerH) : 0f;
                        currentY -= hGpu;
                        if (hGpu > 0.5f)
                        {
                            GUI.color = colGpu;
                            GUI.DrawTexture(new Rect(xPos, currentY, w, hGpu), whitePixelTex);
                        }

                        // 5. Overhead
                        float hOverhead = showLayerOverhead ? Mathf.Clamp((float)(timeline.OverheadMs[i] / maxMs) * innerH, 0f, innerH) : 0f;
                        currentY -= hOverhead;
                        if (hOverhead > 0.5f)
                        {
                            GUI.color = colOverhead;
                            GUI.DrawTexture(new Rect(xPos, currentY, w, hOverhead), whitePixelTex);
                        }

                        // Spike Marker ▼
                        if (timeline.IsSpike[i])
                        {
                            GUI.color = new Color(1f, 0.2f, 0.2f, 1f);
                            Rect markerRect = new Rect(xPos - 3, innerY - 2, 12, 14);
                            GUI.Label(markerRect, "▼", headerStyle);
                        }

                        // GC Event Marker ◆
                        if (timeline.GcCollections[i] > 0)
                        {
                            GUI.color = new Color(1f, 0.5f, 0.1f, 1f);
                            float gcMarkerY = timeline.IsSpike[i] ? innerY + 10 : innerY - 2;
                            Rect gcMarkerRect = new Rect(xPos - 2, gcMarkerY, 10, 12);
                            GUI.Label(gcMarkerRect, "◆", tipStyle);
                        }
                    }
                    GUI.color = Color.white;
                }
                else
                {
                    // === Mode 1: FPS & 1% Low Curve Mode ===
                    float maxFps = 120f;
                    float y60 = innerY + innerH - ((60f / maxFps) * innerH);
                    float y30 = innerY + innerH - ((30f / maxFps) * innerH);

                    // 60 FPS Target (Green)
                    GUI.color = new Color(0.2f, 0.9f, 0.35f, 0.35f);
                    GUI.DrawTexture(new Rect(innerX, y60, innerW, 1.5f), whitePixelTex);
                    GUI.Label(new Rect(axisX, y60 - 8, axisW, 16), "<color=#33ff55>60 FPS</color>", tipStyle);

                    // 30 FPS Minimum (Orange)
                    GUI.color = new Color(1f, 0.65f, 0.2f, 0.35f);
                    GUI.DrawTexture(new Rect(innerX, y30, innerW, 1.5f), whitePixelTex);
                    GUI.Label(new Rect(axisX, y30 - 8, axisW, 16), "<color=#ffaa22>30 FPS</color>", tipStyle);

                    for (int i = 0; i < timeline.Count; i++)
                    {
                        float xPos = innerX + (i * barWidth);
                        float w = Math.Max(1.0f, barWidth - (timeline.Count > 200 ? 0f : 0.5f));

                        // 1% Low indicator (Dark amber base)
                        float hLow = Mathf.Clamp((float)(timeline.OnePctLow[i] / maxFps) * innerH, 1f, innerH);
                        float yLow = innerY + innerH - hLow;
                        GUI.color = new Color(1f, 0.65f, 0.15f, 0.6f);
                        GUI.DrawTexture(new Rect(xPos, yLow, w, hLow), whitePixelTex);

                        // FPS bar (Green/Cyan)
                        float hFps = Mathf.Clamp((float)(timeline.Fps[i] / maxFps) * innerH, 1f, innerH);
                        float yFps = innerY + innerH - hFps;
                        Color fpsCol = timeline.Fps[i] >= 55f ? new Color(0.2f, 0.9f, 0.35f, 0.9f) :
                                      (timeline.Fps[i] >= 28f ? new Color(1f, 0.8f, 0.2f, 0.9f) :
                                      new Color(1f, 0.3f, 0.3f, 0.95f));
                        GUI.color = fpsCol;
                        GUI.DrawTexture(new Rect(xPos, yFps, w, 2.5f), whitePixelTex);

                        // Spike indicator
                        if (timeline.IsSpike[i])
                        {
                            GUI.color = new Color(1f, 0.2f, 0.2f, 1f);
                            Rect markerRect = new Rect(xPos - 3, innerY - 2, 12, 14);
                            GUI.Label(markerRect, "▼", headerStyle);
                        }

                        // GC Event Marker ◆
                        if (timeline.GcCollections[i] > 0)
                        {
                            GUI.color = new Color(1f, 0.5f, 0.1f, 1f);
                            float gcMarkerY = timeline.IsSpike[i] ? innerY + 10 : innerY - 2;
                            Rect gcMarkerRect = new Rect(xPos - 2, gcMarkerY, 10, 12);
                            GUI.Label(gcMarkerRect, "◆", tipStyle);
                        }
                    }
                    GUI.color = Color.white;
                }

                // Interactive Crosshair & Column Highlight
                int activeInspect = lockedTimelineIdx >= 0 ? lockedTimelineIdx : hoveredTimelineIdx;
                if (activeInspect >= 0 && activeInspect < timeline.Count)
                {
                    float colX = innerX + (activeInspect * barWidth);

                    // Glowing column highlight
                    Color colBg = lockedTimelineIdx >= 0 ? new Color(1f, 0.85f, 0.2f, 0.22f) : new Color(0.2f, 0.9f, 1f, 0.14f);
                    GUI.color = colBg;
                    GUI.DrawTexture(new Rect(colX, innerY, barWidth, innerH), whitePixelTex);

                    // Luminous vertical cursor crosshair
                    Color lineCol = lockedTimelineIdx >= 0 ? new Color(1f, 0.85f, 0.2f, 0.9f) : new Color(0.2f, 0.9f, 1f, 0.75f);
                    GUI.color = lineCol;
                    GUI.DrawTexture(new Rect(colX + (barWidth * 0.5f) - 0.75f, innerY, 1.5f, innerH), whitePixelTex);
                    GUI.color = Color.white;
                }
            }

            // Phase Breakdown Sub-card / Interactive Frame Inspection HUD
            GUILayout.Space(4);
            int curInspect = lockedTimelineIdx >= 0 ? lockedTimelineIdx : hoveredTimelineIdx;

            if (curInspect >= 0 && timeline != null && curInspect < timeline.Count)
            {
                double hTotal = Math.Max(0.001, timeline.TotalMs[curInspect]);
                double hFps = timeline.Fps[curInspect];
                double hMod = timeline.ModulesMs[curInspect];
                double hPlugin = timeline.PluginsMs[curInspect];
                double hPhysx = timeline.PhysXMs[curInspect];
                double hGpu = timeline.GpuMs[curInspect];
                double hOverhead = timeline.OverheadMs[curInspect];
                bool hSpike = timeline.IsSpike[curInspect];
                int framesAgo = timeline.Count - 1 - curInspect;

                Color prevBg = GUI.backgroundColor;
                if (lockedTimelineIdx >= 0) GUI.backgroundColor = new Color(0.35f, 0.3f, 0.15f, 1f);
                else GUI.backgroundColor = new Color(0.15f, 0.25f, 0.35f, 1f);

                GUILayout.BeginHorizontal("box");

                string lockTag = lockedTimelineIdx >= 0 ? ProfilerI18n.Get("graph_hover_locked") : ProfilerI18n.Get("graph_hover_tracking");
                GUILayout.Label($"{lockTag} <b>-{framesAgo} 帧</b> ({hTotal:F1}ms, {hFps:F1} FPS)", headerStyle, GUILayout.Width(220));

                GUILayout.Label($"<color=#00d2d3>■</color> 模块: {hMod:F1}ms ({(hMod / hTotal * 100):F0}%)", tipStyle);
                GUILayout.Label($"<color=#a29bfe>■</color> 插件: {hPlugin:F1}ms ({(hPlugin / hTotal * 100):F0}%)", tipStyle);
                GUILayout.Label($"<color=#ff9f43>■</color> 物理: {hPhysx:F1}ms ({(hPhysx / hTotal * 100):F0}%)", tipStyle);
                GUILayout.Label($"<color=#feca57>■</color> GPU: {hGpu:F1}ms ({(hGpu / hTotal * 100):F0}%)", tipStyle);
                GUILayout.Label($"<color=#8395a7>■</color> 调度: {hOverhead:F1}ms ({(hOverhead / hTotal * 100):F0}%)", tipStyle);

                if (hSpike)
                {
                    GUILayout.Label($"<color=#ff3333><b>{ProfilerI18n.Get("graph_hover_spike_tag")}</b></color>", headerStyle);
                }

                // GC event indicator for inspected frame
                int hGc = timeline.GcCollections[curInspect];
                if (hGc > 0)
                {
                    GUILayout.Label($"<color=#ff8800><b>◆ GC×{hGc}</b></color>", headerStyle);
                }

                GUILayout.FlexibleSpace();

                if (lockedTimelineIdx >= 0)
                {
                    if (GUILayout.Button(ProfilerI18n.Get("graph_unlock_btn"), GUILayout.Width(75), GUILayout.Height(20)))
                    {
                        lockedTimelineIdx = -1;
                    }
                }

                GUILayout.EndHorizontal();
                GUI.backgroundColor = prevBg;
            }
            else
            {
                // Standard summary bar when no frame is hovered
                GUILayout.BeginHorizontal("box");
                GUILayout.Label($"<b>{ProfilerI18n.Get("metric_physx")}:</b> {ProfilerData.SmoothPhysicsStepMs:F2} ms", tipStyle, GUILayout.Width(170));
                GUILayout.Label($"<b>{ProfilerI18n.Get("metric_camera")}:</b> {ProfilerData.CameraRenderMs:F2} ms", tipStyle, GUILayout.Width(170));
                GUILayout.Label($"<b>{ProfilerI18n.Get("metric_modules")}:</b> {ProfilerData.SmoothModuleScriptMs:F2} ms", tipStyle, GUILayout.Width(170));
                GUILayout.Label($"<b>{ProfilerI18n.Get("metric_plugins")}:</b> {ProfilerData.SmoothPluginScriptMs:F2} ms", tipStyle, GUILayout.Width(170));
                if (ProfilerData.IsTUFXDetected)
                {
                    GUILayout.Label($"<b><color=#00e5ff>TUFX:</color></b> {ProfilerData.TUFXPostProcessMs:F2} ms", tipStyle, GUILayout.Width(140));
                }

                // Mono GC real-time stats
                double heapMb = MonoHeapPadder.CurrentHeapMb;
                double padMb = MonoHeapPadder.PaddedMb;
                double freeMb = MonoHeapPadder.CurrentFreeMb;
                string heapColor = (freeMb >= 800.0 || padMb >= 1000.0) ? "#33FF33" : (freeMb >= 300.0 ? "#FFFF33" : "#FF3333");
                string gcRateStr = ProfilerData.GcCollectionsPerSec > 0.1
                    ? $"<color=#ff8800>{ProfilerData.GcCollectionsPerSec:F1}/s</color>"
                    : $"<color=#33FF33>0/s</color>";
                string padTag = padMb > 0 ? $"<color=#33FF33>[+{padMb:F0}MB]</color> " : "";
                GUILayout.Label($"<b>🗑️ Mono:</b> {padTag}<color={heapColor}>{heapMb:F0}MB</color> GC: {gcRateStr}", tipStyle);

                GUILayout.FlexibleSpace();
                GUILayout.EndHorizontal();
            }

            GUILayout.EndVertical();
        }

        private void DrawSpikeSnifferCard()
        {
            GUILayout.BeginVertical(cardAccentStyle);

            GUILayout.BeginHorizontal();
            GUILayout.Label($"<b>{ProfilerI18n.Get("spike_card_title")}</b>", headerStyle);
            GUILayout.FlexibleSpace();

            // Prev/Next & Clear buttons
            if (ProfilerData.SpikeHistory.Count > 1)
            {
                if (GUILayout.Button(ProfilerI18n.Get("spike_prev"), GUILayout.Width(90), GUILayout.Height(22)))
                {
                    if (selectedSpikeIndex <= 0) selectedSpikeIndex = ProfilerData.SpikeHistory.Count - 1;
                    else selectedSpikeIndex--;
                }

                int curIdx = (selectedSpikeIndex >= 0 && selectedSpikeIndex < ProfilerData.SpikeHistory.Count)
                    ? selectedSpikeIndex + 1
                    : ProfilerData.SpikeHistory.Count;
                GUILayout.Label(string.Format(ProfilerI18n.Get("spike_history_count"), curIdx, ProfilerData.SpikeHistory.Count), tipStyle, GUILayout.Width(100));

                if (GUILayout.Button(ProfilerI18n.Get("spike_next"), GUILayout.Width(90), GUILayout.Height(22)))
                {
                    if (selectedSpikeIndex >= ProfilerData.SpikeHistory.Count - 1) selectedSpikeIndex = 0;
                    else selectedSpikeIndex++;
                }
                GUILayout.Space(8);
            }

            if (ProfilerData.SpikeHistory.Count > 0 && GUILayout.Button(ProfilerI18n.Get("spike_clear"), GUILayout.Width(85), GUILayout.Height(22)))
            {
                ProfilerData.ClearSpikeHistory();
                selectedSpikeIndex = -1;
            }

            GUILayout.EndHorizontal();

            // Fetch active spike
            SpikeSnapshot spike = null;
            if (selectedSpikeIndex >= 0 && selectedSpikeIndex < ProfilerData.SpikeHistory.Count)
            {
                spike = ProfilerData.SpikeHistory[selectedSpikeIndex];
            }
            else if (ProfilerData.LatestSpike != null)
            {
                spike = ProfilerData.LatestSpike;
            }

            if (spike == null)
            {
                GUILayout.Label($"<color=#33FF55>🟢 {ProfilerI18n.Get("spike_none")}</color>", tipStyle);
            }
            else
            {
                // Navigation and status bar
                GUILayout.BeginHorizontal();

                // Prev/Next buttons
                if (ProfilerData.SpikeHistory.Count > 1)
                {
                    if (GUILayout.Button(ProfilerI18n.Get("spike_prev"), GUILayout.Width(90)))
                    {
                        if (selectedSpikeIndex <= 0) selectedSpikeIndex = ProfilerData.SpikeHistory.Count - 1;
                        else selectedSpikeIndex--;
                    }

                    int curIdx = (selectedSpikeIndex >= 0 && selectedSpikeIndex < ProfilerData.SpikeHistory.Count)
                        ? selectedSpikeIndex + 1
                        : ProfilerData.SpikeHistory.Count;
                    GUILayout.Label(string.Format(ProfilerI18n.Get("spike_history_count"), curIdx, ProfilerData.SpikeHistory.Count), tipStyle, GUILayout.Width(110));

                    if (GUILayout.Button(ProfilerI18n.Get("spike_next"), GUILayout.Width(90)))
                    {
                        if (selectedSpikeIndex >= ProfilerData.SpikeHistory.Count - 1) selectedSpikeIndex = 0;
                        else selectedSpikeIndex++;
                    }
                    GUILayout.Space(10);
                }

                GUILayout.Label(string.Format(ProfilerI18n.Get("spike_time"), spike.Timestamp.ToString("HH:mm:ss")), tipStyle, GUILayout.Width(130));
                GUILayout.Label(string.Format(ProfilerI18n.Get("spike_duration"), spike.FrameMs, spike.NormalAvgMs, spike.SpikeRatio), tipStyle);

                GUILayout.FlexibleSpace();

                // GC pause badge
                if (spike.IsGcPause)
                {
                    GUILayout.Label(string.Format(ProfilerI18n.Get("spike_gc_detected"), spike.GcCollections), headerStyle);
                }
                else
                {
                    GUILayout.Label($"<color=#888888>{ProfilerI18n.Get("spike_gc_none")}</color>", tipStyle);
                }

                GUILayout.EndHorizontal();

                // Culprits table
                if (spike.Culprits != null && spike.Culprits.Count > 0)
                {
                    GUILayout.Space(2);
                    GUILayout.Label($"<b>{ProfilerI18n.Get("spike_culprit_header")}</b>", tipStyle);

                    for (int i = 0; i < spike.Culprits.Count; i++)
                    {
                        var c = spike.Culprits[i];
                        string catColor = c.Category == "PartModule" ? "#00d2d3" :
                                         (c.Category == "Plugin" ? "#a29bfe" :
                                         (c.Category == "PhysX" ? "#ff9f43" :
                                         (c.Category == "Mono GC" ? "#ff5555" : "#feca57")));

                        string methText = !string.IsNullOrEmpty(c.TopMethod) ? $" -> <color=#88bbdd>{c.TopMethod}</color>" : "";

                        GUILayout.BeginHorizontal("box");
                        GUILayout.Label($"<b>#{i + 1}</b> <color={catColor}>[{c.Category}]</color> <b>{c.Name}</b>{methText}", tipStyle, GUILayout.Width(520));
                        GUILayout.Label($"<color=#ff5555><b>{c.FrameMs:F1} ms</b></color> ({c.PctOfFrame:F1}%)", tipStyle, GUILayout.Width(130));
                        DrawMicroBar(c.FrameMs, spike.FrameMs, 100f, 10f);
                        GUILayout.FlexibleSpace();
                        GUILayout.EndHorizontal();
                    }
                }
            }

            GUILayout.EndVertical();
        }

        #endregion

        #region Tab 1: Mod Plugins & Scripts

        private void DrawScriptsTab()
        {
            GUILayout.BeginVertical(cardStyle);

            // Sub-tab selector
            GUILayout.BeginHorizontal();
            if (GUILayout.Button(ProfilerI18n.Get("subtab_plugins"), subTabScripts == 0 ? tableHeaderBtnStyle : GUI.skin.button, GUILayout.Width(240), GUILayout.Height(24)))
            {
                subTabScripts = 0;
            }
            if (GUILayout.Button(ProfilerI18n.Get("subtab_modules"), subTabScripts == 1 ? tableHeaderBtnStyle : GUI.skin.button, GUILayout.Width(240), GUILayout.Height(24)))
            {
                subTabScripts = 1;
            }
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();

            GUILayout.Space(4);

            if (subTabScripts == 0)
            {
                DrawPluginsContent();
            }
            else
            {
                DrawModulesContent();
            }

            GUILayout.EndVertical();
        }

        private void DrawPluginsContent()
        {

            if (ProfilerData.IsTUFXDetected)
            {
                GUILayout.BeginHorizontal("box");
                GUILayout.Label($"<b><color=#00e5ff>[TUFX]</color> {ProfilerI18n.Get("tufx_status_header")}:</b> {ProfilerI18n.Get("tufx_active_passes")}: <color=yellow>{ProfilerData.TUFXActivePassCount}</color> | {ProfilerI18n.Get("tufx_post_process_ms")}: <color=#00e5ff>{ProfilerData.TUFXPostProcessMs:F2} ms</color> | {ProfilerI18n.Get("tufx_cpu_build_ms")}: <color=lime>{ProfilerData.TUFXCpuBuildMs:F2} ms</color>", tipStyle);
                GUILayout.EndHorizontal();
                GUILayout.Space(2);
            }

            // Search Bar & Sort Lock
            GUILayout.BeginHorizontal();
            GUILayout.Label(ProfilerI18n.Get("search_placeholder"), GUILayout.Width(150));
            searchPlugins = GUILayout.TextField(searchPlugins, GUILayout.Width(240));
            if (!string.IsNullOrEmpty(searchPlugins) && GUILayout.Button(ProfilerI18n.Get("clear_search"), GUILayout.Width(60)))
            {
                searchPlugins = "";
            }
            GUILayout.Space(8);
            string sortLockText = isSortLocked ? ProfilerI18n.Get("sort_lock_on") : ProfilerI18n.Get("sort_lock_off");
            if (GUILayout.Button(sortLockText, GUILayout.Width(110)))
            {
                isSortLocked = !isSortLocked;
            }
            if (isSortLocked && GUILayout.Button(ProfilerI18n.Get("sort_refresh_now"), GUILayout.Width(100)))
            {
                cachedTopPlugins = ProfilerData.GetTopPlugins(60, searchPlugins, sortPluginsCol, sortPluginsAsc);
                lastTableUpdateTime = Time.realtimeSinceStartup;
            }
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();

            GUILayout.Space(4);

            // Table Header with Sortable Buttons
            GUILayout.BeginHorizontal();
            if (GUILayout.Button(GetHeaderTitle("col_plugin_class", 0, sortPluginsCol, sortPluginsAsc), tableHeaderBtnStyle, GUILayout.MinWidth(240), GUILayout.ExpandWidth(true)))
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

            scrollPosPlugins = GUILayout.BeginScrollView(scrollPosPlugins, false, false, GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));

            float now = Time.realtimeSinceStartup;
            bool sortChanged = searchPlugins != lastSearchPlugins ||
                               sortPluginsCol != lastSortPluginsCol ||
                               sortPluginsAsc != lastSortPluginsAsc;

            bool shouldReorder = cachedTopPlugins.Count == 0 || sortChanged || (!isSortLocked && (now - lastTableUpdateTime >= 1.5f));

            if (shouldReorder)
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
                double pct = (p.DisplayMs / totalFrameMs) * 100.0;
                string colorStr = p.DisplayMs > 4.0 ? "#FF4444" : (p.DisplayMs > 1.2 ? "#FFAA22" : "#FFFFFF");
                string displayTitle = p.IsTUFX
                    ? $"<color=#00e5ff>[TUFX]</color> <color={colorStr}>{p.FriendlyName}</color>"
                    : $"<color={colorStr}>{p.TypeName}</color>";

                string typeKey = "tab1::" + p.TypeName;
                bool isExp = expandedTypes.Contains(typeKey);
                string expIcon = p.Methods.Count > 0 ? (isExp ? "▼ " : "▶ ") : "  ";

                GUILayout.BeginHorizontal(i % 2 == 0 ? "box" : GUIStyle.none);
                if (GUILayout.Button(expIcon + displayTitle, headerStyle, GUILayout.MinWidth(240), GUILayout.ExpandWidth(true)))
                {
                    if (p.Methods.Count > 0)
                    {
                        if (isExp) expandedTypes.Remove(typeKey);
                        else expandedTypes.Add(typeKey);
                    }
                }

                // Avg Ms + Micro Bar
                GUILayout.BeginHorizontal(GUILayout.Width(110));
                GUILayout.Label($"{p.DisplayMs:F2} ms", GUILayout.Width(62));
                DrawMicroBar(p.DisplayMs, totalFrameMs, 40f);
                GUILayout.EndHorizontal();

                GUILayout.Label($"{p.DisplayPeakMs:F2} ms", GUILayout.Width(100));
                GUILayout.Label($"{p.CurrentFrameCalls}", GUILayout.Width(100));
                GUILayout.Label($"{pct:F1}%", GUILayout.Width(110));
                GUILayout.Label($"{p.AssemblyName}", GUILayout.Width(170));
                GUILayout.EndHorizontal();

                if (isExp && p.Methods.Count > 0)
                {
                    foreach (var meth in p.GetSortedMethods())
                    {
                        if (meth.SmoothMs < 0.0005 && meth.CurrentFrameCalls == 0) continue;
                        string mColor = meth.DisplayMs > 2.0 ? "#FF5555" : (meth.DisplayMs > 0.5 ? "#FFBB33" : "#88BBDD");
                        GUILayout.BeginHorizontal();
                        GUILayout.Space(24);
                        GUILayout.Label($"<color={mColor}>· {meth.MethodName}()</color>", tipStyle, GUILayout.MinWidth(216), GUILayout.ExpandWidth(true));

                        GUILayout.BeginHorizontal(GUILayout.Width(110));
                        GUILayout.Label($"{meth.DisplayMs:F2} ms", GUILayout.Width(62));
                        DrawMicroBar(meth.DisplayMs, totalFrameMs, 40f);
                        GUILayout.EndHorizontal();

                        GUILayout.Label($"{meth.DisplayPeakMs:F2} ms", GUILayout.Width(100));
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
        }

        private void DrawModulesContent()
        {

            // Search Bar & Sort Lock
            GUILayout.BeginHorizontal();
            GUILayout.Label(ProfilerI18n.Get("search_placeholder"), GUILayout.Width(150));
            searchModules = GUILayout.TextField(searchModules, GUILayout.Width(240));
            if (!string.IsNullOrEmpty(searchModules) && GUILayout.Button(ProfilerI18n.Get("clear_search"), GUILayout.Width(60)))
            {
                searchModules = "";
            }
            GUILayout.Space(8);
            string sortLockText = isSortLocked ? ProfilerI18n.Get("sort_lock_on") : ProfilerI18n.Get("sort_lock_off");
            if (GUILayout.Button(sortLockText, GUILayout.Width(110)))
            {
                isSortLocked = !isSortLocked;
            }
            if (isSortLocked && GUILayout.Button(ProfilerI18n.Get("sort_refresh_now"), GUILayout.Width(100)))
            {
                cachedTopModules = ProfilerData.GetTopModules(60, searchModules, sortModulesCol, sortModulesAsc);
                lastTableUpdateTime = Time.realtimeSinceStartup;
            }
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();

            GUILayout.Space(4);

            // Table Header with Sortable Buttons
            GUILayout.BeginHorizontal();
            if (GUILayout.Button(GetHeaderTitle("col_module_type", 0, sortModulesCol, sortModulesAsc), tableHeaderBtnStyle, GUILayout.MinWidth(240), GUILayout.ExpandWidth(true)))
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

            scrollPosModules = GUILayout.BeginScrollView(scrollPosModules, false, false, GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));

            float now = Time.realtimeSinceStartup;
            bool sortChanged = searchModules != lastSearchModules ||
                               sortModulesCol != lastSortModulesCol ||
                               sortModulesAsc != lastSortModulesAsc;

            bool shouldReorder = cachedTopModules.Count == 0 || sortChanged || (!isSortLocked && (now - lastTableUpdateTime >= 1.5f));

            if (shouldReorder)
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
                double pct = (m.DisplayMs / totalFrameMs) * 100.0;
                string colorStr = m.DisplayMs > 4.0 ? "#FF4444" : (m.DisplayMs > 1.2 ? "#FFAA22" : "#FFFFFF");

                string typeKey = "tab2::" + m.TypeName;
                bool isExp = expandedTypes.Contains(typeKey);
                string expIcon = m.Methods.Count > 0 ? (isExp ? "▼ " : "▶ ") : "  ";

                GUILayout.BeginHorizontal(i % 2 == 0 ? "box" : GUIStyle.none);
                if (GUILayout.Button($"{expIcon}<color={colorStr}>{m.TypeName}</color>", headerStyle, GUILayout.MinWidth(240), GUILayout.ExpandWidth(true)))
                {
                    if (m.Methods.Count > 0)
                    {
                        if (isExp) expandedTypes.Remove(typeKey);
                        else expandedTypes.Add(typeKey);
                    }
                }

                // Avg Ms + Micro Bar
                GUILayout.BeginHorizontal(GUILayout.Width(110));
                GUILayout.Label($"{m.DisplayMs:F2} ms", GUILayout.Width(62));
                DrawMicroBar(m.DisplayMs, totalFrameMs, 40f);
                GUILayout.EndHorizontal();

                GUILayout.Label($"{m.DisplayPeakMs:F2} ms", GUILayout.Width(100));
                GUILayout.Label($"{m.CurrentFrameCalls}", GUILayout.Width(100));
                GUILayout.Label($"{pct:F1}%", GUILayout.Width(110));
                GUILayout.Label($"{m.AssemblyName}", GUILayout.Width(170));
                GUILayout.EndHorizontal();

                if (isExp && m.Methods.Count > 0)
                {
                    foreach (var meth in m.GetSortedMethods())
                    {
                        if (meth.SmoothMs < 0.0005 && meth.CurrentFrameCalls == 0) continue;
                        string mColor = meth.DisplayMs > 2.0 ? "#FF5555" : (meth.DisplayMs > 0.5 ? "#FFBB33" : "#88BBDD");
                        GUILayout.BeginHorizontal();
                        GUILayout.Space(24);
                        GUILayout.Label($"<color={mColor}>· {meth.MethodName}()</color>", tipStyle, GUILayout.MinWidth(216), GUILayout.ExpandWidth(true));

                        GUILayout.BeginHorizontal(GUILayout.Width(110));
                        GUILayout.Label($"{meth.DisplayMs:F2} ms", GUILayout.Width(62));
                        DrawMicroBar(meth.DisplayMs, totalFrameMs, 40f);
                        GUILayout.EndHorizontal();

                        GUILayout.Label($"{meth.DisplayPeakMs:F2} ms", GUILayout.Width(100));
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
        }

        #endregion

        #region Tab 2: Parts & Vessels Table

        private void DrawPartsTab()
        {
            GUILayout.BeginVertical(cardStyle);

            // Search Bar & Sort Lock
            GUILayout.BeginHorizontal();
            GUILayout.Label(ProfilerI18n.Get("search_placeholder"), GUILayout.Width(150));
            searchParts = GUILayout.TextField(searchParts, GUILayout.Width(240));
            if (!string.IsNullOrEmpty(searchParts) && GUILayout.Button(ProfilerI18n.Get("clear_search"), GUILayout.Width(60)))
            {
                searchParts = "";
            }
            GUILayout.Space(8);
            string sortLockText = isSortLocked ? ProfilerI18n.Get("sort_lock_on") : ProfilerI18n.Get("sort_lock_off");
            if (GUILayout.Button(sortLockText, GUILayout.Width(110)))
            {
                isSortLocked = !isSortLocked;
            }
            if (isSortLocked && GUILayout.Button(ProfilerI18n.Get("sort_refresh_now"), GUILayout.Width(100)))
            {
                cachedTopParts = ProfilerData.GetTopParts(50, searchParts, sortPartsCol, sortPartsAsc);
                lastTableUpdateTime = Time.realtimeSinceStartup;
            }
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();

            GUILayout.Space(4);

            // Table Header with Sortable Buttons
            GUILayout.BeginHorizontal();
            if (GUILayout.Button(GetHeaderTitle("col_part_title", 0, sortPartsCol, sortPartsAsc), tableHeaderBtnStyle, GUILayout.MinWidth(280), GUILayout.ExpandWidth(true)))
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

            scrollPosParts = GUILayout.BeginScrollView(scrollPosParts, false, false, GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));

            float now = Time.realtimeSinceStartup;
            bool sortChanged = searchParts != lastSearchParts ||
                               sortPartsCol != lastSortPartsCol ||
                               sortPartsAsc != lastSortPartsAsc;

            bool shouldReorder = cachedTopParts.Count == 0 || sortChanged || (!isSortLocked && (now - lastTableUpdateTime >= 1.5f));

            if (shouldReorder)
            {
                cachedTopParts = ProfilerData.GetTopParts(50, searchParts, sortPartsCol, sortPartsAsc);
                lastSearchParts = searchParts;
                lastSortPartsCol = sortPartsCol;
                lastSortPartsAsc = sortPartsAsc;
                lastTableUpdateTime = now;
            }

            var topParts = cachedTopParts;
            double totalFrameMs = Math.Max(0.001, ProfilerData.SmoothTotalFrameMs);

            for (int i = 0; i < topParts.Count; i++)
            {
                PartStats p = topParts[i];
                string colorStr = p.DisplayMs > 2.0 ? "#FF4444" : (p.DisplayMs > 0.8 ? "#FFAA22" : "#FFFFFF");

                GUILayout.BeginHorizontal(i % 2 == 0 ? "box" : GUIStyle.none);
                GUILayout.Label($"<color={colorStr}>{p.PartTitle}</color>", headerStyle, GUILayout.MinWidth(280), GUILayout.ExpandWidth(true));
                GUILayout.Label(p.VesselName, GUILayout.Width(320));

                GUILayout.BeginHorizontal(GUILayout.Width(160));
                GUILayout.Label($"{p.DisplayMs:F2} ms", GUILayout.Width(70));
                DrawMicroBar(p.DisplayMs, totalFrameMs, 60f);
                GUILayout.EndHorizontal();

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
            bool sortChanged = searchAssembly != lastSearchAssembly ||
                               sortAssemblyCol != lastSortAssemblyCol ||
                               sortAssemblyAsc != lastSortAssemblyAsc;

            bool shouldReorder = cachedAssemblyStats.Count == 0 || sortChanged || (!isSortLocked && (now - lastAssemblyUpdateTime >= 1.5f));

            if (shouldReorder)
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
            Rect barRect = GUILayoutUtility.GetRect(0, 16, GUILayout.ExpandWidth(true));
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
                float pct = (float)(asmList[i].DisplayMs / totalFrameMs);
                float segW = totalBarW * pct;
                if (segW < 0.5f) continue;
                Color c = (i < asmBarColors.Length - 1) ? asmBarColors[i] : asmBarColors[asmBarColors.Length - 1];
                GUI.color = c;
                GUI.DrawTexture(new Rect(currentX, barY, segW, barH), whitePixelTex);
                currentX += segW;
                drawnMs += asmList[i].DisplayMs;
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
                GUILayout.Label($"<color={hex}>■</color> <b>{shortName}:</b> {asmList[i].DisplayMs:F1}ms ({asmList[i].PctOfFrame:F1}%)", tipStyle);
            }
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();

            GUILayout.Space(4);

            // === Search Bar & Sort Lock ===
            GUILayout.BeginHorizontal();
            GUILayout.Label(ProfilerI18n.Get("search_placeholder"), GUILayout.Width(150));
            searchAssembly = GUILayout.TextField(searchAssembly, GUILayout.Width(220));
            if (!string.IsNullOrEmpty(searchAssembly) && GUILayout.Button(ProfilerI18n.Get("clear_search"), GUILayout.Width(50)))
            {
                searchAssembly = "";
            }
            GUILayout.Space(6);
            string sortLockText = isSortLocked ? ProfilerI18n.Get("sort_lock_on") : ProfilerI18n.Get("sort_lock_off");
            if (GUILayout.Button(sortLockText, GUILayout.Width(110)))
            {
                isSortLocked = !isSortLocked;
            }
            if (isSortLocked && GUILayout.Button(ProfilerI18n.Get("sort_refresh_now"), GUILayout.Width(100)))
            {
                cachedAssemblyStats = ProfilerData.GetAssemblyBreakdown(searchAssembly, sortAssemblyCol, sortAssemblyAsc);
                lastAssemblyUpdateTime = Time.realtimeSinceStartup;
            }
            GUILayout.Space(6);
            if (GUILayout.Button(groupBySubsystems ? ProfilerI18n.Get("subsystem_toggle_on") : ProfilerI18n.Get("subsystem_toggle_off"), GUILayout.Width(160)))
            {
                groupBySubsystems = !groupBySubsystems;
            }
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();

            GUILayout.Space(4);

            // === Table Header ===
            GUILayout.BeginHorizontal();
            if (GUILayout.Button(GetHeaderTitle("col_assembly_name", 0, sortAssemblyCol, sortAssemblyAsc), tableHeaderBtnStyle, GUILayout.MinWidth(240), GUILayout.ExpandWidth(true)))
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
            scrollPosAssembly = GUILayout.BeginScrollView(scrollPosAssembly, false, false, GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));

            for (int i = 0; i < asmList.Count; i++)
            {
                var asm = asmList[i];
                string colorStr = asm.SmoothMs > 5.0 ? "#FF4444" : (asm.SmoothMs > 1.5 ? "#FFAA22" : "#FFFFFF");
                bool isExpanded = expandedAssemblies.Contains(asm.AssemblyName);

                string expandIcon = isExpanded ? "▼" : "▶";

                GUILayout.BeginHorizontal(i % 2 == 0 ? "box" : GUIStyle.none);

                // Clickable expand/collapse assembly name
                if (GUILayout.Button($"{expandIcon} <color={colorStr}><b>{asm.AssemblyName}</b></color>", headerStyle, GUILayout.MinWidth(240), GUILayout.ExpandWidth(true)))
                {
                    if (isExpanded)
                        expandedAssemblies.Remove(asm.AssemblyName);
                    else
                        expandedAssemblies.Add(asm.AssemblyName);
                }
                GUILayout.BeginHorizontal(GUILayout.Width(110));
                GUILayout.Label($"{asm.DisplayMs:F2} ms", GUILayout.Width(62));
                DrawMicroBar(asm.DisplayMs, totalFrameMs, 40f);
                GUILayout.EndHorizontal();

                GUILayout.Label($"{asm.DisplayPeakMs:F2} ms", GUILayout.Width(100));
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

                        string nsColorStr = ns.DisplayMs > 3.0 ? "#FF6666" : (ns.DisplayMs > 0.8 ? "#FFCC44" : "#AACCFF");
                        string nsKey = asm.AssemblyName + "::" + ns.Namespace;
                        bool nsExpanded = expandedNamespaces.Contains(nsKey);
                        string nsIcon = nsExpanded ? "  ▼" : "  ▶";

                        GUILayout.BeginHorizontal();
                        GUILayout.Space(20);

                        // Clickable namespace row
                        string nsDisplayName = ns.Namespace;
                        if (nsDisplayName.Length > 30) nsDisplayName = nsDisplayName.Substring(0, 27) + "...";

                        if (GUILayout.Button($"{nsIcon} <color={nsColorStr}>{nsDisplayName}</color>", tipStyle, GUILayout.MinWidth(220), GUILayout.ExpandWidth(true)))
                        {
                            if (nsExpanded)
                                expandedNamespaces.Remove(nsKey);
                            else
                                expandedNamespaces.Add(nsKey);
                        }

                        GUILayout.BeginHorizontal(GUILayout.Width(110));
                        GUILayout.Label($"{ns.DisplayMs:F2} ms", GUILayout.Width(62));
                        DrawMicroBar(ns.DisplayMs, totalFrameMs, 40f);
                        GUILayout.EndHorizontal();

                        GUILayout.Label($"{ns.DisplayPeakMs:F2} ms", GUILayout.Width(100));
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
                                    string subColor = sub.DisplayMs > 2.0 ? "#FF8888" : (sub.DisplayMs > 0.5 ? "#FFDD66" : "#CCDDEE");

                                    GUILayout.BeginHorizontal();
                                    GUILayout.Space(36);
                                    if (GUILayout.Button($"<color={subColor}><b>{subIcon}{sub.DisplayName}</b></color>", tipStyle, GUILayout.MinWidth(204), GUILayout.ExpandWidth(true)))
                                    {
                                        if (subExp) expandedSubsystems.Remove(subKey);
                                        else expandedSubsystems.Add(subKey);
                                    }

                                    GUILayout.BeginHorizontal(GUILayout.Width(110));
                                    GUILayout.Label($"{sub.DisplayMs:F2} ms", GUILayout.Width(62));
                                    DrawMicroBar(sub.DisplayMs, totalFrameMs, 40f);
                                    GUILayout.EndHorizontal();

                                    GUILayout.Label($"{sub.DisplayPeakMs:F2} ms", GUILayout.Width(100));
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

            string tColorStr = t.DisplayMs > 2.0 ? "#FF5555" : (t.DisplayMs > 0.5 ? "#FFBB33" : "#88BBDD");

            GUILayout.BeginHorizontal();
            GUILayout.Space(indent);

            int nameWidth = Math.Max(120, 240 - indent);
            if (GUILayout.Button($"<color={tColorStr}>{typeIcon}{t.TypeName}</color>", tipStyle, GUILayout.MinWidth(nameWidth), GUILayout.ExpandWidth(true)))
            {
                if (hasMethods)
                {
                    if (typeExpanded) expandedTypes.Remove(typeKey);
                    else expandedTypes.Add(typeKey);
                }
            }

            GUILayout.BeginHorizontal(GUILayout.Width(110));
            GUILayout.Label($"{t.DisplayMs:F2} ms", GUILayout.Width(62));
            DrawMicroBar(t.DisplayMs, ProfilerData.SmoothTotalFrameMs, 40f);
            GUILayout.EndHorizontal();

            GUILayout.Label($"{t.DisplayPeakMs:F2} ms", GUILayout.Width(100));
            GUILayout.Label(ProfilerI18n.Format("unit_calls", t.CurrentFrameCalls), GUILayout.Width(100));
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
                    string mColor = meth.DisplayMs > 1.5 ? "#FFAA22" : "#99DDFF";

                    GUILayout.BeginHorizontal();
                    GUILayout.Space(indent + 16);

                    int methWidth = Math.Max(100, 240 - indent - 16);
                    if (GUILayout.Button($"<color={mColor}>{methIcon}{meth.MethodName}()</color>", tipStyle, GUILayout.MinWidth(methWidth), GUILayout.ExpandWidth(true)))
                    {
                        if (hasSubs)
                        {
                            if (methExpanded) expandedMethods.Remove(methKey);
                            else expandedMethods.Add(methKey);
                        }
                    }

                    GUILayout.BeginHorizontal(GUILayout.Width(110));
                    GUILayout.Label($"{meth.DisplayMs:F2} ms", GUILayout.Width(62));
                    DrawMicroBar(meth.DisplayMs, ProfilerData.SmoothTotalFrameMs, 40f);
                    GUILayout.EndHorizontal();

                    GUILayout.Label($"{meth.DisplayPeakMs:F2} ms", GUILayout.Width(100));
                    GUILayout.Label(ProfilerI18n.Format("unit_calls", meth.CurrentFrameCalls), GUILayout.Width(100));
                    GUILayout.EndHorizontal();

                    // Expanded Dispatcher Sub-Invocations (e.g. Principia inside TimingPre)
                    if (methExpanded && hasSubs)
                    {
                        for (int sIdx = 0; sIdx < subs.Count; sIdx++)
                        {
                            var sub = subs[sIdx];
                            if (sub.SmoothMs < 0.0005 && sub.CurrentFrameCalls == 0) continue;

                            string sColor = sub.DisplayMs > 1.0 ? "#FF5555" : (sub.DisplayMs > 0.3 ? "#FFAA22" : "#55FF88");

                            GUILayout.BeginHorizontal();
                            GUILayout.Space(indent + 32);

                            string subDisplay = $"⚡ <color=#00e5ff>[{sub.AssemblyName}]</color> {sub.TypeName}.{sub.MethodName}";
                            int subWidth = Math.Max(140, 360 - indent - 32);
                            GUILayout.Label($"<color={sColor}>{subDisplay}</color>", tipStyle, GUILayout.MinWidth(subWidth), GUILayout.ExpandWidth(true));

                            GUILayout.BeginHorizontal(GUILayout.Width(110));
                            GUILayout.Label($"{sub.DisplayMs:F2} ms", GUILayout.Width(62));
                            DrawMicroBar(sub.DisplayMs, ProfilerData.SmoothTotalFrameMs, 40f);
                            GUILayout.EndHorizontal();

                            GUILayout.Label($"{sub.DisplayPeakMs:F2} ms", GUILayout.Width(100));
                            GUILayout.Label(ProfilerI18n.Format("unit_calls", sub.CurrentFrameCalls), GUILayout.Width(100));
                            GUILayout.EndHorizontal();
                        }
                    }
                }
            }
        }

        #endregion

        #region Tab 4: Optimization Guide & Settings

        private void DrawSettingsAndHelpTab()
        {
            GUILayout.BeginVertical(cardStyle);
            scrollPosHelp = GUILayout.BeginScrollView(scrollPosHelp, false, false, GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));

            // Section 1: Quick Maintenance Actions Card
            GUILayout.BeginVertical("box");
            GUILayout.Label($"<b>⚙️ {ProfilerI18n.Get("tab_settings")}</b>", headerStyle);
            GUILayout.Space(2);
            GUILayout.BeginHorizontal();

            if (GUILayout.Button($"📋 {ProfilerI18n.Get("export_dump")}", GUILayout.Width(170), GUILayout.Height(25)))
            {
                PhysProfilerPlugin.Instance.ExportReportToFile();
            }
            if (GUILayout.Button($"🔄 {ProfilerI18n.Get("reset_peak")}", GUILayout.Width(130), GUILayout.Height(25)))
            {
                ProfilerData.ResetAllPeakData();
            }
            if (GUILayout.Button(ProfilerI18n.GetCurrentLanguageButtonText(), GUILayout.Width(170), GUILayout.Height(25)))
            {
                ProfilerI18n.ToggleNextLanguage();
            }

            GUILayout.FlexibleSpace();
            GUILayout.Label(string.Format(ProfilerI18n.Get("patched_stats"), HarmonyPatches.PatchedModuleCount, HarmonyPatches.PatchedPluginCount), tipStyle);
            GUILayout.EndHorizontal();
            GUILayout.EndVertical();

            GUILayout.Space(6);

            // Section 1.5: Dedicated Language Selection & Dynamic Hot-Reload Card
            GUILayout.BeginVertical("box");
            GUILayout.BeginHorizontal();
            GUILayout.Label($"<b>🌐 {ProfilerI18n.Get("lang_label")} ({ProfilerI18n.AvailablePacks.Count})</b>", headerStyle);
            GUILayout.FlexibleSpace();
            if (GUILayout.Button(ProfilerI18n.IsChinese ? "🔄 重新扫描语言包" : "🔄 Reload Language Packs", GUILayout.Height(22)))
            {
                ProfilerI18n.ReloadLanguagePacks();
            }
            GUILayout.EndHorizontal();
            GUILayout.Space(2);

            string[] langOptions = new string[ProfilerI18n.AvailablePacks.Count + 1];
            var activePack = ProfilerI18n.GetActiveLanguagePack();
            langOptions[0] = ProfilerI18n.CurrentPackIndex == -1 && activePack != null
                ? $"🌐 {ProfilerI18n.Get("lang_auto")} ({activePack.Name})"
                : $"🌐 {ProfilerI18n.Get("lang_auto")}";
            for (int i = 0; i < ProfilerI18n.AvailablePacks.Count; i++)
            {
                langOptions[i + 1] = ProfilerI18n.AvailablePacks[i].Name;
            }

            int curSelected = ProfilerI18n.CurrentPackIndex + 1;
            int newSelected = GUILayout.Toolbar(curSelected, langOptions, GUILayout.Height(26));
            if (newSelected != curSelected)
            {
                ProfilerI18n.CurrentPackIndex = newSelected - 1;
            }
            GUILayout.EndVertical();

            GUILayout.Space(6);

            // Section 2: Mono Heap Padder & GC Stutter Management Card
            GUILayout.BeginVertical("box");
            GUILayout.Label($"<b>{ProfilerI18n.Get("pad_card_title")}</b>", headerStyle);
            GUILayout.Label(ProfilerI18n.Get("pad_card_desc"), tipStyle);
            GUILayout.Space(4);

            // Machine Physical RAM Banner & Intelligent Recommendation
            int ramMb = MonoHeapPadder.SystemRamMb;
            int ramGb = Math.Max(1, (int)Math.Round(ramMb / 1024.0));
            int recMb = MonoHeapPadder.RecommendedPadMb;
            int recGb = Math.Max(1, recMb / 1024);
            int maxSafeMb = MonoHeapPadder.MaxSafePadMb;

            GUILayout.BeginVertical("box");
            GUILayout.Label(ProfilerI18n.Format("pad_ram_banner", ramGb, ramMb, recMb, recGb, maxSafeMb), headerStyle);
            GUILayout.Label(ProfilerI18n.Get("pad_ram_tip"), tipStyle);
            GUILayout.EndVertical();
            GUILayout.Space(4);

            // Realtime heap status display
            double heapMb = MonoHeapPadder.CurrentHeapMb;
            double usedMb = MonoHeapPadder.GameUsedMb;
            double padMb = MonoHeapPadder.PaddedMb;
            double freeMb = MonoHeapPadder.CurrentFreeMb;
            string freeColor = (freeMb >= 800.0 || padMb >= 1000.0) ? "#33FF33" : (freeMb >= 300.0 ? "#FFFF33" : "#FF3333");
            
            GUILayout.Label(ProfilerI18n.Format("pad_heap_stat", heapMb, usedMb, freeColor, freeMb, ramGb, padMb), headerStyle);
            GUILayout.Space(6);

            // Row 1: Presets & One-click Recommendation
            GUILayout.BeginHorizontal();
            GUILayout.Label("<b>预设梯度:</b>", tipStyle, GUILayout.Width(65));

            int[] presets = new int[] { 1024, 2048, 4096, 6144 };
            for (int i = 0; i < presets.Length; i++)
            {
                int mb = presets[i];
                if (mb > maxSafeMb && maxSafeMb > 1024) continue;
                bool isCurrent = MonoHeapPadder.TargetPadMb == mb && MonoHeapPadder.IsPadded;
                Color prevBg = GUI.backgroundColor;
                if (isCurrent) GUI.backgroundColor = new Color(0.2f, 0.9f, 0.5f, 1f);

                string btnText = ProfilerI18n.Format("pad_btn_preset", mb, mb / 1024);
                if (GUILayout.Button(btnText, GUILayout.Width(135), GUILayout.Height(25)))
                {
                    MonoHeapPadder.TargetPadMb = mb;
                    customPadMbInput = mb.ToString();
                    padValidationMsg = "";
                    MonoHeapPadder.Pad(mb);
                }
                GUI.backgroundColor = prevBg;
                GUILayout.Space(4);
            }

            // Quick Apply Recommended Value
            Color prevBgRec = GUI.backgroundColor;
            GUI.backgroundColor = new Color(0.3f, 0.85f, 0.95f, 1f);
            if (GUILayout.Button(ProfilerI18n.Format("pad_btn_use_rec", recMb), GUILayout.Width(170), GUILayout.Height(25)))
            {
                MonoHeapPadder.TargetPadMb = recMb;
                customPadMbInput = recMb.ToString();
                padValidationMsg = "";
                MonoHeapPadder.Pad(recMb);
            }
            GUI.backgroundColor = prevBgRec;

            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();

            GUILayout.Space(6);

            // Row 2: Manual Custom Value Input with Quick Steppers
            if (string.IsNullOrEmpty(customPadMbInput))
            {
                customPadMbInput = (MonoHeapPadder.TargetPadMb > 0 ? MonoHeapPadder.TargetPadMb : recMb).ToString();
            }

            GUILayout.BeginHorizontal();
            GUILayout.Label($"<b>{ProfilerI18n.Get("pad_custom_label")}</b>", tipStyle, GUILayout.Width(155));

            if (GUILayout.Button("-1024", GUILayout.Width(50), GUILayout.Height(24)))
            {
                if (int.TryParse(customPadMbInput, out int curVal))
                {
                    customPadMbInput = Math.Max(256, curVal - 1024).ToString();
                }
            }
            if (GUILayout.Button("-512", GUILayout.Width(45), GUILayout.Height(24)))
            {
                if (int.TryParse(customPadMbInput, out int curVal))
                {
                    customPadMbInput = Math.Max(256, curVal - 512).ToString();
                }
            }

            customPadMbInput = GUILayout.TextField(customPadMbInput, GUILayout.Width(75), GUILayout.Height(24));

            if (GUILayout.Button("+512", GUILayout.Width(45), GUILayout.Height(24)))
            {
                if (int.TryParse(customPadMbInput, out int curVal))
                {
                    customPadMbInput = Math.Min(maxSafeMb, curVal + 512).ToString();
                }
            }
            if (GUILayout.Button("+1024", GUILayout.Width(50), GUILayout.Height(24)))
            {
                if (int.TryParse(customPadMbInput, out int curVal))
                {
                    customPadMbInput = Math.Min(maxSafeMb, curVal + 1024).ToString();
                }
            }

            GUILayout.Space(6);

            // Apply Custom Value Button
            Color prevBgApply = GUI.backgroundColor;
            GUI.backgroundColor = new Color(0.2f, 0.9f, 0.5f, 1f);
            if (GUILayout.Button($"<b>{ProfilerI18n.Get("pad_custom_apply")}</b>", GUILayout.Width(150), GUILayout.Height(25)))
            {
                if (int.TryParse(customPadMbInput, out int val) && val >= 256 && val <= maxSafeMb)
                {
                    padValidationMsg = "";
                    MonoHeapPadder.TargetPadMb = val;
                    MonoHeapPadder.Pad(val);
                }
                else
                {
                    padValidationMsg = ProfilerI18n.Format("pad_custom_err_range", maxSafeMb);
                }
            }
            GUI.backgroundColor = prevBgApply;

            GUILayout.Space(6);

            // Release Padding Button (if active)
            if (MonoHeapPadder.IsPadded)
            {
                Color prevBgRel = GUI.backgroundColor;
                GUI.backgroundColor = new Color(1f, 0.5f, 0.3f, 1f);
                if (GUILayout.Button($"<b>{ProfilerI18n.Get("pad_btn_release")}</b>", GUILayout.Width(100), GUILayout.Height(25)))
                {
                    MonoHeapPadder.ReleasePadding();
                }
                GUI.backgroundColor = prevBgRel;
                GUILayout.Space(6);
            }

            // Force GC Button
            if (GUILayout.Button($"<b>{ProfilerI18n.Get("pad_force_gc")}</b>", GUILayout.Width(220), GUILayout.Height(25)))
            {
                MonoHeapPadder.ForceGarbageCollection();
            }

            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();

            // Validation warning line if input out of bounds
            if (!string.IsNullOrEmpty(padValidationMsg))
            {
                GUILayout.Space(2);
                GUILayout.Label($"<color=#ff5555>⚠️ {padValidationMsg}</color>", tipStyle);
            }

            GUILayout.Space(6);

            // Toggles: AutoPad on scene change & Hotkey
            GUILayout.BeginHorizontal();
            
            bool newAuto = GUILayout.Toggle(MonoHeapPadder.AutoPadOnSceneChange, $" <b>{ProfilerI18n.Get("pad_auto_toggle")}</b>", GUILayout.Width(260));
            if (newAuto != MonoHeapPadder.AutoPadOnSceneChange)
            {
                MonoHeapPadder.AutoPadOnSceneChange = newAuto;
                MonoHeapPadder.SaveConfig();
            }

            bool newHotkey = GUILayout.Toggle(MonoHeapPadder.EnableHotkey, $" <b>{ProfilerI18n.Get("pad_hotkey_toggle")}</b>", GUILayout.Width(280));
            if (newHotkey != MonoHeapPadder.EnableHotkey)
            {
                MonoHeapPadder.EnableHotkey = newHotkey;
                MonoHeapPadder.SaveConfig();
            }

            GUILayout.FlexibleSpace();

            // Status message
            if (!string.IsNullOrEmpty(MonoHeapPadder.LastStatusMessage))
            {
                GUILayout.Label(MonoHeapPadder.LastStatusMessage, tipStyle);
            }

            GUILayout.EndHorizontal();
            GUILayout.EndVertical();

            GUILayout.Space(6);

            // Section 3: Concrete Lag Countermeasures
            GUILayout.BeginVertical("box");
            GUILayout.Label($"<b>💡 {ProfilerI18n.Get("help_mod_tuning_q")}</b>", headerStyle);
            GUILayout.Label(ProfilerI18n.Get("help_mod_tuning_a"), tipStyle);
            GUILayout.EndVertical();

            GUILayout.Space(6);

            // Section 3: Performance Metrics Reference
            GUILayout.Label($"<b>{ProfilerI18n.Get("help_h1")}</b>", headerStyle);
            GUILayout.Space(2);

            // PTR Explanation
            GUILayout.BeginVertical("box");
            GUILayout.Label($"<b>⏱️ {ProfilerI18n.Get("help_ptr_q")}</b>", headerStyle);
            GUILayout.Label(ProfilerI18n.Get("help_ptr_a"), tipStyle);
            GUILayout.EndVertical();

            GUILayout.Space(4);

            // 1% Low Explanation
            GUILayout.BeginVertical("box");
            GUILayout.Label($"<b>📊 {ProfilerI18n.Get("help_1pct_q")}</b>", headerStyle);
            GUILayout.Label(ProfilerI18n.Get("help_1pct_a"), tipStyle);
            GUILayout.EndVertical();

            GUILayout.Space(4);

            // Jitter Explanation
            GUILayout.BeginVertical("box");
            GUILayout.Label($"<b>〰️ {ProfilerI18n.Get("help_jitter_q")}</b>", headerStyle);
            GUILayout.Label(ProfilerI18n.Get("help_jitter_a"), tipStyle);
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

            int sampleCount = ProfilerData.GetFrameHistorySample(miniSparklineBuffer, 50);
            if (sampleCount > 1)
            {
                float maxMs = 70f;
                float innerW = sparkRect.width - 8;
                float innerH = sparkRect.height - 8;
                float innerX = sparkRect.x + 4;
                float innerY = sparkRect.y + 4;

                for (int i = 0; i < sampleCount; i++)
                {
                    float ms = (float)miniSparklineBuffer[i];
                    float barH = Mathf.Clamp((ms / maxMs) * innerH, 2f, innerH);
                    float barW = innerW / sampleCount;
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
