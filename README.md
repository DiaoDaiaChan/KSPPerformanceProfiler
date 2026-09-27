# 🚀 KSPPerformanceProfiler

[![KSP Version](https://img.shields.io/badge/KSP-1.12.x-brightgreen.svg?style=for-the-badge&logo=kerbalspaceprogram)](https://www.kerbalspaceprogram.com/)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg?style=for-the-badge)](LICENSE)
[![Release](https://img.shields.io/badge/Release-v1.2.0-blue.svg?style=for-the-badge)](https://github.com/DiaoDaiaChan/KSPPerformanceProfiler/releases)
[![Language](https://img.shields.io/badge/Language-EN%20%7C%20ZH%20%7C%20RU%20%7C%20ES%20%7C%20DE%20%7C%20JA-blueviolet.svg?style=for-the-badge)](#-多语言支持)

> **坎巴拉太空计划（KSP 1.12.x）新一代低开销游戏内底层性能分析器、微卡顿猎犬与原生 Mono 堆内存防卡顿管理工具。**  
> 全场景可用（飞行/地图/航天中心/空间站/装配大楼），穿透原生调度器抓出高耗时 Mod 回调，彻底告别盲目排查卡顿！

---

## 🖼️ 界面预览

| **仪表盘大盘与交互式时间线图表** | **掉帧瞬态抓拍与真凶分析 (Spike Sniffer)** |
|:---:|:---:|
| ![Dashboard](Screenshots/01_dashboard.png) | ![Spike Sniffer](Screenshots/02_spike_sniffer.png) |
| **五层调度器委托深度穿透 (Dispatcher Penetration)** | **堆内存垫高与内存智能推荐 (Heap Padder)** |
| ![Dispatcher Penetration](Screenshots/03_dispatcher_penetration.png) | ![Heap Padder](Screenshots/04_heap_padder.png) |

> 💡 *若需要极简常驻监控，可点击右上角切换至 [迷你 HUD 浮窗模式](Screenshots/05_mini_hud.png)。*

---

## 🌟 核心杀手级特性

### 1. ⚡ 原生 Mono 堆内存防卡顿管理 (Native Mono Heap Padder)

在安装大量 Mod 的大型 KSP 游戏中，Mono 垃圾回收（GC Stop-The-World）是微卡顿的第一大元凶。本工具原生集成了常驻堆内存保护：

- **静态常驻 GC 根对象（Permanent GC Root）**：分配的物理内存块常驻于静态根引用中，即使手动或自动触发 GC，**垫高块也绝不释放、绝不回缩**，将每几秒一次的 GC 卡顿大幅推迟至数十分钟一次！
- **非侵入式设计（默认不擅自修改内存）**：首次安装默认**不主动垫高**，仅在加载场景后通过屏幕提示建议推荐值。一切修改权交由玩家在设置页手动应用或按下快捷键。
- **智能物理内存探测**：自动识别本机物理 RAM（8G / 16G / 32G / 64G+），智能推荐安全垫高容量，并计算安全保留线（至少预留 4GB 供系统与 GPU 使用）。
- **细粒度微调与快速释放**：支持自由输入任意整数（MB），提供 `[-1024M]` `[-512M]` `[+512M]` `[+1024M]` 步进微调按钮，并可随时一键【释放垫高】将物理内存归还系统。

### 2. 🎯 掉帧瞬态抓拍与卡顿自动冻结 (Spike Sniffer & Auto-Freeze)

- **微秒级掉帧雷达**：当某帧耗时突增超过基准 200% 且大于 35ms（或帧率跌破 15 FPS）时，立即自动截获该帧的完整诊断拓扑。
- **六大真凶排行榜**：抓拍时列出该帧消耗占比最高的 6 大实体（精确定位至具体类名、Mod 程序集、生命周期方法，以及是否由 Mono GC 回收引起）。
- **自动急冻复盘 (Auto-Freeze)**：开启后，在遭遇严重卡顿的瞬间自动将物理引擎与游戏时钟暂停，保留“案发现场”，方便从容复盘真凶，分析完毕后一键解除。

### 3. 📊 交互式微秒时间线图表 (Interactive Timeline Graph)

- **堆叠面积图与 FPS 曲线双模式**：
  - **堆叠图 (Stacked Area)**：直观展现部件模块 (PartModules)、插件脚本 (Plugins)、PhysX 物理、GPU 渲染及引擎调度 (Overhead) 的切片堆叠。
  - **FPS 曲线**：实时标定 60 FPS、30 FPS、20 FPS 警示基准线与 1% Low 电竞级平滑度指标。
- **悬停穿透检视**：鼠标悬停在波形图任意柱体上，即可实时显示该帧的微秒构成与各模块占比。
- **单帧锁定复盘**：点击任意异常帧即可锁定高亮，并在下方常驻分析卡片中复盘该帧构成。
- **独立图层开关**：支持分别勾选显示或隐藏模块、插件、物理、GPU 等图层。
- **GC 标记指示**：时间线上方以橙色 `◆` 菱形清晰标出发生 GC 垃圾回收的特定帧。

### 4. 🧠 智能瓶颈诊断与处方引擎 (Smart Bottleneck Diagnostics)

全自动识别游戏性能短板，并给出针对性优化方案：

| 诊断状态 | 表现特征 | 推荐优化处方 |
|:---|:---|:---|
| 🛑 **物理解算过载 (PhysX & Joints Bound)** | PTR 物理实速比下降，顶部时钟变黄/红变慢 | 减少部件数、减少 AutoStrut 刚化或安装 KSPCommunityFixes |
| 🛑 **Mod 全局插件过载 (Mod Plugin Overhead)** | 全局单例管理器脚本严重占用 CPU | 在插件列表中按耗时排序定位高耗时 Mod |
| 🛑 **部件模块脚本过载 (PartModule Bound)** | 载具部件模块耗时暴增 | 检查 Waterfall、FAR、B9 等模块的高耗时部件 |
| 🛑 **GPU / 画面渲染瓶颈 (GPU / Render Bound)** | 显卡满载，帧耗时集中在渲染管线 | 调低分辨率、抗锯齿或地表散布密度 |
| 🛑 **后处理负载过高 (TUFX Overhead)** | TUFX 多通道导致 CPU/GPU 开销大 | 减少激活的 TUFX 通道数或关闭景深/抗锯齿 |
| 🛑 **微卡顿 / GC 刺客 (Micro-Stutters / GC)** | 1% Low 帧率骤降，帧抖动剧烈 | 使用内置 Heap Padder 垫高堆内存，延缓 GC 频率 |
| 🟢 **极度流畅 (Balanced & Smooth)** | 物理时钟满速、帧率平稳无瓶颈 | 性能极佳，尽情享受飞行！ |

### 5. 🌳 五层深度级联钻取与调度器穿透 (5-Level Deep Drill-Down)

彻底打破性能黑盒，从高层 Mod 程序集一路下钻至底层方法：

```text
📦 程序集 (DLL / Assembly)
 └── 📂 功能子系统 (⏱️时钟调度、🚀载具动力学、🪐天体轨道、👨‍🚀出舱、🛠️编辑器...)
      └── 📄 具体类型 (Class / Component)
           └── ⚙️ 生命周期方法 (FixedUpdate / Update / LateUpdate / OnRenderImage)
                └── ⚡ 调度器穿透 (TimingPre / Timing1~5 → Principia, Scatterer ...)
```

**调度器穿透（Dispatcher Penetration）是本 Mod 的核心差异化杀手特性**：传统性能分析工具查看 `TimingPre` 或 `TimingManager` 时只能看到原生的黑盒耗时；本 Mod 则能**直接钻入底层委托调用链**，瞬间抓出躲在原生调度器背后的真实 Mod 回调（例如 `[ksp_plugin_adapter] PrincipiaPluginAdapter.Precalc`）！

### 6. 🪟 自由拖拽缩放与自适应布局 (Free Resize & Responsive UI)

- **右下角自由缩放**：采用标准 `◢` 缩放手柄，随心拖动窗口至任意尺寸，自动适配 1080p、2K、4K。
- **全场景全天候可用**：覆盖飞行 (Flight)、地图 (MapView)、航天中心 (KSC)、装配大楼 (VAB/SPH) 与深空追踪站 (Tracking Station)。
- **坐标记忆与屏幕防丢失保护**：窗口尺寸和坐标自动保存，且带屏幕边界吸附，杜绝窗口被拖出屏幕外无法找回的问题。
- **一键诊断导出**：一键导出完整诊断报告文本至 `GameData/KSPPerformanceProfiler/Logs/` 供 Mod 开发者或反馈工单排查。

---

## 🌐 多语言支持

内置 6 种主流语言包，与 KSP 官方本地化及全球社区高度契合：

| 语言代码 | 语言名称 | 状态 | 适用地区 / 社区 |
|:---:|:---:|:---:|:---|
| `zh-cn` | **简体中文** | ✅ 官方原生 | 中国大陆 / 华语圈社区 |
| `en-us` | **English** | ✅ 官方支持 | Global / International Community |
| `ru` | **Русский** | ✅ 社区精翻 | Россия / СНГ (Крупнейшее сообщество KSP) |
| `es-es` | **Español** | ✅ 官方本地化 | España / Latinoamérica |
| `de-de` | **Deutsch** | ✅ 官方本地化 | Deutschland / Österreich / Schweiz |
| `ja` | **日本語** | ✅ 官方本地化 | 日本 KSP コミュニティ |

- **主标题栏直达**：主标题栏旁设有 `🌐 Language ▾` 按钮，点击即可呼出沉浸式下拉菜单，支持鼠标左键精准点选与右键快速轮换。
- **设置页自适应网格**：设置页提供了自适应换行按钮列表，并支持 `[ 🔄 重新扫描语言包 ]`，动态放入新语言包即刻热生效，无需重启游戏。

---

## ⌨️ 快捷键指南

| 快捷键 | 功能说明 |
|:---|:---|
| `Ctrl + Shift + P` / `Alt + Shift + P` / `小键盘加号 (+)` | 打开 / 关闭完整性能分析面板 (Toggle Full UI) |
| `Ctrl + Shift + H` / `Alt + Shift + H` | 打开 / 切换迷你 HUD 模式 (Toggle Mini HUD) |
| `Alt + End` / `Mod + End` | 一键快捷应用推荐的 Mono 堆内存垫高 (Quick Pad Heap) |

> 💡 **提示**：在任意场景均可直接点击游戏右侧应用启动器 (AppLauncher) 上的绿色性能图标打开面板。

---

## 📦 安装说明

### 依赖项
- **Kerbal Space Program 1.12.x** (1.12.0 ~ 1.12.5)
- **Harmony 2.x** (`000_Harmony`，通常 ModuleManager、Community Category Kit、KSPCommunityFixes 等 Mod 均已自带)

### 手动安装
1. 前往 [Releases 页面](https://github.com/DiaoDaiaChan/KSPPerformanceProfiler/releases) 下载最新的 `KSPPerformanceProfiler-vX.X.X.zip`。
2. 解压并将压缩包内的 `GameData/KSPPerformanceProfiler` 文件夹完整拷贝至你的 KSP 根目录下的 `GameData/` 中。
3. 安装完成后的正确路径结构如下：
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
4. 启动游戏，在任意场景（飞行、地图、航天中心、空间站、装配大楼）点击工具栏图标或按快捷键即可打开。

---

## 🛠️ 源码构建

本项目面向 .NET Framework 4.7.2 / C# 7.3 开发，支持通过 .NET CLI 或 Visual Studio 进行构建。

```bash
# 1. 克隆代码仓库
git clone https://github.com/DiaoDaiaChan/KSPPerformanceProfiler.git
cd KSPPerformanceProfiler

# 2. 执行编译 (Release 配置)
dotnet build KSPPerformanceProfiler.csproj -c Release
```

构建成功后，MSBuild PostBuild 会自动将编译生成的 DLL、PDB、本地化文件及版本信息拷贝输出至目标 `GameData/KSPPerformanceProfiler/` 目录。

---

## ❓ 常见问题 (FAQ)

#### Q: 这个 Mod 本身会带来多少性能开销？
**A:** 几乎为零。我们在核心拦截和渲染路径上实行了严苛的 **Zero-Allocation（零堆内存分配）** 策略，避免在主循环中产生任何额外的 GC 垃圾；UI 刷新实行了 250ms 阻尼节流合并，整体自身 CPU 开销通常小于单帧的 **0.05ms**。

#### Q: 为什么安装后没有默认自动垫高内存？
**A:** 我们坚持“非侵入式”原则。任何修改系统和 Mono 内存的行为均应当由玩家知情并主动确认。因此初次安装时工具仅在加载完成后弹出一次性推荐提醒，绝不会擅自占用你的物理 RAM。你可以随时在设置页根据你的机器配置手动应用。

#### Q: 我已经装了 MemGraph 或 HeapPadder，会冲突吗？
**A:** 本 Mod 的 **Mono Heap Padder** 已完整原生替代了 MemGraph / HeapPadder 的核心功能。建议卸载旧版 HeapPadder / MemGraph，避免两个 Mod 同时向系统申请多份多余的垫高内存造成物理 RAM 浪费。

#### Q: 游戏内看不见 Mod 图标怎么办？
**A:** 本 Mod 已注册全场景显示（`ApplicationLauncher.AppScenes.ALWAYS`）。如果工具栏图标被其他过多插件挤出屏幕，可直接按下快捷键 `Ctrl + Shift + P` 或小键盘 `+` 呼出面板。

---

## 📄 开源协议

本项目基于 [MIT License](LICENSE) 协议开放源码，你可以自由学习、分发与修改。欢迎提交 PR 与 Issue！
