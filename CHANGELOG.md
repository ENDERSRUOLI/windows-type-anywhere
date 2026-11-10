# 变更记录

本项目采用语义化版本号 `MAJOR.MINOR.PATCH`。版本号写在 `WindowsTypeAnywhere.csproj` 的 `<Version>`，编译后显示在托盘图标提示中。Git tag 与 GitHub Release 待实测稳定后启用。

## 0.2.0

- 监听模式改为只把「会写入字符」的按键导向中转框；`Enter`、`Backspace`、`Delete`、方向键、功能键以及 `Ctrl` / `Alt` / `Win` 组合保留在目标窗口，可在目标文本框里删除、换行和执行快捷键。
- 修复监听模式在提交/粘贴期间抢回焦点，导致 `Ctrl+V` 被送进中转框的问题。
- 修复 WSLg/RDP 窗口异步抢回前台，导致首次唤起中转框后无法键入的问题。
- 托盘菜单新增「编辑配置」，直接打开 `config.json`；托盘图标提示显示程序版本。
- README 按 GitHub 规范重整，补充安装、使用建议与反馈方式。

## 0.1.0

- 初版：Windows 原生 WinForms 中转框 + 剪贴板 + 扫描码 `Ctrl+V`，覆盖 WSLg、虚拟机与远程桌面窗口。
- 按需 / 监听双模式、任意组合键热键、剪贴板快照与延迟恢复、最小权限清单。
- 目标框架 `net10.0-windows`（LTS），构建 0 警告 0 错误。
