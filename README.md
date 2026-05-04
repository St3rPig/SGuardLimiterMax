# SGuard Limiter

![Platform](https://img.shields.io/badge/platform-Windows%2010%2F11-blue)
![.NET](https://img.shields.io/badge/.NET-8.0-purple)
![License](https://img.shields.io/badge/license-MIT-green)

**自动检测游戏启动，一键完成系统级性能调优。**
A lightweight Windows utility that automatically applies OS-level performance optimizations when a monitored game is detected.

| 亮色主题 | 暗色主题 |
|:---:|:---:|
| ![亮色主题](Assets/亮版本.png) | ![暗色主题](Assets/暗版本.png) |

---

## 功能介绍 / Features

| 功能 | 说明 |
|---|---|
| **限制 SGuard 进程** | 将 `SGuard64` / `SGuardSvc64` 设为最低优先级并限定在单个 CPU 核心，释放其他核心给游戏 |
| **提升游戏进程优先级** | 将游戏进程提升至"高"优先级，使操作系统优先调度游戏 |
| **解绑 CPU 0 核心** | 将游戏进程从 CPU 0 上移除，避免系统中断占用（可选，效果因 CPU 架构而异） |
| **切换电源计划** | 游戏启动时自动切换至指定电源计划，退出后自动还原；可从系统现有计划列表中选择目标计划 |
| **刷新 DNS 缓存** | 游戏启动时执行 `ipconfig /flushdns`，清除过期 DNS 记录以降低首次连接延迟 |
| **提升系统计时器精度** | 将 Windows 定时器精度从默认 15.6ms 提升至竞技级精度，支持 1ms / 2ms / 4ms / 8ms 四档可选，改善帧时序和输入响应一致性 |
| **自定义游戏进程** | 手动添加任意游戏进程名，独立配置优先级提升和 CPU 0 解绑策略 |
| **深色 / 浅色双主题** | 内置暗色与亮色主题，可一键切换，默认跟随 Windows 系统主题（读取注册表 `AppsUseLightTheme`） |
| **自定义结果弹窗** | 执行优化后以非模态弹窗展示结果摘要，取代系统 MessageBox，与整体 UI 风格统一 |
| **游戏退出托盘通知** | 游戏退出后发送系统通知，告知优化已还原 |
| **检测到游戏自动进入托盘** | 游戏启动后主界面自动最小化至系统托盘，后台静默运行 |
| **游戏退出后继续监控** | 保持驻留等待下次游戏启动（默认关闭，即游戏退出后程序自动结束） |
| **开机自动启动** | 注册至 `HKCU\...\Run`，以 `--autostart` 模式静默启动（不显示主界面） |

默认开启：限制 SGuard、提升游戏进程优先级、刷新 DNS 缓存。

---

## 系统要求 / Requirements

- **操作系统**：Windows 10 / 11（x64）
- **权限**：需要以**管理员身份**运行（修改进程优先级和电源计划必须）
- **内置支持游戏**：VALORANT（无畏契约）、Delta Force（三角洲行动）；其他游戏可通过"自定义游戏进程"添加

---

## 下载 / Download

前往 [Releases](../../releases) 页面，根据情况选择版本：

| 文件 | 大小 | 说明 |
|---|---|---|
| `SGuardLimiterMax_standalone.exe` | ~70 MB | **推荐**：自带运行时，开箱即用 |
| `SGuardLimiterMax_framework.exe` | ~2 MB | 需要已安装 [.NET 8.0 Desktop Runtime](https://dotnet.microsoft.com/download/dotnet/8.0) |

> 不确定自己有没有装 .NET 8？下载 standalone 版本即可。

---

## 快速开始 / Quick Start

1. 下载上方对应版本的 exe
2. 右键选择"以管理员身份运行"
3. 根据需要开启或关闭各项功能，点击"保存设置"
4. 点击"启动"或直接选中"启动后最小化到托盘"——程序在后台等待游戏启动
5. 启动游戏，优化自动生效；退出游戏，所有系统设置自动还原

> 配置文件 `Config.json` 保存在程序同目录下，便携无需安装。

---

## 技术原理 / How It Works

> 本节供开发者参考。软件仅使用 Windows 公开 API，无任何代码注入或内核驱动。

### 架构

```
MainWindow (WPF, WindowStyle=None)
├── MainViewModel         # MVVM ViewModel，3 秒轮询后台检测循环
├── ProcessOptimizer      # 进程优先级 & CPU 亲和性（内置 + 自定义游戏）
├── PowerManager          # 电源计划查询/切换 & DNS 刷新
├── TimerResolutionService # WinMM 计时器精度（支持多档调节）
├── StartupManager        # 注册表自启
├── ConfigManager         # Config.json 读写
├── ThemeManager          # 深色/浅色主题切换 & 系统主题检测
└── Views/
    └── ResultDialog      # 自定义结果弹窗（替代 MessageBox）
```

### 各模块实现

**ProcessOptimizer** — `Services/ProcessOptimizer.cs`
使用 .NET `System.Diagnostics.Process` API 修改进程调度属性：
```csharp
proc.PriorityClass = ProcessPriorityClass.Idle;
proc.ProcessorAffinity = (nint)(1 << lastCoreIndex);  // 限定末尾核心
```
内置游戏使用全局标志；自定义游戏（`CustomGameEntry`）各自独立配置。

**PowerManager** — `Services/PowerManager.cs`
通过 `powercfg /list` 查询系统所有电源计划，`powercfg /setactive {GUID}` 切换，`ipconfig /flushdns` 刷新 DNS。支持用户从列表中指定目标计划，未指定时自动选择卓越性能或高性能。

**TimerResolutionService** — `Services/TimerResolutionService.cs`
P/Invoke 调用 `winmm.dll`：
```csharp
[DllImport("winmm.dll")] static extern uint timeBeginPeriod(uint uPeriod);
[DllImport("winmm.dll")] static extern uint timeEndPeriod(uint uPeriod);
```
提供竞技 1ms、推荐 2ms、轻薄本 4ms、旧机型 8ms 四档可选，退出时自动还原。

**ThemeManager** — `Services/ThemeManager.cs`
启动时读取注册表键 `HKCU\Software\Microsoft\Windows\CurrentVersion\Themes\Personalize\AppsUseLightTheme` 检测系统主题，自动匹配深色或浅色主题资源字典（`Resources/DarkTheme.xaml` / `Resources/LightTheme.xaml`）。点击侧边栏主题按钮可在两套主题间即时切换。

**ResultDialog** — `Views/ResultDialog.xaml` / `Views/ResultDialog.xaml.cs`
自定义 WPF 弹窗，用于展示"立即应用"和"添加游戏"等操作的结果摘要。支持 Enter / Escape 快捷键关闭，窗口可拖拽，视觉风格与主界面保持一致。

**StartupManager** — `Services/StartupManager.cs`
读写 `HKCU\Software\Microsoft\Windows\CurrentVersion\Run`，每次启动时同步注册表路径，确保移动 exe 后自启仍有效。

**ConfigManager** — `Services/ConfigManager.cs`
`System.Text.Json` 序列化/反序列化 `AppConfig`，配置文件与 exe 同目录，完全便携。

### 构建 / Build

```powershell
# 同时生成 standalone 和 framework 两个版本
.\build.ps1

# 或直接双击
build.bat
```

输出目录：`publish\standalone\` 和 `publish\framework\`，均为单个可执行文件。

---

## 与上游项目的差异 / Differences from Upstream

本项目 fork 自 [SGuardLimiterMax](https://github.com/Kangieee/SGuardLimiterMax)，在此基础上进行了以下改动：

- **UI 重构**：由原项目的"有机手绘"侧边栏多页布局改为极简单页布局，所有选项一览无余
- **双主题支持**：新增基于注册表检测的深色/浅色自动主题，取代原项目的 organic / zen 双模式
- **多档计时器精度**：由固定 1ms 扩展为 1ms / 2ms / 4ms / 8ms 四档可选，适配不同硬件
- **自定义结果弹窗**：以与 UI 风格统一的 `ResultDialog` 替代系统 `MessageBox`
- **精简命名**：应用名由 "SGuard Limiter Max" 简化为 "SGuard Limiter"

---

## 免责声明 / Disclaimer

- 本软件**不注入代码**、**不修改游戏文件**、**不读取游戏内存**，仅通过 Windows 公开 API 调整系统级调度参数。
- 使用本软件**可能违反 VALORANT、Delta Force 等游戏的用户协议（ToS）**，由此产生的封号或其他后果由用户自行承担，开发者不负任何责任。
- 本项目与 Riot Games、腾讯、TiMi Studio Group 及任何反作弊厂商**无任何关联**，亦未获得其授权。
- 本软件按"**原样**"提供，不附带任何明示或暗示的保证。

---

## License

[MIT](LICENSE) © 2026 SGuardLimiter Contributors
