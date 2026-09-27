# KSPPerformanceProfiler

[![KSP Version](https://img.shields.io/badge/KSP-1.12.x-brightgreen.svg)](https://www.kerbalspaceprogram.com/)
[![License](https://img.shields.io/badge/license-MIT-blue.svg)](LICENSE)

**KSPPerformanceProfiler** 是一个专为坎巴拉太空计划 (Kerbal Space Program 1.12.x) 深度定制的低开销游戏内实时性能剖析、微卡顿智能猎犬与物理瓶颈分析器。

无需离开游戏，通过非侵入式 Harmony 拦截，在运行时精准定位每一毫秒消耗在哪个程序集、哪个命名空间、哪个部件模块、哪个物理关节、后处理通道或 Mono GC 停顿上。

---

## 🌟 核心特性 (Key Features)

### 1. ⚡ Mono 堆内存防卡顿管理 (Native Mono Heap Padder)
- **彻底消除 GC 刺客**：内置 MemGraph / HeapPadder 原理的静态常驻内存垫高器，预先向系统申请并永久保留 2GB~6GB 托管堆物理保护块，将 GC 停顿触发间隔从数秒一次大幅延长至数十分钟甚至数小时一次！
- **智能硬件识别**：自动探测本机物理内存容量（如 16GB / 32GB），根据硬件规格推荐最适垫高配置，并设定绝对安全上限，预留至少 4GB 供系统与 3D 渲染使用。
- **自由微调与快捷步进**：支持任意数值手动输入（MB），配备 `[-1024]`、`[-512]`、`[+512]`、`[+1024]` 快速微调按钮及一键推荐设定。
- **一键释放与手动回收**：随时一键释放垫高块恢复原生状态，或触发完全垃圾回收。

### 2. 🎯 掉帧抓拍与卡顿自动冻结 (Spike Sniffer & Auto-Freeze)
- **智能掉帧猎犬**：当瞬时帧耗时突破基准 200% 且超过 35ms（或单帧超过 66.6ms / 低于 15 FPS）时，微秒级自动抓拍现场。
- **头号元凶排行榜**：抓拍瞬时自动列出前 6 大导致掉帧的罪魁祸首（包含 Mod 脚本、PhysX 解算、Mono GC 垃圾回收及对应具体生命周期方法）。
- **自动冻结 (Killer Auto-Freeze)**：开启后发生掉帧瞬间自动冻结数据看板，从容复盘分析，点击【继续监控】瞬间解除。

### 3. 📊 交互式时间线图表 (Interactive Timeline Graph)
- **多层宏观堆叠与 FPS 曲线模式**：支持堆叠面积图与 FPS/1% Low 曲线双模切换。
- **交互式悬停与点击锁定**：鼠标悬停在图表柱体上即可浮现单帧微秒级构成（部件模块、插件、物理、GPU、调度）；点击任意柱体锁定该帧深入复盘。
- **图层独立过滤开关**：支持单独开启/关闭模块、插件、物理、GPU、引擎调度图层。
- **GC 事件标记**：时间线上以橙色 `◆` 醒目标注每次 Mono GC 触发的帧。

### 4. 智能瓶颈诊断 (Smart Bottleneck Diagnostics)
- **实时识别性能短板**：
  - 🛑 **物理解算过载 (PhysX & Joints Bound)**：零件数超标、AutoStrut 刚化过度、物理实速比 (PTR) 下降导致黄/红时钟减速。
  - 🛑 **Mod 插件脚本过载 (Mod Plugin Overhead)**：定位高耗时全局管理器（Scatterer、Principia、EVE 等）。
  - 🛑 **部件模块脚本过载 (PartModule Script Bound)**：定位载具上高耗时部件模块（Waterfall、FAR 等）。
  - 🛑 **GPU / 画面渲染瓶颈 (GPU / Render Bound)**：显卡满载、分辨率过高、抗锯齿开销。
  - 🛑 **TUFX 画面后处理负载 (TUFX Post-Processing Bound)**：深入探测 TUFX 激活通道数与时间。
  - 🛑 **微卡顿 / GC 掉帧 (Micro-Stutters & GC Spikes)**：监控 1% Low 帧率与帧时间抖动 (Jitter)。
  - 🟢 **极度流畅 (Balanced & Smooth)**：物理时钟满速、帧率平稳。
- **处方级优化建议 (Actionable Advice)**：诊断卡片附带针对性调优策略。

### 5. 程序集与多级深度钻取 (Assembly & Multi-Level Breakdown)
- **五层深度级联钻取 (Deep Hierarchical Drill-Down)**：
  - 📦 **程序集层级**：查看各 DLL 的平均耗时、峰值耗时、活跃类型数、调用次数及帧占比。
  - 📂 **命名空间 / 智能子系统层级**：一键切换按功能子系统（⏱️ 时钟调度、🚀 载具与动力学、🪐 天体与轨道、👨‍🚀 乘员与出舱、🛠️ 编辑器、🎮 操纵、🎨 视效、⚙️ 系统设置）归类。
  - 📄 **类型层级 (Class / Component)**：查看每个类的总开销。
  - ⚙️ **生命周期方法层级 (Methods)**：点击展开查看具体是 `FixedUpdate()`、`Update()`、`LateUpdate()` 还是 `OnRenderImage()` 在耗时。
  - ⚡ **调度器委托穿透层级 (Dispatcher Penetration)**：深入透视 `TimingPre`、`Timing1`~`Timing5`、`TimingFI`、`TimingManager` 等时间分发器，直观展示挂载在其内部的第三方 Mod 实际回调（如 `[ksp_plugin_adapter] PrincipiaPluginAdapter.Precalc`）。

### 6. 交互与布局自适应 (Responsive UI & Free Resize)
- **自由拖拽缩放**：右下角带有 `◢` 尺寸调节手柄，支持拖动缩放窗口大小，智能居中，配置自动持久化保存。
- **迷你 HUD (Mini HUD)**：轻量级悬浮窗，常驻屏幕角落，只显示 PTR、FPS、1% Low、诊断状态与简易波形图。
- **一键导出诊断报告**：将完整的性能剖析数据和瓶颈分析导出为文本日志。
- **中英双语 (Bilingual)**：内置简体中文与 English，一键切换。

---

## ⌨️ 快捷键 (Hotkeys)

| 快捷键 | 功能 |
|---|---|
| `Ctrl + Shift + P` / `Alt + Shift + P` / `小键盘 +` | 打开 / 关闭完整分析面板 |
| `Ctrl + Shift + H` / `Alt + Shift + H` | 打开 / 切换迷你 HUD 模式 |
| `Alt + End` / `Mod + End` | 一键垫高 Mono 堆内存 (Heap Pad) |

也可以点击游戏右侧应用启动器 (AppLauncher) 上的绿色性能图标打开面板。

---

## 📦 安装与依赖 (Installation)

### 依赖项
- **Kerbal Space Program 1.12.x**
- **Harmony 2.x** (`000_Harmony`，通常其他主流 Mod 如 Community Category Kit、ModuleManager 等已自带)

### 安装方式
1. 从 [Releases](https://github.com/DiaoDaiaChan/KSPPerformanceProfiler/releases) 下载最新构建。
2. 将 `GameData/KSPPerformanceProfiler` 文件夹放入 KSP 根目录的 `GameData/` 下。
3. 启动游戏进入飞行场景即可自动加载。

---

## 🛠️ 构建指南 (Build)

本插件使用 .NET Framework 4.7.2 / C# 7.3 构建。

```bash
# 克隆仓库
git clone https://github.com/DiaoDaiaChan/KSPPerformanceProfiler.git

# 构建 Release
dotnet build KSPPerformanceProfiler.csproj -c Release
```

生成的 DLL 及资源将自动通过 PostBuild 部署至 `../GameData/KSPPerformanceProfiler/`。

---

## 📄 开源许可 (License)

本项目采用 [MIT License](LICENSE) 开源。
