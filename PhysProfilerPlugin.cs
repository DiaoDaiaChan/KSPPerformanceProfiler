using System;
using System.Collections;
using System.IO;
using KSP.UI.Screens;
using UnityEngine;

namespace KSPPerformanceProfiler
{
    [KSPAddon(KSPAddon.Startup.EveryScene, false)]
    public class PhysProfilerPlugin : MonoBehaviour
    {
        public static PhysProfilerPlugin Instance { get; private set; }

        private ApplicationLauncherButton appButton;
        private bool isUiVisible = false;
        private bool isUpdatingAppButton = false;

        private void Awake()
        {
            if (Instance != null)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);

            UnityEngine.Debug.Log("[KSPPerformanceProfiler] Plugin Initializing...");
            HarmonyPatches.ApplyPatches();

            if (gameObject.GetComponent<ProfilerUI>() == null)
            {
                gameObject.AddComponent<ProfilerUI>();
            }
        }

        private void Start()
        {
            MonoHeapPadder.Initialize();
            StartCoroutine(EndOfFixedUpdateRoutine());
            GameEvents.onGUIApplicationLauncherReady.Add(OnAppLauncherReady);
            GameEvents.onGUIApplicationLauncherDestroyed.Add(OnAppLauncherDestroyed);

            GameEvents.onGameSceneSwitchRequested.Add(OnSceneSwitchRequested);
            GameEvents.onLevelWasLoadedGUIReady.Add(OnLevelLoadedGUIReady);
            GameEvents.onVesselChange.Add(OnVesselChange);
            GameEvents.onVesselWillDestroy.Add(OnVesselWillDestroy);

            Camera.onPreRender += OnCameraPreRender;
            Camera.onPostRender += OnCameraPostRender;

            if (ApplicationLauncher.Ready)
            {
                OnAppLauncherReady();
            }
        }

        private void OnDestroy()
        {
            GameEvents.onGUIApplicationLauncherReady.Remove(OnAppLauncherReady);
            GameEvents.onGUIApplicationLauncherDestroyed.Remove(OnAppLauncherDestroyed);

            GameEvents.onGameSceneSwitchRequested.Remove(OnSceneSwitchRequested);
            GameEvents.onLevelWasLoadedGUIReady.Remove(OnLevelLoadedGUIReady);
            GameEvents.onVesselChange.Remove(OnVesselChange);
            GameEvents.onVesselWillDestroy.Remove(OnVesselWillDestroy);

            OnAppLauncherDestroyed();

            Camera.onPreRender -= OnCameraPreRender;
            Camera.onPostRender -= OnCameraPostRender;
        }

        private void OnSceneSwitchRequested(GameEvents.FromToAction<GameScenes, GameScenes> action)
        {
            ProfilerData.ClearPartStats();
        }

        private void OnVesselChange(Vessel v)
        {
            ProfilerData.ClearPartStats();
        }

        private void OnVesselWillDestroy(Vessel v)
        {
            ProfilerData.ClearPartStats();
        }

        private void OnCameraPreRender(Camera cam)
        {
            if (cam == Camera.main)
            {
                ProfilerData.OnCameraPreRender();
            }
        }

        private void OnCameraPostRender(Camera cam)
        {
            if (cam == Camera.main)
            {
                ProfilerData.OnCameraPostRender();
            }
        }

        private void LateUpdate()
        {
            ProfilerData.UpdateFrameMetrics();
        }

        private void Update()
        {
            // Hotkeys:
            // 1. Alt + Shift + P / Ctrl + Shift + P / Keypad Plus (+) => Toggle Full UI
            // 2. Alt + Shift + H / Ctrl + Shift + H => Toggle Mini HUD
            bool isAltPressed = Input.GetKey(KeyCode.LeftAlt) || Input.GetKey(KeyCode.RightAlt);
            bool isCtrlPressed = Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl);
            bool isShiftPressed = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);

            if (((isAltPressed || isCtrlPressed) && isShiftPressed && Input.GetKeyDown(KeyCode.P)) ||
                Input.GetKeyDown(KeyCode.KeypadPlus))
            {
                ToggleUI();
                ScreenMessages.PostScreenMessage(
                    $"[KSPPerformanceProfiler] {(isUiVisible ? ProfilerI18n.Get("msg_ui_opened") : ProfilerI18n.Get("msg_ui_closed"))}",
                    1.5f,
                    ScreenMessageStyle.LOWER_CENTER
                );
            }
            else if ((isAltPressed || isCtrlPressed) && isShiftPressed && Input.GetKeyDown(KeyCode.H))
            {
                if (!isUiVisible)
                {
                    ProfilerUI.IsMiniHud = true;
                    SetUIVisibility(true);
                }
                else
                {
                    ProfilerUI.IsMiniHud = !ProfilerUI.IsMiniHud;
                }

                ScreenMessages.PostScreenMessage(
                    $"[KSPPerformanceProfiler] {(ProfilerUI.IsMiniHud ? ProfilerI18n.Get("msg_hud_opened") : ProfilerI18n.Get("msg_ui_opened"))}",
                    1.5f,
                    ScreenMessageStyle.LOWER_CENTER
                );
            }
            else if (MonoHeapPadder.EnableHotkey &&
                     (isAltPressed || GameSettings.MODIFIER_KEY.GetKey()) &&
                     Input.GetKeyDown(KeyCode.End))
            {
                MonoHeapPadder.Pad(MonoHeapPadder.TargetPadMb > 0 ? MonoHeapPadder.TargetPadMb : MonoHeapPadder.RecommendedPadMb);
            }
        }

        private void OnLevelLoadedGUIReady(GameScenes scene)
        {
            MonoHeapPadder.OnSceneLoaded();
            ProfilerI18n.ReloadLanguagePacks();
        }

        private void FixedUpdate()
        {
            ProfilerData.BeginPhysicsFrame();
        }

        private IEnumerator EndOfFixedUpdateRoutine()
        {
            while (true)
            {
                yield return new WaitForFixedUpdate();
                ProfilerData.EndPhysicsFrame();
            }
        }

        public void ToggleUI()
        {
            SetUIVisibility(!isUiVisible);
        }

        public void SetUIVisibility(bool visible)
        {
            isUiVisible = visible;
            ProfilerUI.IsVisible = visible;

            if (appButton != null && !isUpdatingAppButton)
            {
                isUpdatingAppButton = true;
                if (isUiVisible)
                    appButton.SetTrue(false);
                else
                    appButton.SetFalse(false);
                isUpdatingAppButton = false;
            }
        }

        private void OnAppLauncherTrue()
        {
            if (isUpdatingAppButton) return;
            isUiVisible = true;
            ProfilerUI.IsVisible = true;
        }

        private void OnAppLauncherFalse()
        {
            if (isUpdatingAppButton) return;
            isUiVisible = false;
            ProfilerUI.IsVisible = false;
        }

        private void OnAppLauncherReady()
        {
            if (ApplicationLauncher.Instance == null) return;

            if (appButton != null)
            {
                ApplicationLauncher.Instance.RemoveModApplication(appButton);
                appButton = null;
            }

            Texture2D icon = LoadPluginIcon();
            appButton = ApplicationLauncher.Instance.AddModApplication(
                OnAppLauncherTrue,
                OnAppLauncherFalse,
                null, null, null, null,
                ApplicationLauncher.AppScenes.ALWAYS,
                icon
            );

            if (appButton != null && isUiVisible)
            {
                isUpdatingAppButton = true;
                appButton.SetTrue(false);
                isUpdatingAppButton = false;
            }
        }

        private void OnAppLauncherDestroyed()
        {
            if (appButton != null && ApplicationLauncher.Instance != null)
            {
                ApplicationLauncher.Instance.RemoveModApplication(appButton);
                appButton = null;
            }
        }

        private Texture2D LoadPluginIcon()
        {
            try
            {
                string iconPath = Path.Combine(KSPUtil.ApplicationRootPath, "GameData", "KSPPerformanceProfiler", "Icons", "icon.png");
                if (File.Exists(iconPath))
                {
                    byte[] data = File.ReadAllBytes(iconPath);
                    Texture2D tex = new Texture2D(38, 38, TextureFormat.RGBA32, false);
                    if (tex.LoadImage(data))
                    {
                        tex.wrapMode = TextureWrapMode.Clamp;
                        return tex;
                    }
                }
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogWarning($"[KSPPerformanceProfiler] Failed to load icon from disk: {ex.Message}. Falling back to procedural icon.");
            }

            return CreateDefaultIcon();
        }

        private Texture2D CreateDefaultIcon()
        {
            Texture2D tex = new Texture2D(38, 38, TextureFormat.RGBA32, false);
            tex.wrapMode = TextureWrapMode.Clamp;

            Color perfGreen = new Color(0.15f, 0.95f, 0.35f, 1f);
            Color darkBg = new Color(0.1f, 0.12f, 0.16f, 1f);

            for (int x = 0; x < 38; x++)
            {
                for (int y = 0; y < 38; y++)
                {
                    if (x < 3 || x > 34 || y < 3 || y > 34)
                        tex.SetPixel(x, y, perfGreen);
                    else
                        tex.SetPixel(x, y, darkBg);
                }
            }

            for (int y = 9; y <= 28; y++)
            {
                for (int x = 9; x <= 12; x++)
                    tex.SetPixel(x, y, perfGreen);
            }
            for (int y = 19; y <= 28; y++)
            {
                for (int x = 13; x <= 26; x++)
                {
                    if (y >= 26 || y <= 20 || x >= 23)
                        tex.SetPixel(x, y, perfGreen);
                }
            }

            tex.Apply();
            return tex;
        }

        public void ExportReportToFile()
        {
            try
            {
                string logDir = Path.Combine(KSPUtil.ApplicationRootPath, "GameData", "KSPPerformanceProfiler", "Logs");
                if (!Directory.Exists(logDir))
                {
                    Directory.CreateDirectory(logDir);
                }

                string fileName = $"ProfileReport_{DateTime.Now:yyyyMMdd_HHmmss}.txt";
                string filePath = Path.Combine(logDir, fileName);
                string reportContent = ProfilerData.ExportReport();
                File.WriteAllText(filePath, reportContent);

                string successMsg = string.Format(ProfilerI18n.Get("msg_report_saved"), fileName);
                ScreenMessages.PostScreenMessage(successMsg, 5.0f, ScreenMessageStyle.UPPER_CENTER);
                UnityEngine.Debug.Log($"[KSPPerformanceProfiler] Saved report to {filePath}");
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogError($"[KSPPerformanceProfiler] Failed to save report: {ex}");
                string failMsg = string.Format(ProfilerI18n.Get("msg_report_fail"), ex.Message);
                ScreenMessages.PostScreenMessage(failMsg, 5.0f, ScreenMessageStyle.UPPER_CENTER);
            }
        }
    }
}
