**English** | [简体中文](README.zh-CN.md)

<p align="center">
  <img src="Icons/icon.png" alt="KSPPerformanceProfiler" width="128">
</p>

# 🚀 KSPPerformanceProfiler

[![KSP Version](https://img.shields.io/badge/KSP-1.12.x-brightgreen.svg?style=for-the-badge&logo=kerbalspaceprogram)](https://www.kerbalspaceprogram.com/)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg?style=for-the-badge)](LICENSE)
[![Release](https://img.shields.io/badge/Release-v1.2.0-blue.svg?style=for-the-badge)](https://github.com/DiaoDaiaChan/KSPPerformanceProfiler/releases)
[![Language](https://img.shields.io/badge/Language-EN%20%7C%20ZH%20%7C%20RU%20%7C%20ES%20%7C%20DE%20%7C%20JA-blueviolet.svg?style=for-the-badge)](#-multi-language)

> **A next-generation, low-overhead in-game performance profiler, micro-stutter hunter, and native Mono heap anti-stutter manager for Kerbal Space Program (KSP 1.12.x).**
> Works in every scene — Flight, Map, KSC, and VAB/SPH — and drills through the native dispatcher to locate heavy Mod callbacks, ending blind stutter hunting.

---

## 🖼️ Preview

| **Dashboard & Interactive Timeline** | **Spike Sniffer & Culprit Analysis** |
|:---:|:---:|
| ![Dashboard](Screenshots/01_dashboard.png) | ![Spike Sniffer](Screenshots/02_spike_sniffer.png) |
| **Dispatcher Delegate Penetration** | **Heap Padder & Memory Recommendation** |
| ![Dispatcher Penetration](Screenshots/03_dispatcher_penetration.png) | ![Heap Padder](Screenshots/04_heap_padder.png) |

> 💡 For a minimal always-on monitor, switch to [Mini HUD mode](Screenshots/05_mini_hud.png).

---

## 🌟 Core Features

### 1. ⚡ Mono Heap Anti-Stutter Management

With many Mods installed, Mono garbage collection (GC Stop-The-World) is the leading source of micro-stutters. This tool natively integrates resident heap protection:

- **Permanent static GC root**: the physical memory block lives in a static root reference. Even when GC is triggered manually or automatically, **the padded block is never released and never shrinks**, pushing GC stalls from every few seconds to once every tens of minutes.
- **Non-intrusive by design**: on first install it does **not pad automatically**; it only suggests a recommended value after a scene loads, and the player applies it manually in settings or via a hotkey.
- **Smart memory detection**: detects physical RAM (8G / 16G / 32G / 64G+), recommends a safe padding size, and reserves at least 4GB for the system and GPU.
- **Fine-grained tuning**: accepts any integer (MB), provides `[-1024M]` `[-512M]` `[+512M]` `[+1024M]` step buttons, and can release the padding in one click to return memory to the system.

### 2. 🎯 Frame-Spike Capture & Auto-Freeze

- **Microsecond spike radar**: when a frame's time exceeds 200% of baseline and 35ms (or FPS drops below 15), the frame's full diagnostic topology is captured automatically.
- **Top-6 culprit ranking**: lists the 6 entities with the highest cost share in that frame, pinpointing the class name, Mod assembly, lifecycle method, and whether GC caused it.
- **Auto-freeze review**: when enabled, it pauses the physics engine and game clock at the moment of a severe stutter to preserve the scene; one click resumes.

### 3. 📊 Interactive Timeline Graph

- **Dual-mode chart**: a stacked area chart shows the cost breakdown across PartModules, plugin scripts, PhysX physics, GPU rendering, and engine scheduling; the FPS curve marks 60 / 30 / 20 FPS baselines and the 1% Low smoothness metric.
- **Hover inspection**: hover any bar to see that frame's microsecond composition and per-module share.
- **Single-frame lock**: click an anomaly frame to highlight it and review its composition in the card below.
- **Layer toggles**: show or hide the module, plugin, physics, and GPU layers independently.
- **GC markers**: an orange `◆` above the timeline marks frames where GC occurred.

### 4. 🧠 Smart Bottleneck Diagnostics

Automatically identifies performance shortfalls and suggests fixes:

| Diagnosis | Symptoms | Recommendation |
|:---|:---|:---|
| 🛑 **Physics / Joints bound** | Physics real-time ratio drops, top clock turns yellow/red | Reduce part count, reduce AutoStrut rigidity, or install KSPCommunityFixes |
| 🛑 **Mod plugin overhead** | Global singleton manager scripts use high CPU | Sort the plugin list by cost to locate heavy Mods |
| 🛑 **PartModule bound** | PartModule cost spikes on the vessel | Check heavy parts in Waterfall, FAR, B9, etc. |
| 🛑 **GPU / render bound** | GPU saturated, frame time concentrated in the render pipeline | Lower resolution, anti-aliasing, or ground scatter density |
| 🛑 **Post-processing overhead** | TUFX multi-pass adds high CPU/GPU cost | Reduce active TUFX passes or disable DoF/AA |
| 🛑 **Micro-stutters / GC** | 1% Low FPS drops sharply, heavy jitter | Use the built-in heap padder to reduce GC frequency |
| 🟢 **Balanced & smooth** | Physics clock at full speed, stable FPS | Excellent performance — enjoy the flight |

### 5. 🌳 Five-Level Drill-Down & Dispatcher Penetration

Drills from high-level Mod assemblies down to low-level methods:

```text
📦 Assembly (DLL)
 └── 📂 Subsystem (timing / vessel dynamics / celestial orbits / EVA / editor)
      └── 📄 Concrete type (class / component)
           └── ⚙️ Lifecycle method (FixedUpdate / Update / LateUpdate / OnRenderImage)
                └── ⚡ Dispatcher penetration (TimingPre, Timing1~5 → Principia, Scatterer…)
```

**Dispatcher penetration is the core differentiator of this Mod**: traditional tools see only native black-box cost when inspecting `TimingPre` or `TimingManager`, whereas this Mod drills directly into the underlying delegate call chain to surface the real Mod callbacks hidden behind the native dispatcher (e.g. `PrincipiaPluginAdapter.Precalc`).

### 6. 🪟 Free Resize & Responsive Layout

- **Free resize**: drag the `◢` handle at the bottom-right to resize; adapts to 1080p / 2K / 4K.
- **Works everywhere**: covers Flight, Map, KSC, VAB/SPH, and the Tracking Station.
- **Position memory**: window size and position are saved automatically, with screen-edge snapping so the window can't be lost off-screen.
- **One-click export**: exports a full diagnostic report to `GameData/KSPPerformanceProfiler/Logs/` for reporting.

---

## 🌐 Multi-Language

Ships with 6 language packs, aligned with KSP's official localization and global communities:

| Code | Language |
|:---:|:---:|:---:|:---|
| `zh-cn` | **简体中文** | 
| `en-us` | **English** |
| `ru` | **Русский** |
| `es-es` | **Español** | 
| `de-de` | **Deutsch** | 
| `ja` | **日本語** |

- **Header shortcut**: the `🌐 Language ▾` button next to the header opens a dropdown; left-click to pick, right-click to cycle quickly.
- **Adaptive settings grid**: the settings page offers a wrapping button list and a `[ 🔄 Rescan language packs ]` action; new packs take effect instantly without restarting the game.

---

## ⌨️ Hotkeys

| Hotkey | Action |
|:---|:---|
| `Ctrl + Shift + P` / `Alt + Shift + P` / `Numpad +` | Toggle the full profiler panel |
| `Ctrl + Shift + H` / `Alt + Shift + H` | Open / toggle Mini HUD mode |
| `Alt + End` / `Mod + End` | Apply the recommended Mono heap padding |

> 💡 You can also open the panel from the green performance icon on the game's AppLauncher in any scene.

---

## 📦 Installation

### Requirements
- **Kerbal Space Program 1.12.x** (1.12.0 – 1.12.5)
- **Harmony 2.x** (`000_Harmony`, usually bundled with ModuleManager, Community Category Kit, KSPCommunityFixes, etc.)

### Manual installation
1. Download the latest `KSPPerformanceProfiler-vX.X.X.zip` from the [Releases page](https://github.com/DiaoDaiaChan/KSPPerformanceProfiler/releases).
2. Unzip and copy the `GameData/KSPPerformanceProfiler` folder into your KSP root `GameData/`.
3. The correct folder structure after installation:

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
4. Launch the game and click the toolbar icon or press a hotkey in any scene to open.

---

## 🛠️ Build from Source

The project targets .NET Framework 4.7.2 / C# 7.3 and builds with the .NET CLI or Visual Studio.

```bash
# 1. Clone the repository
git clone https://github.com/DiaoDaiaChan/KSPPerformanceProfiler.git
cd KSPPerformanceProfiler

# 2. Build (Release)
dotnet build KSPPerformanceProfiler.csproj -c Release
```

After a successful build, the PostBuild step copies the DLL, PDB, localization files, and version info to `GameData/KSPPerformanceProfiler/`.

---

## ❓ FAQ

#### Q: How much performance overhead does the Mod itself add?
**A:** Virtually none. Core interception and rendering paths use a strict **zero-allocation** strategy to avoid extra GC garbage in the main loop; UI refresh is throttled and merged with a 250ms damper, keeping the Mod's own CPU cost typically below **0.05ms** per frame.

#### Q: Why isn't heap padding applied automatically after installation?
**A:** We follow a non-intrusive principle: modifying memory should be a player's informed, deliberate choice. On first install it only shows a one-time recommendation after loading and never takes your physical RAM on its own; you can apply it manually in settings anytime.

#### Q: I already have MemGraph or HeapPadder — will it conflict?
**A:** This Mod's heap padder fully replaces the core functionality of MemGraph / HeapPadder. We recommend removing the old versions to avoid multiple Mods requesting extra padded memory and wasting physical RAM.

#### Q: I can't see the Mod icon in-game — what should I do?
**A:** This Mod registers for all scenes. If the toolbar icon is pushed off-screen by too many plugins, press `Ctrl + Shift + P` or the numpad `+` to open the panel.

---

## 📄 License

Released under the [MIT License](LICENSE). You are free to study, distribute, and modify it. PRs and issues are welcome.

---

<p align="center">
  <a href="https://github.com/DiaoDaiaChan">
    <img src="https://github.com/DiaoDaiaChan.png" alt="DiaoDaiaChan" width="96">
  </a>
</p>
