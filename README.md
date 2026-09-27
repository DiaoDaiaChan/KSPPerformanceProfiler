<p align="center">
  <img src="Icons/icon.png" alt="KSPPerformanceProfiler" width="128">
</p>

# 🚀 KSPPerformanceProfiler

[![KSP Version](https://img.shields.io/badge/KSP-1.12.x-brightgreen.svg?style=for-the-badge&logo=kerbalspaceprogram)](https://www.kerbalspaceprogram.com/)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg?style=for-the-badge)](LICENSE)
[![Release](https://img.shields.io/badge/Release-v1.2.0-blue.svg?style=for-the-badge)](https://github.com/DiaoDaiaChan/KSPPerformanceProfiler/releases)
[![Language](https://img.shields.io/badge/Language-EN%20%7C%20ZH%20%7C%20RU%20%7C%20ES%20%7C%20DE%20%7C%20JA-blueviolet.svg?style=for-the-badge)](#-多语言支持)

> **坎巴拉太空计划（KSP 1.12.x）新一代低开销游戏内性能分析器、微卡顿猎犬与原生 Mono 堆内存防卡顿管理工具。**
> 覆盖飞行、地图、航天中心、空间站与装配大楼全场景，可穿透原生调度器定位高耗时 Mod 回调，告别盲目排查卡顿。

---

## 🖼️ 界面预览

| **仪表盘与交互式时间线** | **掉帧抓拍与真凶分析** |
|:---:|:---:|
| ![Dashboard](Screenshots/01_dashboard.png) | ![Spike Sniffer](Screenshots/02_spike_sniffer.png) |
| **调度器委托深度穿透** | **堆内存垫高与内存推荐** |
| ![Dispatcher Penetration](Screenshots/03_dispatcher_penetration.png) | ![Heap Padder](Screenshots/04_heap_padder.png) |

> 💡 需要极简常驻监控时，可切换至 [迷你 HUD 浮窗模式](Screenshots/05_mini_hud.png)。

---

## 🌟 核心特性

### 1. ⚡ Mono 堆内存防卡顿管理

安装大量 Mod 后，Mono 垃圾回收（GC Stop-The-World）是微卡顿的首要来源。本工具原生集成常驻堆内存保护：

- **静态常驻 GC 根对象**：物理内存块常驻静态根引用，即使手动或自动触发 GC，**垫高块也绝不释放、绝不回缩**，将每几秒一次的 GC 卡顿推迟至数十分钟一次。
- **非侵入式设计**：首次安装默认**不主动垫高**，仅在加载场景后提示推荐值，由玩家在设置页手动应用或按快捷键确认。
- **智能内存探测**：自动识别本机物理内存（8G / 16G / 32G / 64G+），推荐安全垫高容量，并预留至少 4GB 供系统与 GPU 使用。
- **细粒度微调**：支持输入任意整数（MB），提供 `[-1024M]` `[-512M]` `[+512M]` `[+1024M]` 步进按钮，并可一键释放垫高、归还物理内存。

### 2. 🎯 掉帧抓拍与卡顿自动冻结

- **微秒级掉帧雷达**：某帧耗时突增超过基准 200% 且大于 35ms（或帧率跌破 15 FPS）时，自动截获该帧完整诊断拓扑。
- **六大真凶排行**：列出该帧耗时占比最高的 6 个实体，精确定位到类名、Mod 程序集、生命周期方法，并标明是否由 GC 引起。
- **自动急冻复盘**：开启后，在严重卡顿瞬间自动暂停物理引擎与游戏时钟，保留案发现场，分析完毕一键解除。

### 3. 📊 交互式时间线图表

- **双模式图表**：堆叠面积图展示部件模块、插件脚本、PhysX 物理、GPU 渲染与引擎调度的耗时构成；FPS 曲线标定 60 / 30 / 20 FPS 基准线与 1% Low 平滑度指标。
- **悬停检视**：鼠标悬停任意柱体即可查看该帧的微秒构成与各模块占比。
- **单帧锁定**：点击异常帧即可锁定高亮，在下方分析卡片中复盘该帧构成。
- **图层开关**：可分别显示或隐藏模块、插件、物理、GPU 等图层。
- **GC 标记**：时间线上方以橙色 `◆` 标出发生 GC 的帧。

### 4. 🧠 智能瓶颈诊断

自动识别性能短板并给出优化处方：

| 诊断状态 | 表现特征 | 优化处方 |
|:---|:---|:---|
| 🛑 **物理解算过载** | 物理实速比下降，顶部时钟变黄/红 | 减少部件数、减少 AutoStrut 刚化或安装 KSPCommunityFixes |
| 🛑 **Mod 全局插件过载** | 全局单例管理器脚本占用 CPU 高 | 在插件列表中按耗时排序定位高耗时 Mod |
| 🛑 **部件模块脚本过载** | 载具部件模块耗时暴增 | 检查 Waterfall、FAR、B9 等模块的高耗时部件 |
| 🛑 **GPU / 渲染瓶颈** | 显卡满载，帧耗时集中在渲染管线 | 调低分辨率、抗锯齿或地表散布密度 |
| 🛑 **后处理负载过高** | TUFX 多通道导致 CPU/GPU 开销大 | 减少激活的 TUFX 通道或关闭景深/抗锯齿 |
| 🛑 **微卡顿 / GC** | 1% Low 帧率骤降，帧抖动剧烈 | 使用内置堆内存垫高延缓 GC 频率 |
| 🟢 **极度流畅** | 物理时钟满速、帧率平稳 | 性能极佳，尽情飞行 |

### 5. 🌳 五层钻取与调度器穿透

从高层 Mod 程序集一路下钻至底层方法：

```text
📦 程序集
 └── 📂 功能子系统（时钟调度 / 载具动力学 / 天体轨道 / 出舱 / 编辑器）
      └── 📄 具体类型
           └── ⚙️ 生命周期方法（FixedUpdate / Update / LateUpdate / OnRenderImage）
                └── ⚡ 调度器穿透（TimingPre、Timing1~5 → Principia、Scatterer…）
```

**调度器穿透是本 Mod 的核心差异化能力**：传统工具查看 `TimingPre` 或 `TimingManager` 时只能看到原生黑盒耗时，本 Mod 可直接钻入底层委托调用链，抓出隐藏在原生调度器背后的真实 Mod 回调（如 `PrincipiaPluginAdapter.Precalc`）。

### 6. 🪟 自由缩放与自适应布局

- **自由缩放**：拖动右下角 `◢` 手柄即可调整窗口尺寸，自动适配 1080p / 2K / 4K。
- **全场景可用**：覆盖飞行、地图、航天中心、装配大楼与深空追踪站。
- **坐标记忆**：窗口尺寸与位置自动保存，并带屏幕边界吸附，避免窗口被拖出屏幕外。
- **一键导出**：将完整诊断报告导出至 `GameData/KSPPerformanceProfiler/Logs/`，便于反馈排查。

---

## 🌐 多语言支持

内置 6 种语言包，与 KSP 官方本地化及全球社区契合：

| 语言代码 | 语言名称 | 状态 | 适用地区 |
|:---:|:---:|:---:|:---|
| `zh-cn` | **简体中文** | ✅ 官方原生 | 中国大陆 / 华语圈 |
| `en-us` | **English** | ✅ 官方支持 | 全球 / 国际社区 |
| `ru` | **Русский** | ✅ 社区精翻 | 俄罗斯 / 独联体 |
| `es-es` | **Español** | ✅ 官方本地化 | 西班牙 / 拉美 |
| `de-de` | **Deutsch** | ✅ 官方本地化 | 德国 / 奥地利 / 瑞士 |
| `ja` | **日本語** | ✅ 官方本地化 | 日本社区 |

- **主标题栏直达**：主标题栏旁的 `🌐 Language ▾` 按钮可呼出下拉菜单，支持左键点选与右键快速轮换。
- **设置页自适应网格**：设置页提供自适应换行按钮列表，并支持 `[ 🔄 重新扫描语言包 ]`，新增语言包即时热生效，无需重启游戏。

---

## ⌨️ 快捷键

| 快捷键 | 功能 |
|:---|:---|
| `Ctrl + Shift + P` / `Alt + Shift + P` / `小键盘加号 (+)` | 打开 / 关闭完整性能分析面板 |
| `Ctrl + Shift + H` / `Alt + Shift + H` | 打开 / 切换迷你 HUD 模式 |
| `Alt + End` / `Mod + End` | 快捷应用推荐的 Mono 堆内存垫高 |

> 💡 在任意场景也可点击游戏右侧应用启动器上的绿色性能图标打开面板。

---

## 📦 安装

### 依赖项
- **Kerbal Space Program 1.12.x**（1.12.0 ~ 1.12.5）
- **Harmony 2.x**（`000_Harmony`，通常已由 ModuleManager、Community Category Kit、KSPCommunityFixes 等 Mod 自带）

### 手动安装
1. 前往 [Releases 页面](https://github.com/DiaoDaiaChan/KSPPerformanceProfiler/releases) 下载最新的 `KSPPerformanceProfiler-vX.X.X.zip`。
2. 解压并将 `GameData/KSPPerformanceProfiler` 文件夹完整拷贝至 KSP 根目录下的 `GameData/`。
3. 安装后的正确路径结构如下：

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
4. 启动游戏，在任意场景点击工具栏图标或按快捷键即可打开。

---

## 🛠️ 源码构建

项目面向 .NET Framework 4.7.2 / C# 7.3 开发，支持 .NET CLI 或 Visual Studio 构建。

```bash
# 1. 克隆代码仓库
git clone https://github.com/DiaoDaiaChan/KSPPerformanceProfiler.git
cd KSPPerformanceProfiler

# 2. 执行编译（Release 配置）
dotnet build KSPPerformanceProfiler.csproj -c Release
```

构建成功后，PostBuild 会自动将 DLL、PDB、本地化文件与版本信息拷贝至 `GameData/KSPPerformanceProfiler/`。

---

## ❓ 常见问题

#### Q: 这个 Mod 本身会带来多少性能开销？
**A:** 几乎为零。核心拦截与渲染路径采用**零堆内存分配**策略，避免主循环产生额外 GC 垃圾；UI 刷新做 250ms 阻尼节流合并，自身 CPU 开销通常小于单帧 **0.05ms**。

#### Q: 为什么安装后没有默认自动垫高内存？
**A:** 我们坚持非侵入式原则，修改内存的行为应由玩家知情并主动确认。初次安装仅在加载完成后弹出一次性推荐提醒，不会擅自占用物理内存，可随时在设置页手动应用。

#### Q: 我已经装了 MemGraph 或 HeapPadder，会冲突吗？
**A:** 本 Mod 的堆内存垫高已完整替代 MemGraph / HeapPadder 的核心功能。建议卸载旧版，避免多个 Mod 同时申请垫高内存造成物理内存浪费。

#### Q: 游戏内看不见 Mod 图标怎么办？
**A:** 本 Mod 已注册全场景显示。若工具栏图标被过多插件挤出屏幕，可直接按 `Ctrl + Shift + P` 或小键盘 `+` 呼出面板。

---

## 📄 开源协议

本项目基于 [MIT License](LICENSE) 开放源码，可自由学习、分发与修改。欢迎提交 PR 与 Issue。

---

<p align="center">
  <a href="https://github.com/DiaoDaiaChan">
    <img src="https://github.com/DiaoDaiaChan.png" alt="DiaoDaiaChan" width="96">
  </a>
</p>
