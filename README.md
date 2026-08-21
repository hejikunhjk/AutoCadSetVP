[![Apache License 2.0](https://img.shields.io/badge/License-Apache%202.0-blue.svg)](LICENSE)
[![.NET](https://img.shields.io/badge/.NET-8.0-blue.svg)](https://dotnet.microsoft.com/)

# SetVP

## 项目简介 / Project Overview

SetVP 是一款面向 AutoCAD 2024+ 的 Windows 插件，用于在指定布局视口中快速应用和恢复参考图层的颜色覆盖状态，适合 Xref / reference layer 的批量检查与可视化管理。

SetVP is a Windows plugin for AutoCAD 2024+ that helps users quickly apply and restore color overrides for reference layers in selected layout viewports. Designed for efficient review and visual management of Xref or reference-layer states in drafting workflows.

## 功能特性 / Features

- 视口范围选择 / viewport scope selection
- 参考图层筛选 / reference-layer filtering
- 批量颜色覆盖应用 / quick color override application
- 原始颜色状态恢复 / restore original layer color state
- 设置持久化保存 / persisted settings using JSON
- AutoCAD 内部模型窗体交互 / modeless interaction inside AutoCAD

## 系统要求 / Prerequisites

- AutoCAD 2024 或更高版本 / AutoCAD 2024 or later
- Windows 10/11 x64
- .NET 8 Runtime（AutoCAD 2024+ 通常已内置 / usually bundled with AutoCAD 2024+）

## 快速安装 / Quick Install

- 下载 [dist/SetVP.dll](dist/SetVP.dll) 和 [dist/load_setvp.lsp](dist/load_setvp.lsp) / Download [dist/SetVP.dll](dist/SetVP.dll) and [dist/load_setvp.lsp](dist/load_setvp.lsp)

### 方法一：自动加载（推荐）/ Auto Load (Recommended)

1. 将两个文件放入 AutoCAD 支持搜索路径，例如： / Place both files into an AutoCAD support search path, for example:
   ```
   C:\Users\你的用户名\AppData\Roaming\Autodesk\AutoCAD 2026\R26\chs\Support\
   ```
2. 在 AutoCAD 命令行运行： / Run in AutoCAD command line:
   ```
   ap
   ```
3. 然后加载： / Then load:
   ```
   C:\Users\你的用户名\AppData\Roaming\Autodesk\AutoCAD 2026\R26\chs\Support\load_setvp.lsp
   ```
4. 添加到启动组以便以后自动加载 / Add to Startup group for automatic loading on next launch

5. 输入 `SETVP` 启动插件 / Type `SETVP` to launch the plugin

### 方法二：直接使用 / Direct Use (Recommended)

1. 将两个文件放入 AutoCAD 支持搜索路径，例如： / Place both files into an AutoCAD support search path, for example:
   ```
   C:\Users\你的用户名\AppData\Roaming\Autodesk\AutoCAD 2026\R26\chs\Support\
   ```
2. 在 AutoCAD 命令行运行： / Run in AutoCAD command line:
   ```
   (load "load_setvp")
   ```
3. 输入 `SETVP` 启动插件 / Type `SETVP` to launch the plugin

### 方法三：NETLOAD / Method 2: NETLOAD

1. 在 AutoCAD 命令行输入 `NETLOAD` / Type `NETLOAD` in AutoCAD command line
2. 选择 `dist/SetVP.dll` / Select `dist/SetVP.dll`
3. 输入 `SETVP` 运行 / Type `SETVP` to run

## 编译构建 / Build from Source

```powershell
git clone https://github.com/hejikunhjk/AutoCadSetVP.git
cd AutoCadSetVP
C:\Program Files\dotnet\dotnet.exe build "SetVP\SetVP.csproj" -c Release -nologo
```

编译产物位置 / Output: `dist\SetVP.dll`

## 主要命令 / Main Commands

| 命令 / Command | 说明 / Description |
|------|------|
| `SETVP` | 启动插件主界面 |
| | Launch plugin main UI |
| `NETLOAD` | 加载 DLL 到当前 AutoCAD 会话 |
| | Load DLL into current AutoCAD session |

## 项目结构 / Project Structure

```
SetVP/
├── SetVP.csproj              # 项目文件
├── SetVPExtApp.cs            # 插件入口
├── Commands/
│   └── SetVPCommands.cs      # SETVP 命令定义
├── Services/
│   ├── ColorOverrideService.cs    # 颜色覆盖逻辑
│   ├── LayerSelectionService.cs   # 图层枚举与筛选
│   ├── ViewportSelectionService.cs # 视口选择
│   ├── SelectionStateService.cs   # 选择状态管理
│   └── SettingsManager.cs         # 配置持久化
├── Models/
│   ├── AppSettings.cs
│   └── ReferenceLayerInfo.cs
└── UI/
    └── SetVpForm.cs          # WinForms 主窗体
```

## 使用说明 / Usage

### 视口作用域 / Viewport Scope

| 模式 / Mode | 说明 / Description |
|------|------|
| 当前布局视口 / Current Layout Viewport | 对当前布局内所有视口窗口生效 |
| | Apply to all viewport windows in current layout |
| 当前视口 / Current Viewport | 仅对当前活动视口生效 |
| | Apply to current active viewport only |
| 选择视口 / Select Viewport | 切换到该模式 → 点"应用" → 在 CAD 绘图区域框选视口 → 按 Enter 确认（Esc 取消）|
| | Switch to this mode → Click "Apply" → Select viewports in CAD drawing area → Press Enter to confirm (Esc to cancel) |

> **注意**："选择视口"模式下，框选操作在 CAD 绘图区域进行，窗体会暂时隐藏，确认后恢复。
> **Note**: In "Select Viewport" mode, the selection is made in the CAD drawing area, the form will be temporarily hidden, and restored after confirmation.

### 颜色选择 / Color Selection

| 模式 / Mode | 说明 / Description |
|------|------|
| ByLayer | 移除视口覆盖，让图层恢复为 ByLayer（随图层颜色）|
| | Remove viewport override, restore layer to ByLayer color |
| ACI | 标准 ACI 颜色索引（1-9, 250-259），10列3行网格布局 |
| | Standard ACI color index (1-9, 250-259), 10-column by 3-row grid layout |
| RGB | 点击弹出系统颜色对话框选择任意颜色，显示十六进制色号（RGB #RRGGBB）|
| | Click to open system color dialog for custom color, displays hex color code (RGB #RRGGBB) |



### 应用与还原 / Apply & Restore

- **应用**：将选定颜色覆盖到所选视口的选定图层 /
  **Apply**: Apply the selected color override to the chosen layers in selected viewports
- **还原**：将所有视口中被覆盖的图层恢复为原始颜色 /
  **Restore**: Restore all overridden layers in all viewports to their original colors
  - 初始状态：还原按钮灰掉（无可还原内容） /
    Initial state: Restore button is grayed out (nothing to restore)
  - 应用后：还原按钮变为可用 /
    After apply: Restore button becomes enabled
  - 还原后：还原按钮再次灰掉 /
    After restore: Restore button is grayed out again

### 界面说明 / UI

- 每次打开 `SETVP` 窗口均为最小尺寸（不记忆窗口大小） /
  Every time `SETVP` window opens, it is in minimum size (window size is not remembered)
- 窗口可手动调整大小，图层列表区域随窗口拉伸 /
  Window can be manually resized, layer list area stretches with the window
- ESC 键关闭窗体 /
  Press ESC to close the form

## 许可证 / License

Apache License 2.0 — 详见 [LICENSE](LICENSE) / see [LICENSE](LICENSE).
