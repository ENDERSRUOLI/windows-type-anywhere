# 贡献指南

感谢参与。项目处于早期阶段，可复现的问题报告与聚焦的改动都很有价值。

## 环境

- 目标框架 `net10.0-windows`，构建需要 **.NET 10 SDK**；运行需要 **.NET 10 Desktop Runtime (x64)**。
- 这是 WinForms 托盘程序，没有主窗口；`EnableWindowsTargeting` 允许日后从 WSL 交叉构建。
- 交互行为（焦点切换、IME 组合、RDP 粘贴）**只能在真实 Windows 桌面验证**，自动化验证覆盖不到。请在 PR 描述里写明实测环境。

## 构建

```powershell
dotnet build -c Release
```

提交前确认 **0 警告 0 错误**。不启动 GUI 的基本验证（配置加载、热键解析、P/Invoke 结构体布局、按键分类）可用反射读取产物程序集完成。

## 提交改动

- 一个提交聚焦一个问题，提交信息说明「改了什么、为什么」。
- 涉及版本号时，同步更新 `WindowsTypeAnywhere.csproj` 的 `<Version>`、`CHANGELOG.md` 与 `README.md` 的版本徽章。
- 新增或修改 C# 源文件时，保留文件头的 MPL Exhibit A 声明、版权行与 `SPDX-License-Identifier: MPL-2.0`。
- 报告问题请使用 [问题报告模板](.github/ISSUE_TEMPLATE/bug_report.yml)，尤其是按键顺序与 `config.json`；安全问题见 [SECURITY.md](SECURITY.md)。

## 设计约定

- 中转框必须是 **Windows 原生控件**：Windows 输入法（TSF/IMM32）只挂在原生输入上下文上，不能把它移进 WSLg。
- 目标窗口由 `EVENT_SYSTEM_FOREGROUND` 自动记录，不引入手动选定步骤。
- 不做剪贴板变更监听、不做轮询；前台切换用事件驱动等待。
- 默认最小权限（`asInvoker`），不主动提权，也不规避 UIPI。
- 不发明独有的文本交互逻辑：替换文本遵循标准 Windows 的「先选中、再粘贴」。
- 文本注入用扫描码而非 Unicode 字符，因为 RDP/WSLg 主要按扫描码传输键盘输入。

## 许可

本项目采用 [MPL-2.0](LICENSE)。提交贡献即表示同意你的贡献按 MPL-2.0 提供；修改并分发 MPL 覆盖的文件时，这些文件及其修改须继续以 MPL-2.0 提供源码。
