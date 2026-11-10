# Windows Type Anywhere

解决 **WSLg 中的 Linux GUI 程序无法使用 Windows 输入法键入中文** 的问题。这是一个纯 Windows 原生的 WinForms 托盘程序，同样适用于任意虚拟机、远程桌面、VNC 等"只是一个能接受粘贴的前台窗口"的场景。

## 原理

- Windows 输入法（TSF/IMM32）只挂在 Windows 原生输入上下文上，Linux 的 GTK/Qt 编辑控件拿不到 —— 所以中转文本框必须是 **Windows 原生 TextBox**，不能放进 WSLg，否则只是把问题复制一遍。
- RDP 键盘通道以扫描码为主，把合成好的中文用 `KEYEVENTF_UNICODE` 当按键注入并不可靠（微软文档明确列出 Qt 应用等不接受字符型键盘输入）。而 Windows ↔ WSLg 之间有 RDP CLIPRDR 文本剪贴板通道。
- 因此走 **原生文本框 → 剪贴板 → 扫描码 Ctrl+V**。对端是 X11 还是 Wayland 都无关，也天然覆盖各类虚拟机。
- 目标窗口由 `EVENT_SYSTEM_FOREGROUND` 自动记录，不需要事先手动选定。

流程：目标窗口处于前台 → 唤起中转框 → 用 Windows 输入法键入 → 提交 → 文本进剪贴板 → 焦点切回目标窗口 → 注入 Ctrl+V → 延迟恢复原剪贴板。全程 **无剪贴板变更监听、无轮询**。

## 运行环境

| 项目 | 要求 |
| --- | --- |
| 系统 | Windows 10 21H2+ / Windows 11 |
| 运行时 | **.NET 10 Desktop Runtime (x64)**，需使用者自行安装 |
| 权限 | `asInvoker` 最小权限，不自动提权 |
| 部署形态 | framework-dependent，非自包含；运行时由 Microsoft Update 打补丁 |

采用非自包含是刻意选择：自包含应用不会随 Microsoft Update 更新运行时，安全补丁要自己重新发布，对长期驻留的托盘程序不划算。

检查运行时是否已装：

```powershell
dotnet --list-runtimes
# 应包含 Microsoft.WindowsDesktop.App 10.x.x
```

## 构建

需要 .NET 10 SDK：

```powershell
dotnet build -c Release
```

产物：`bin/Release/net10.0-windows/WindowsTypeAnywhere.exe`。当前构建为 **0 警告 0 错误**。

## 快速开始

1. 双击 `WindowsTypeAnywhere.exe`。程序无主窗口，只在系统托盘显示图标（默认 `EnabledOnStart=true`，已启用）。
2. 点进 WSLg 应用（或虚拟机）里**需要输入文字的文本框**，让该窗口成为前台窗口 —— 目标就是这么被记录的。
3. 按 `Ctrl+Shift+Space`，中转框出现在目标窗口所在屏幕的工作区底部并获得焦点。
4. 切换到 Windows 输入法，在中转框里键入中文。
5. 按 `Ctrl+Enter` 提交。文本被剪切进剪贴板，焦点切回目标窗口并粘贴；按需模式下中转框自动隐藏。
6. 若粘贴失败或找不到目标窗口，文本会**退回中转框**，不会丢失。

右键托盘图标可操作：状态（启用/禁用）、模式（按需/监听）、显示中转框、重新载入配置、退出。**双击托盘图标**等同于"显示中转框"。改完 `config.json` 后用"重新载入配置"重启程序生效。

### 默认快捷键

| 快捷键 | 作用 |
| --- | --- |
| `Ctrl+Shift+Space` | 唤起中转框（同托盘双击） |
| `Ctrl+Enter` | 提交文本并粘贴到目标窗口 |
| `Ctrl+Shift+F12` | 启用 / 禁用（禁用会卸载键盘钩子并隐藏中转框） |
| `Esc` | 在中转框内按下时隐藏窗口 |
| `Ctrl+V` | 注入到目标窗口的粘贴键（可配置，非本程序快捷键） |

默认用 `Ctrl+Enter` 而非裸 `Enter`：中文 IME 组合态下按 Enter 是"上屏候选"，会被输入法吃掉，到不了消息循环。

### 两种模式

- **OnDemand（默认）**：只有按唤起热键才弹出中转框，提交后隐藏。推荐先用这个实测。
- **Monitoring（实验性）**：启用后常驻低级键盘钩子，在任何键入前把中转框拉回前台，提交后中转框保持显示。在钩子回调里同步切焦点属 best-effort，**可能丢掉第一个键**。可在托盘菜单"模式"切换，或改 `config.json` 的 `Mode`。

## 配置

`config.json` 与 exe 放在同一目录；文件缺失、损坏或字段无法识别时静默回落到默认值。

| 字段 | 默认值 | 说明 |
| --- | --- | --- |
| `Mode` | `OnDemand` | `OnDemand` 或 `Monitoring` |
| `EnabledOnStart` | `true` | 启动时是否启用 |
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

热键格式：`Ctrl` / `Alt` / `Shift` / `Win` 加主键，用 `+` 连接，大小写不敏感；主键名取自 `System.Windows.Forms.Keys` 枚举，如 `F12`、`Enter`、`Space`、`V`、`A`。**带修饰键**走 `RegisterHotKey`；**无修饰键单键**会按需安装 `WH_KEYBOARD_LL` 并吞掉该键 —— 这会让该键在其它程序中也无法正常使用，所以建议始终用组合键。

## 已完成的自动化验证

不启动 GUI 的部分已在 Windows / **.NET 10 SDK 10.0.404** 下验证通过：

- `dotnet build -c Release` / `Rebuild`：0 警告 0 错误；`runtimeconfig.json` 指向 `net10.0` + `Microsoft.WindowsDesktop.App 10.0.0`。
- `AppConfig.Load` 正确读出全部 14 个配置项；配置文件缺失时回落默认值。
- 4 个默认热键均解析为非零修饰键 + 有效 VK；无修饰键单键 `F12` 走钩子路径；非法按键抛 `FormatException`。
- P/Invoke 结构体 x64 布局：`RECT=16`、`KBDLLHOOKSTRUCT=24`、`KEYBDINPUT=24`、`MOUSEINPUT=32`、`HARDWAREINPUT=8`、`INPUTUNION=32`、**`INPUT=40`**（与 Win32 一致，`SendInput` 的 `cbSize` 正确）。
- `MapVirtualKey(VK→VSC)` 对 Ctrl/V/Enter/F12/Space 均返回有效扫描码。

**以下必须在真实桌面人工实测**：隐藏窗口能否收到 `WM_HOTKEY`、焦点往返与 RDP 粘贴是否成功、IME 组合态行为、剪贴板恢复时机。

## 实测清单（请按序验证并反馈）

1. 启动后出现托盘图标，右键菜单显示"状态：已启用"。若有"快捷键注册失败"气泡，说明热键被其它程序占用或格式写错。
2. 打开一个 WSLg GUI 程序，点进可输入文本框，按 `Ctrl+Shift+Space` —— 中转框应出现在屏幕底部并获得焦点。
3. 切到中文输入法键入一段中文（含候选词上屏），按 `Ctrl+Enter` —— 文本应出现在目标文本框，中转框隐藏。
4. 约 1.5 秒后检查剪贴板是否已恢复成传输前的内容。
5. 单独验证 `Esc` 取消、托盘双击、启用/禁用切换、`Monitoring` 模式、改 `config.json` 后"重新载入配置"。
6. 重点反馈：是否丢第一个键、目标窗口失焦后插入符是否保留、热键是否被占用、更高完整性级别（管理员运行的）目标窗口是否注入失败。

## 已知边界

- 监听模式是实验性的，可能丢第一个键；默认仍是按需模式。
- IME 组合态下按提交键的优先级问题按约定未处理，用 `Ctrl+Enter` 规避。
- 目标应用失焦后内部插入符是否保留需实测；密码框通常禁用粘贴。
- 剪贴板恢复延时与远端取数存在竞态，可调 `RestoreClipboardDelayMs` 或关闭 `RestoreClipboard`。
- UIPI/更高完整性级别窗口未做规避；按最小权限原则，只有实测确有必要时才手动"以管理员身份运行"本程序。
- 焦点往返是设计固有的：IME 必须挂在真正获得焦点的原生控件上，`WS_EX_NOACTIVATE` 之类"置顶但不抢焦点"的优化会直接让输入法失效。

## 故障排查

| 现象 | 处理 |
| --- | --- |
| 热键无反应 | 看托盘气泡提示；改 `config.json` 换一组组合键后"重新载入配置" |
| 没粘贴进去 | 确认目标窗口提交前曾是前台窗口、托盘状态为已启用、目标控件允许粘贴 |
| 中转框不显示 | 托盘菜单"显示中转框"，或检查 `RelayOpacity` / `RelayTopMost` |
| 提示缺少运行时 | 安装 .NET 10 Desktop Runtime (x64) 后重启程序 |
| 文本丢失 | 设计上失败会退回中转框；若中转框被关闭，托盘双击重新唤出即可看到 |
