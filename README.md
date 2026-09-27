# 🚀 KSPPerformanceProfiler

[![KSP Version](https://img.shields.io/badge/KSP-1.12.x-brightgreen.svg?style=for-the-badge&logo=kerbalspaceprogram)](https://www.kerbalspaceprogram.com/)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg?style=for-the-badge)](LICENSE)
[![Release](https://img.shields.io/badge/Release-v1.2.0-blue.svg?style=for-the-badge)](https://github.com/DiaoDaiaChan/KSPPerformanceProfiler/releases)
[![Language](https://img.shields.io/badge/Language-EN%20%7C%20ZH%20%7C%20RU%20%7C%20ES%20%7C%20DE%20%7C%20JA-blueviolet.svg?style=for-the-badge)](#-internationalization--多语言支持)

> **Next-Generation In-Game Performance Profiler, Stutter Hunter & Native Mono Heap Padder for Kerbal Space Program 1.12.x.**  
> *坎巴拉太空计划新一代游戏内底层性能分析器、微卡顿猎犬与原生 Mono 堆内存防卡顿工具。*

---

## 🖼️ Visual Showcase / 界面预览

| **Dashboard & Interactive Timeline** | **Spike Sniffer & Auto-Freeze** |
|:---:|:---:|
| ![Dashboard](Screenshots/01_dashboard.png) | ![Spike Sniffer](Screenshots/02_spike_sniffer.png) |
| **5-Level Dispatcher Penetration** | **Heap Padder & RAM Recommendation** |
| ![Dispatcher Penetration](Screenshots/03_dispatcher_penetration.png) | ![Heap Padder](Screenshots/04_heap_padder.png) |

---

## 🌟 Killer Features / 核心杀手级特性

### 1. ⚡ Native Mono Heap Padder / 原生堆内存防卡顿

Mono GC "Stop-The-World" pauses are the #1 source of micro-stutters in heavily modded KSP. This mod **eliminates them**.

- **Permanent GC Root blocks** — allocated memory is pinned as a static root, so `GC.Collect()` can never shrink the heap back. Stutters that used to happen every few seconds get pushed out to 30+ minutes.  
- **Smart RAM detection** — auto-detects your physical RAM (8 / 16 / 32 / 64 GB), recommends optimal padding, and enforces a safe ceiling (reserves ≥4 GB for OS + GPU).  
- **Manual-first, never invasive** — the padder does **not** run automatically on first install. A one-time reminder guides you to the Settings tab or the <kbd>Alt</kbd>+<kbd>End</kbd> hotkey.  
- **Fine-grained controls** — `[-1024M]` `[-512M]` `[+512M]` `[+1024M]` steppers, free-text MB input, one-click "Recommended" button, and instant "Release" to return memory to the OS.

### 2. 🎯 Spike Sniffer & Auto-Freeze / 掉帧瞬态抓拍

- **Microsecond spike radar** — captures the full diagnostic topology the instant a frame exceeds 200% of baseline or drops below 15 FPS.  
- **Top-6 culprit leaderboard** — pinpoints the exact class, assembly, lifecycle method, and whether the spike was triggered by Mono GC.  
- **Auto-Freeze** — when enabled, the physics clock and game time freeze the very moment a stutter occurs, preserving the crime scene for analysis. Dismiss with one click.

### 3. 📊 Interactive Timeline Graph / 交互式时间线图表

- **Stacked area** + **FPS curve** dual-mode visualization with 60 / 30 / 20 FPS reference lines.  
- **Hover inspection** — mouse over any bar to see per-frame μs breakdown by Module, Plugin, PhysX, GPU, Overhead.  
- **Single-frame lock** — click to pin a spike frame for detailed forensics in the analysis card below.  
- **Layer toggles** — independently show/hide each subsystem layer.  
- **GC event markers** — orange `◆` diamonds on the timeline mark garbage-collection frames.

### 4. 🧠 Smart Bottleneck Diagnostics / 智能瓶颈诊断

Automatic detection and prescriptive advice for:

| Bottleneck | Symptom | Recommended Fix |
|:---|:---|:---|
| 🛑 **PhysX / Joints Bound** | PTR dropping, yellow/red clock | Reduce part count, disable AutoStrut |
| 🛑 **Mod Plugin Overhead** | Global manager scripts hogging CPU | Identify and trim heavy mods |
| 🛑 **PartModule Script Bound** | Per-vessel module CPU spike | Locate Waterfall / FAR / etc. |
| 🛑 **GPU / Render Bound** | GPU fully loaded | Lower resolution / AA / scatter density |
| 🛑 **TUFX Post-Processing** | Post-FX CPU/GPU buildup | Reduce active TUFX passes |
| 🛑 **Micro-Stutters / GC** | 1% Low FPS dips, high jitter | Apply Heap Padder |
| 🟢 **Balanced & Smooth** | All clear, full-speed physics | Enjoy your flight! |

### 5. 🌳 5-Level Deep Drill-Down / 五层深度级联钻取

Break open the black box — from high-level mod assemblies all the way down to individual lifecycle callbacks:

```
📦 Assembly (DLL)
 └── 📂 Subsystem (Timing / Vessel / Orbit / EVA / Editor ...)
      └── 📄 Class / Component
           └── ⚙️ Lifecycle Method (FixedUpdate / Update / LateUpdate / OnRenderImage)
                └── ⚡ Timing Dispatcher Penetration (TimingPre → Principia, Scatterer ...)
```

**Dispatcher Penetration** is the killer differentiator: traditional profilers see `TimingManager` as a single opaque block. This mod **drills into the delegate chain** to reveal the actual third-party callbacks hiding inside (e.g. `[ksp_plugin_adapter] PrincipiaPluginAdapter.Precalc`).

### 6. 🪟 Free Resize & Responsive UI / 自由拖拽缩放

- **Corner resize grip** `◢` — drag to any size, auto-adapts from 1080p to 4K.  
- **Position memory & edge clamping** — window position/size persists across sessions; cannot be dragged off-screen.  
- **Mini HUD** — persistent corner overlay showing PTR, FPS, 1% Low, bottleneck badge, and a sparkline chart.  
- **One-click diagnostic export** — dump the full report to `GameData/KSPPerformanceProfiler/Logs/`.

---

## 🌐 Internationalization / 多语言支持

6 built-in language packs covering all major KSP communities:

| Code | Language | Status |
|:---:|:---:|:---:|
| `en-us` | English | ✅ Official |
| `zh-cn` | 简体中文 | ✅ Official |
| `ru` | Русский | ✅ Community |
| `es-es` | Español | ✅ Localized |
| `de-de` | Deutsch | ✅ Localized |
| `ja` | 日本語 | ✅ Localized |

- **Auto-detection** — matches KSP's in-game language setting, falls back to OS locale.  
- **Live hot-switch** — click the `🌐 Language ▾` button in the title bar to change instantly, no restart needed.

---

## ⌨️ Hotkeys / 快捷键

| Hotkey | Action |
|:---|:---|
| <kbd>Ctrl</kbd>+<kbd>Shift</kbd>+<kbd>P</kbd> / <kbd>Alt</kbd>+<kbd>Shift</kbd>+<kbd>P</kbd> / <kbd>Keypad +</kbd> | Toggle full profiler window |
| <kbd>Ctrl</kbd>+<kbd>Shift</kbd>+<kbd>H</kbd> / <kbd>Alt</kbd>+<kbd>Shift</kbd>+<kbd>H</kbd> | Toggle Mini HUD |
| <kbd>Alt</kbd>+<kbd>End</kbd> | Quick-apply heap padding |

> 💡 You can also click the green profiler icon on the AppLauncher toolbar (visible in all scenes).

---

## 📦 Installation / 安装

### Dependencies
- **Kerbal Space Program 1.12.x** (1.12.0 – 1.12.5)
- **Harmony 2.x** (`000_Harmony` — bundled with most mod packs: ModuleManager, KSPCommunityFixes, etc.)

### Manual Install
1. Download the latest `KSPPerformanceProfiler-vX.X.X.zip` from the [Releases page](https://github.com/DiaoDaiaChan/KSPPerformanceProfiler/releases).
2. Extract and copy the `GameData/KSPPerformanceProfiler` folder into your KSP `GameData/` directory.
3. Verify the final structure:
   ```text
   Kerbal Space Program/
   └── GameData/
       ├── 000_Harmony/
       └── KSPPerformanceProfiler/
           ├── Icons/
           │   └── icon.png
           ├── Localization/
           │   ├── de-de.json
           │   ├── en-us.json
           │   ├── es-es.json
           │   ├── ja.json
           │   ├── ru.json
           │   └── zh-cn.json
           ├── Plugins/
           │   ├── KSPPerformanceProfiler.dll
           │   └── KSPPerformanceProfiler.pdb
           └── KSPPerformanceProfiler.version
   ```
4. Launch KSP. The profiler icon appears on the toolbar in **every scene** (Flight, Map, KSC, VAB/SPH, Tracking Station). Click it or press <kbd>Ctrl</kbd>+<kbd>Shift</kbd>+<kbd>P</kbd>.

---

## 🛠️ Build from Source / 源码构建

Targets .NET Framework 4.7.2 / C# 7.3. Build with .NET CLI or Visual Studio:

```bash
git clone https://github.com/DiaoDaiaChan/KSPPerformanceProfiler.git
cd KSPPerformanceProfiler
dotnet build KSPPerformanceProfiler.csproj -c Release
```

The PostBuild target automatically copies the DLL, PDB, localization packs, and version file into `GameData/KSPPerformanceProfiler/`.

---

## ❓ FAQ / 常见问题

**Q: How much overhead does this mod add?**  
A: Virtually zero. The core hooks use a strict **zero-allocation** strategy — no GC garbage in the hot path. UI refreshes are throttled to 250ms intervals. Total CPU cost is typically **< 0.05ms per frame**.

**Q: I already have MemGraph / HeapPadder installed. Will they conflict?**  
A: This mod's built-in Heap Padder fully replaces MemGraph / HeapPadder. We recommend removing the old one to avoid double-allocating physical RAM.

**Q: The heap padder didn't activate on its own?**  
A: By design. The padder **never modifies your Mono heap automatically** on first install. Open the Settings tab and click "Apply Recommended", or press <kbd>Alt</kbd>+<kbd>End</kbd>. Once you enable "Auto-Pad on Scene Change" in settings, it will persist across sessions.

---

## 📄 License

[MIT License](LICENSE) — free to use, modify, and distribute. PRs and Issues welcome!
