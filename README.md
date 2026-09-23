# KSPPerformanceProfiler (KSPPhysProfiler)

[![KSP Version](https://img.shields.io/badge/KSP-1.12.x-brightgreen.svg)](https://www.kerbalspaceprogram.com/)
[![License](https://img.shields.io/badge/license-MIT-blue.svg)](LICENSE)

**KSPPerformanceProfiler** 是一个专为坎巴拉太空计划 (Kerbal Space Program 1.12.x) 设计的低开销游戏内实时性能剖析与物理瓶颈智能诊断工具。

无需离开游戏，通过非侵入式 Harmony 拦截，在运行时精准定位每一毫秒消耗在哪个程序集、哪个命名空间、哪个部件模块、哪个物理关节或后处理通道上。

---

## 🌟 核心特性 (Key Features)

### 1. 智能瓶颈诊断 (Smart Bottleneck Diagnostics)
- **实时识别性能短板**：自动诊断系统属于哪种瓶颈：
  - 🛑 **物理解算过载 (PhysX & Joints Bound)**：零件数超标、AutoStrut 刚化过度、物理实速比 (PTR) 下降导致黄/红时钟减速。
  - 🛑 **Mod 插件脚本过载 (Mod Plugin Overhead)**：定位高耗时全局管理器（Scatterer、Principia、EVE 等）。
  - 🛑 **部件模块脚本过载 (PartModule Script Bound)**：定位载具上高耗时部件模块（Waterfall、FAR 等）。
  - 🛑 **GPU / 画面渲染瓶颈 (GPU / Render Bound)**：显卡满载、分辨率过高、抗锯齿开销。
  - 🛑 **TUFX 画面后处理负载 (TUFX Post-Processing Bound)**：深入探测 TUFX 激活通道数与时间。
  - 🛑 **微卡顿 / GC 掉帧 (Micro-Stutters & GC Spikes)**：监控 1% Low 帧率与帧时间抖动 (Jitter)。
  - 🟢 **极度流畅 (Balanced & Smooth)**：物理时钟满速、帧率平稳。
- **优化建议 (Actionable Advice)**：诊断卡片附带针对性调优策略。

### 2. 程序集与命名空间钻取 (Assembly Breakdown)
- **实时 CPU 占用分布条**：直观查看各大 DLL（如 `Assembly-CSharp.dll`、各 Mod DLL）的帧时间占比。
- **三层深度钻取**：
  - 📦 **程序集层级**：查看各 DLL 的平均耗时、峰值耗时、活跃类型数、调用次数及帧占比。
  - 📂 **命名空间层级**：点击展开查看命名空间（如 `<global>`、`KSP.UI.Screens`、`Expansions.Missions`、`CommNet` 等）耗时。
  - 📄 **类型层级**：继续展开查看具体类型的执行开销。
- **零新增运行时开销**：复用已有拦截数据，UI 刷新 250ms 节流聚合。

### 3. 多维度性能分析
- **📈 FPS 与掉帧波形**：实时 FPS、平均 FPS、1% Low、0.1% Low、帧时间抖动 (Jitter)、物理实速比 (PTR) 以及 100 帧历史走势图（标定 60 FPS / 30 FPS 基准线）。
- **🧩 全局插件管理器 (Global Plugins)**：监控所有继承自 `MonoBehaviour` 的独立插件耗时，按平均/峰值/调用数/帧占比排序与搜索。
- **⚙️ PartModule 模块耗时**：监控载具上所有 `PartModule` 的 `FixedUpdate`、`Update`、`LateUpdate` 耗时。
- **🚀 零件与载具分析 (Parts & Vessels)**：定位整机上最耗 CPU 的单个零件及其所属载具。
- **宏观预算比例条 (Macro Budget Bar)**：直观展示模块脚本、插件脚本、PhysX、GPU/渲染、引擎调度的占比划分。

### 4. 交互与便捷功能
- **迷你 HUD (Mini HUD)**：轻量级悬浮窗，常驻屏幕角落，只显示 PTR、FPS、1% Low、诊断状态与简易波形图。
- **一键导出诊断报告 (Export Diagnostic Dump)**：将完整的性能剖析数据和瓶颈分析导出为文本日志文件。
- **中英双语 (Bilingual)**：内置简体中文与 English，随时一键切换。

---

## ⌨️ 快捷键 (Hotkeys)

| 快捷键 | 功能 |
|---|---|
| `Ctrl + Shift + P` / `Alt + Shift + P` / `小键盘 +` | 打开 / 关闭完整分析面板 |
| `Ctrl + Shift + H` / `Alt + Shift + H` | 打开 / 切换迷你 HUD 模式 |

也可以点击游戏右侧应用启动器 (AppLauncher) 上的绿色性能图标打开面板。

---

## 📦 安装与依赖 (Installation)

### 依赖项
- **Kerbal Space Program 1.12.x**
- **Harmony 2.x** (`000_Harmony`，通常其他主流 Mod 如 Community Category Kit、ModuleManager 等已自带)

### 安装方式
1. 从 [Releases](https://github.com/DiaoDaiaChan/KSPPerformanceProfiler/releases) 下载最新构建。
2. 将 `GameData/KSPPhysProfiler` 文件夹放入 KSP 根目录的 `GameData/` 下。
3. 启动游戏进入飞行场景即可自动加载。

---

## 🛠️ 构建指南 (Build)

本插件使用 .NET Framework 4.7.2 / C# 7.3 构建。

```bash
# 克隆仓库
git clone https://github.com/DiaoDaiaChan/KSPPerformanceProfiler.git

# 构建 Release
dotnet build KSPPhysProfiler.csproj -c Release
```

生成的 DLL 将自动拷贝至 `../GameData/KSPPhysProfiler/Plugins/`。

---

## 📄 开源许可 (License)

本项目采用 [MIT License](LICENSE) 开源。
