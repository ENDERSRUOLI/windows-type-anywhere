# Windows Type Anywhere

[![.NET 10](https://img.shields.io/badge/.NET-10.0--windows-512BD4)](https://dotnet.microsoft.com/download/dotnet/10.0)
[![Platform](https://img.shields.io/badge/platform-Windows-0078D6)](#运行环境)
[![Version](https://img.shields.io/badge/version-0.2.1-2ea44f)](CHANGELOG.md)
[![Status](https://img.shields.io/badge/status-experimental-d97706)](#项目状态)

让 **WSLg 里的 Linux GUI 程序用上 Windows 输入法**。它是一个纯 Windows 原生的 WinForms 托盘工具：在 Windows 原生文本框里用输入法打字，再经剪贴板把文本送进 WSLg、虚拟机或远程桌面窗口。

## 目录

- [项目简介](#项目简介)
- [项目状态](#项目状态)
- [运行环境](#运行环境)
- [安装](#安装)
- [快速开始](#快速开始)
- [快捷键与托盘菜单](#快捷键与托盘菜单)
- [两种模式](#两种模式)
- [配置](#配置)
- [使用建议](#使用建议)
- [已知边界](#已知边界)
- [故障排查](#故障排查)
- [开发与验证](#开发与验证)
- [反馈与贡献](#反馈与贡献)
- [许可证](#许可证)

## 项目简介

WSLg 通过 RDP 展示 Linux GUI 窗口，而 Windows 输入法（TSF/IMM32）只挂在 Windows 原生输入上下文上，GTK/Qt 编辑控件拿不到。把中转文本框做成 WSLg 里的 Linux 窗口，只是把问题复制一遍。

因此本程序在 **Windows 侧**放一个能获得真实焦点的原生 `TextBox` 作为中转框，流程是：

```
目标窗口处于前台 → 唤起中转框 → 用 Windows 输入法键入 → 提交
→ 文本进剪贴板 → 焦点切回目标窗口 → 扫描码注入 Ctrl+V → 延迟恢复原剪贴板
```

- RDP 键盘通道以扫描码为主，把合成好的中文用 `KEYEVENTF_UNICODE` 当按键注入并不可靠（微软文档列出 Qt 应用等不接受字符型键盘输入）；而 Windows ↔ WSLg 之间有 RDP CLIPRDR 文本剪贴板通道。
- 用剪贴板还有一个好处：对端是 X11 还是 Wayland 都无关，纯 Linux 侧方案在原生 Wayland 上会失效。
- 目标窗口由 `EVENT_SYSTEM_FOREGROUND` 自动记录，不需要事先手动选定。
- 全程 **无剪贴板变更监听、无轮询**；前台切换是事件驱动等待。

**适用**：WSLg GUI 程序、VirtualBox / VMware、远程桌面、VNC 等"只是一个能接受粘贴的前台窗口"的场景。
**不适用**：Windows 原生程序 —— 它们本来就能直接用输入法，无需中转。

## 项目状态

当前版本 **0.2.1**，实验性工具，交互行为依赖真实桌面实测。版本规则与历次变更见 [CHANGELOG.md](CHANGELOG.md)；版本号写在 `WindowsTypeAnywhere.csproj` 的 `<Version>`，编译后显示在托盘图标提示中。

按维护者约定：**项目实测稳定前不提供预编译包与 GitHub Release**。Git tag 也暂不启用。

## 运行环境

| 项目 | 要求 |
| --- | --- |
| 系统 | Windows 10 21H2+ / Windows 11（x64） |
| 运行时 | **.NET 10 Desktop Runtime (x64)**，需自行安装 |
| 权限 | `asInvoker` 最小权限，不自动提权 |
| 部署形态 | framework-dependent，非自包含；运行时由 Microsoft Update 打补丁 |

采用非自包含是刻意选择：自包含应用不会随 Microsoft Update 更新运行时，安全补丁要自己重新发布，对长期驻留的托盘程序不划算。

检查运行时：

```powershell
dotnet --list-runtimes
# 应包含 Microsoft.WindowsDesktop.App 10.x.x
```

## 安装

### 方式 A：从源码构建（当前唯一方式）

需要 **.NET 10 SDK**：

```powershell
git clone https://github.com/ENDERSRUOLI/windows-type-anywhere.git
cd windows-type-anywhere
dotnet build -c Release
```

产物在 `bin/Release/net10.0-windows/WindowsTypeAnywhere.exe`，`config.json` 会被复制到同一目录。双击 exe 即可运行，程序没有主窗口，只在系统托盘显示图标。

### 方式 B：发布包

尚未提供预编译 zip 与 GitHub Release，待实测稳定后发布。届时下载解压即可运行 —— 仍是非自包含程序，需要目标机器装有 .NET 10 Desktop Runtime。

## 快速开始

1. 双击 `WindowsTypeAnywhere.exe`，托盘图标出现（默认 `EnabledOnStart=true`，已启用）。
2. 点进 WSLg 应用（或虚拟机）里**需要输入文字的文本框**，让该窗口成为前台窗口 —— 目标就是这么被记录的。
3. 按 `Ctrl+Shift+Space`，中转框出现在目标窗口所在屏幕的工作区底部并获得焦点。
4. 切换到 Windows 输入法，在中转框里键入中文；按 `Enter` 可在框内换行。
5. 按 `Ctrl+Enter` 提交：文本进剪贴板，焦点切回目标窗口并粘贴；按需模式下中转框自动隐藏。
6. 若粘贴失败或找不到目标窗口，文本会**退回中转框**，不会丢失。

### WSLg / 虚拟机窗口的焦点

WSLg 的 RDP 客户端偶尔会在焦点切走后异步把前台抢回去，表现为「中转框已弹出却键不进去」。程序会在唤起 80ms 后复核前台窗口，必要时再切一次焦点；若仍不生效，再按一次 `Ctrl+Shift+Space`（或托盘双击）即可，不需要重新选取目标文本框。

## 快捷键与托盘菜单

| 快捷键 | 作用 |
| --- | --- |
| `Ctrl+Shift+Space` | 唤起中转框（同托盘双击） |
| `Ctrl+Enter` | 提交文本并粘贴到目标窗口 |
| `Ctrl+Shift+F12` | 启用 / 禁用（禁用会卸载键盘钩子并隐藏中转框） |
| `Esc` | 在中转框内按下时隐藏窗口 |
| `Ctrl+V` | 注入到目标窗口的粘贴键（可配置，不是本程序快捷键） |

默认用 `Ctrl+Enter` 而非裸 `Enter`：中文 IME 组合态下按 Enter 是"上屏候选"，会被输入法吃掉，到不了消息循环。

**托盘菜单**：状态（启用/禁用）、模式（按需/监听）、显示中转框、**编辑配置**、**重新载入配置**、退出。双击托盘图标等同于「显示中转框」，把鼠标停在图标上可看到版本号。

### 自定义快捷键

四个热键都能在 `config.json` 里改：`SummonHotkey`、`CommitHotkey`、`ToggleHotkey`、`PasteHotkey`。最方便的做法是托盘菜单「编辑配置」→ 改完保存 → 「重新载入配置」（会重启程序）。

格式是 `Ctrl` / `Alt` / `Shift` / `Win` 加主键，用 `+` 连接，大小写不敏感；主键名取自 `System.Windows.Forms.Keys` 枚举，如 `F12`、`Enter`、`Space`、`V`、`A`。例如：

```json
{
  "SummonHotkey": "Alt+`",
  "CommitHotkey": "Ctrl+Shift+Enter",
  "ToggleHotkey": "Ctrl+Alt+T"
}
```

**带修饰键**走 `RegisterHotKey`；**无修饰键单键**会按需安装 `WH_KEYBOARD_LL` 并吞掉该键 —— 这会让该键在其它程序中也无法正常使用，所以建议始终用组合键。若热键被占用或写法有误，启动时会弹出托盘气泡提示，程序自动回落到 `Ctrl+Enter`。

## 两种模式

- **OnDemand（默认）**：只有按唤起热键才弹出中转框，提交后隐藏。推荐日常使用。
- **Monitoring（实验性）**：中转框常驻显示；**提交后焦点自动交还目标窗口**，中转框保持可见但不抢焦点，所以不必手动点击目标窗口就能继续在那里编辑。

  默认 `MonitoringAutoFocus=true` 时，只有**字母键**（拼音等 IME 组合的起始键）与 IME 处理键会把中转框拉回前台；数字、符号、空格、小键盘、`Enter` / `Backspace` / `Delete` / 方向键 / 功能键以及 `Ctrl` / `Alt` / `Win` 组合都留在目标窗口，可直接输入键盘上已有的字符、删除、换行或执行复制粘贴等快捷键。触发切换的那个字母会被吞掉，以免污染目标窗口，因此**会丢首键**，需要重按一次。

  **局限**：钩子在按键发生前无法区分"想输入 ASCII 字母"和"想用拼音输入中文"，所以自动聚焦开启时，在目标窗口按字母键仍会切到中转框。若需要频繁输入 ASCII 字母，把 `MonitoringAutoFocus` 设为 `false`，或改用按需模式。

  `MonitoringAutoFocus=false` 时监听模式不安装自动聚焦钩子：焦点始终留在目标窗口，需要输入法时按 `Ctrl+Shift+Space`（或托盘双击）显式切到中转框，提交后自动回到目标窗口。这是最不容易产生"劫持感"的用法。

在托盘菜单「模式」切换，或改 `config.json` 的 `Mode`。

## 配置

`config.json` 与 exe 放在同一目录；文件缺失、损坏或字段无法识别时静默回落到默认值。

| 字段 | 默认值 | 说明 |
| --- | --- | --- |
| `Mode` | `OnDemand` | `OnDemand` 或 `Monitoring` |
| `EnabledOnStart` | `true` | 启动时是否启用 |
| `MonitoringAutoFocus` | `true` | 监听模式下是否由字母键自动把中转框拉回前台；`false` 时用唤起热键显式切换 |
| `SummonHotkey` | `Ctrl+Shift+Space` | 唤起中转框 |
| `CommitHotkey` | `Ctrl+Enter` | 提交并粘贴 |
| `ToggleHotkey` | `Ctrl+Shift+F12` | 启用/禁用开关 |
| `PasteHotkey` | `Ctrl+V` | 注入到目标窗口的粘贴组合键 |
| `ForegroundWaitMs` | `400` | 等待目标窗口成为前台的上限（事件驱动，非轮询） |
| `ClipboardDelayMs` | `60` | 切回目标后注入粘贴前的固定短延迟，可设 `0` |
| `RestoreClipboard` | `true` | 是否恢复传输前的剪贴板内容 |
| `RestoreClipboardDelayMs` | `1500` | 成功后延迟恢复剪贴板，避免远端还没取走数据 |
| `RelayTopMost` | `true` | 中转框置顶 |
| `RelayOpacity` | `0.96` | 中转框不透明度（限制在 0.3–1.0） |
| `RelayWidth` / `RelayHeight` | `560` / `110` | 中转框尺寸（最小 240×70） |

## 使用建议

- Windows 原生窗口直接用输入法即可，本工具是给 WSLg / 虚拟机准备的，不要多绕一层。
- 日常优先用**按需模式**；监听模式适合连续中文输入，提交后焦点会自动回目标窗口。
- 若经常要在目标窗口输入数字、符号或 ASCII 字母，把 `MonitoringAutoFocus` 设为 `false`，用热键显式切换最省心。
- 长文本先在中转框里编辑完整（含换行）再提交，比分多次提交更稳。
- 想替换目标文本框里的内容，按标准 Windows 交互先选中再提交粘贴；程序不做"按应用自适应"，也不会发明独有交互逻辑。
- 热键一律用组合键，避免无修饰键单键在其它程序里被吞掉。
- 别在密码框使用：这类控件通常禁用粘贴。
- 若远端粘贴时剪贴板已被恢复，调大 `RestoreClipboardDelayMs` 或把 `RestoreClipboard` 设为 `false`。
- 目标窗口若以管理员身份运行，先实测是否需要手动"以管理员身份运行"本程序；默认保持最小权限，不主动提权。
- 改配置后记得「重新载入配置」，否则要重启程序才生效。

## 已知边界

- 监听模式是实验性的：`MonitoringAutoFocus=true` 时会丢掉触发焦点切换的字母键，且无法区分 ASCII 字母与拼音输入；关闭该选项可规避。
- 提交/粘贴期间会暂停"抢焦点"，避免注入的 `Ctrl+V` 被送回中转框。
- IME 组合态下按提交键的优先级问题未处理，用 `Ctrl+Enter` 规避。
- 目标应用失焦后内部插入符是否保留需实测；密码框通常禁用粘贴。
- 剪贴板恢复延时与远端取数存在竞态，可调 `RestoreClipboardDelayMs` 或关闭 `RestoreClipboard`。
- UIPI / 更高完整性级别窗口未做规避；只有实测确有必要时才手动提权。
- 焦点往返是设计固有的：IME 必须挂在真正获得焦点的原生控件上，`WS_EX_NOACTIVATE` 之类"置顶但不抢焦点"的优化会直接让输入法失效。

## 故障排查

| 现象 | 处理 |
| --- | --- |
| 热键无反应 | 看托盘气泡提示；用「编辑配置」换一组组合键后「重新载入配置」 |
| 没粘贴进去 | 确认目标窗口提交前曾是前台窗口、托盘状态为已启用、目标控件允许粘贴 |
| 中转框不显示 | 托盘菜单「显示中转框」，或检查 `RelayOpacity` / `RelayTopMost` |
| 监听模式下字母键被吞 | 自动聚焦的触发键，会被用来切换焦点；关闭 `MonitoringAutoFocus` 或改用按需模式 |
| 提示缺少运行时 | 安装 .NET 10 Desktop Runtime (x64) 后重启程序 |
| 文本丢失 | 失败时文本会退回中转框；托盘双击重新唤出即可看到 |

## 开发与验证

```powershell
dotnet build -c Release
```

当前构建为 **0 警告 0 错误**。不启动 GUI 的部分已验证：

- `config.json` 的 15 个配置键均能被 `AppConfig.Load` 正确读出；配置文件缺失时回落默认值。
- 4 个默认热键均解析为有效「修饰键 + VK」；无修饰键单键走钩子路径；非法按键抛 `FormatException`。
- 监听模式按键分类：字母 `A-Z` 与 IME 处理键为自动聚焦触发键；数字、符号、空格、小键盘、编辑键、功能键与修饰键均排除。
- P/Invoke 结构体 x64 布局：`INPUT=40`（与 Win32 一致，`SendInput` 的 `cbSize` 正确）、`INPUTUNION=32`、`KEYBDINPUT=24`、`KBDLLHOOKSTRUCT=24`、`RECT=16`。
- `MapVirtualKey(VK→VSC)` 对 Ctrl / V / Enter / F12 / Space 均返回有效扫描码。

**仍需真实桌面人工验证**：隐藏窗口接收 `WM_HOTKEY`、焦点往返与 RDP 粘贴、IME 组合态行为、剪贴板恢复时机、监听模式的按键分类是否符合实际输入习惯。

## 反馈与贡献

欢迎通过 [GitHub Issues](https://github.com/ENDERSRUOLI/windows-type-anywhere/issues) 反馈。报告问题时请附：目标程序类型（WSLg / 虚拟机 / Windows 原生）、当前模式、`config.json` 中的 `Mode` / `MonitoringAutoFocus` / 热键配置，以及具体按键顺序与现象。

提交改动前请跑 `dotnet build -c Release`；版本号变更同步更新 `CHANGELOG.md` 与 `WindowsTypeAnywhere.csproj`。

## 许可证

尚未指定许可证。如需再分发或商用，请先与维护者确认；建议后续补充 `LICENSE` 文件。
