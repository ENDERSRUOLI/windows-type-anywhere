# Windows Type Anywhere

[简体中文](README.md) | **English**

[![.NET 10](https://img.shields.io/badge/.NET-10.0--windows-512BD4)](https://dotnet.microsoft.com/download/dotnet/10.0)
[![Platform](https://img.shields.io/badge/platform-Windows-0078D6)](#requirements)
[![Version](https://img.shields.io/badge/version-0.2.2-2ea44f)](CHANGELOG.en.md)
[![Status](https://img.shields.io/badge/status-experimental-d97706)](#project-status)
[![License](https://img.shields.io/badge/license-MPL--2.0-brightgreen)](LICENSE)

Use a **Windows IME to type into WSLg Linux GUI apps**. It is a native Windows WinForms tray tool: type with your IME into a Windows textbox, then deliver the text into a WSLg, VM, or remote-desktop window via the clipboard.

## Contents

- [How it works](#how-it-works)
- [Project status](#project-status)
- [Requirements](#requirements)
- [Installation](#installation)
- [Quick start](#quick-start)
- [Hotkeys and tray menu](#hotkeys-and-tray-menu)
- [Two modes](#two-modes)
- [Configuration](#configuration)
- [Tips](#tips)
- [Known limitations](#known-limitations)
- [Troubleshooting](#troubleshooting)
- [Development and verification](#development-and-verification)
- [Feedback and contributing](#feedback-and-contributing)
- [License](#license)

## How it works

WSLg displays Linux GUI windows over RDP, but a Windows IME (TSF/IMM32) only attaches to a **native Windows input context** — GTK/Qt edit controls cannot receive it. Putting the relay textbox inside WSLg would simply reproduce the problem.

So this app places a real-focus native `TextBox` on the **Windows side**:

```
target window in foreground → summon relay box → type with Windows IME → commit
→ text to clipboard → focus returns to target → Ctrl+V injected as scan codes → clipboard restored later
```

- RDP carries keyboard input mainly as scan codes; injecting composed Unicode via `KEYEVENTF_UNICODE` is unreliable (Microsoft documents Qt apps that reject character-based keyboard input). Windows ↔ WSLg does have an RDP CLIPRDR text clipboard channel.
- Using the clipboard also means it does not matter whether the remote side is X11 or Wayland (pure Linux-side tooling breaks on native Wayland).
- The target window is recorded automatically via `EVENT_SYSTEM_FOREGROUND`; there is no manual "select target" step.
- **No clipboard-change listeners and no polling**; foreground switching uses an event-driven wait.

**Use it for**: WSLg GUI apps, VirtualBox / VMware, remote desktop, VNC — anything that is "just a foreground window that accepts paste".
**Not needed for**: native Windows apps, which already work with the IME directly.

## Project status

Current version **0.2.2**. This project is at an **early stage**: the first release, `v0.2.2`, is marked **Pre-release** and is not claimed to be stable. Interactive behavior depends on real-desktop testing; see [known limitations](#known-limitations) and [Issues](https://github.com/ENDERSRUOLI/windows-type-anywhere/issues). Version history: [CHANGELOG.en.md](CHANGELOG.en.md). The version is set by `<Version>` in `WindowsTypeAnywhere.csproj` and shown in the tray tooltip.

Licensed under MPL-2.0 — see [LICENSE](LICENSE).

## Requirements

| Item | Requirement |
| --- | --- |
| OS | Windows 10 21H2+ / Windows 11 (x64) |
| Runtime | **.NET 10 Desktop Runtime (x64)**, installed separately |
| Privileges | `asInvoker`, least privilege; no auto-elevation |
| Deployment | framework-dependent, not self-contained; runtime patched by Microsoft Update |

Staying framework-dependent is deliberate: self-contained apps do not receive runtime security patches via Microsoft Update, which is a poor tradeoff for a long-running tray tool.

Check the runtime:

```powershell
dotnet --list-runtimes
# should include Microsoft.WindowsDesktop.App 10.x.x
```

## Installation

### Option A: build from source

Requires the **.NET 10 SDK**:

```powershell
git clone https://github.com/ENDERSRUOLI/windows-type-anywhere.git
cd windows-type-anywhere
dotnet build -c Release
```

Output: `bin/Release/net10.0-windows/WindowsTypeAnywhere.exe`, with `config.json` copied alongside. Double-click the exe; there is no main window, just a tray icon.

### Option B: release package

The first package corresponds to `v0.2.2` (Pre-release) and is distributed as a framework-dependent zip on [GitHub Releases](https://github.com/ENDERSRUOLI/windows-type-anywhere/releases). Unzip and run the exe. The zip **does not contain the runtime**.

## Quick start

1. Double-click `WindowsTypeAnywhere.exe`; a tray icon appears (`EnabledOnStart=true` by default).
2. Click into the text field of a WSLg app (or VM) so that window becomes the foreground window — that is how the target is recorded.
3. Press `Ctrl+Shift+Space`; the relay box appears at the bottom of the target's screen and takes focus.
4. Switch to your Windows IME and type. `Enter` inserts a newline inside the relay box.
5. Press `Ctrl+Enter`: text goes to the clipboard, focus returns to the target and pastes; in OnDemand mode the relay box hides.
6. If pasting fails or no target is available, the text is **returned to the relay box** rather than lost.

### Focus with WSLg / VM windows

The WSLg RDP client sometimes asynchronously reclaims the foreground after a focus change, which looks like "the relay box is visible but I cannot type into it". The app re-checks the foreground window 80 ms after summoning and retries the switch if needed. If it still fails, press `Ctrl+Shift+Space` (or double-click the tray icon) once more; you do not need to re-select the target field.

## Hotkeys and tray menu

| Hotkey | Action |
| --- | --- |
| `Ctrl+Shift+Space` | Summon relay box (same as tray double-click) |
| `Ctrl+Enter` | Commit and paste into the target |
| `Ctrl+Shift+F12` | Enable / disable (uninstalls the keyboard hook and hides the relay box) |
| `Esc` | Hide the relay box when it has focus |
| `Ctrl+V` | Paste keys injected into the target (configurable, not an app hotkey) |

`Ctrl+Enter` is used instead of bare `Enter` because in an IME composition state Enter commits the candidate and never reaches the message loop.

**Tray menu**: status (enable/disable), mode (OnDemand/Monitoring), show relay box, **edit config**, **reload config**, exit. Double-clicking the tray icon shows the relay box; hover for the version.

### Custom hotkeys

All four hotkeys are configurable in `config.json`: `SummonHotkey`, `CommitHotkey`, `ToggleHotkey`, `PasteHotkey`. The easiest route is tray menu → "Edit config" → save → "Reload config" (restarts the app).

Format: `Ctrl` / `Alt` / `Shift` / `Win` plus a main key joined by `+`, case-insensitive; main key names come from `System.Windows.Forms.Keys` (`F12`, `Enter`, `Space`, `V`, `A`, ...). For example:

```json
{
  "SummonHotkey": "Alt+`",
  "CommitHotkey": "Ctrl+Shift+Enter",
  "ToggleHotkey": "Ctrl+Alt+T"
}
```

Hotkeys **with modifiers** use `RegisterHotKey`. A **single key without modifiers** installs `WH_KEYBOARD_LL` on demand and swallows that key — it will no longer work normally in other applications, so combinations are recommended. If a hotkey is taken or malformed, a tray balloon warns at startup and the app falls back to `Ctrl+Enter`.

## Two modes

- **OnDemand (default)**: the relay box appears only when summoned and hides after commit. Recommended for daily use.
- **Monitoring (experimental)**: the relay box stays visible; after a commit **focus is returned to the target window**, and the relay box stays visible without taking focus, so you can keep editing there without clicking the target manually.

  With `MonitoringAutoFocus=true` (default), only **letter keys** (the start of an IME composition) and IME processing keys pull the relay box to the foreground. Digits, symbols (including `+ - [ ]`), space, numpad, `Enter` / `Backspace` / `Delete` / arrows / function keys and `Ctrl` / `Alt` / `Win` combinations stay in the target window, so you can type characters that already exist on the keyboard, delete, insert newlines, or use shortcuts. The letter that triggers the switch is swallowed to avoid polluting the target, so the **first key is lost** and must be pressed again. These symbol keys are excluded only when the relay box does not have focus; when it does, they still work normally for candidate paging and similar IME operations.

  **CapsLock heuristic**: with CapsLock on, mainstream Chinese IMEs emit uppercase English and never enter a composition state, so letter keys no longer trigger auto-focus — you can type uppercase ASCII directly into the target. Turning CapsLock off restores Chinese input triggering. This gives users an explicit switch without editing the config.

  **Limitation**: a low-level hook cannot tell "I want lowercase ASCII" from "I want pinyin" before the key is pressed; CapsLock only covers the uppercase case. If you often type lowercase letters in the target, set `MonitoringAutoFocus` to `false` or use OnDemand mode.

  With `MonitoringAutoFocus=false`, Monitoring installs no auto-focus hook: focus stays on the target, and you switch to the relay box explicitly with `Ctrl+Shift+Space` (or a tray double-click) when you need the IME; focus returns to the target after commit. This is the least intrusive setup.

  **Scope**: the key classification uses Chinese pinyin IMEs as its reference model. IMEs for other languages may need different trigger keys — please open an Issue with the language and key details before we consider making this configurable.

Switch modes from the tray menu, or set `Mode` in `config.json`.

## Configuration

`config.json` lives next to the exe; a missing or invalid file silently falls back to defaults.

| Key | Default | Meaning |
| --- | --- | --- |
| `Mode` | `OnDemand` | `OnDemand` or `Monitoring` |
| `EnabledOnStart` | `true` | Enable at startup |
| `MonitoringAutoFocus` | `true` | Whether letter keys pull the relay box forward in Monitoring mode; `false` uses the summon hotkey explicitly |
| `SummonHotkey` | `Ctrl+Shift+Space` | Summon relay box |
| `CommitHotkey` | `Ctrl+Enter` | Commit and paste |
| `ToggleHotkey` | `Ctrl+Shift+F12` | Enable/disable toggle |
| `PasteHotkey` | `Ctrl+V` | Paste combination injected into the target |
| `ForegroundWaitMs` | `400` | Max wait for the target to become foreground (event-driven, no polling) |
| `ClipboardDelayMs` | `60` | Fixed short delay before injecting paste; `0` disables it |
| `RestoreClipboard` | `true` | Restore the pre-transfer clipboard |
| `RestoreClipboardDelayMs` | `1500` | Delay before restoring, so the remote side can read the data |
| `RelayTopMost` | `true` | Keep relay box on top |
| `RelayOpacity` | `0.96` | Relay box opacity (clamped to 0.3–1.0) |
| `RelayWidth` / `RelayHeight` | `560` / `110` | Relay box size (minimum 240×70) |

## Tips

- Native Windows apps already support the IME — this tool is for WSLg / VMs, do not add an extra hop.
- Prefer **OnDemand** for daily use; Monitoring suits continuous Chinese input, and focus returns to the target after each commit.
- Turn on `CapsLock` to type a run of uppercase English in the target; auto-focus steps aside. Turn it off before typing Chinese.
- If you frequently type lowercase letters, digits or symbols in the target, set `MonitoringAutoFocus` to `false` and switch with the hotkey.
- Compose long text (including newlines) in the relay box before committing rather than committing in many small pieces.
- To replace text in the target, select it first using standard Windows interaction; the app does no per-application adaptation and invents no new interaction model.
- Always use hotkey combinations; a single modifierless key gets swallowed in other apps too.
- Do not use it on password fields, which usually block pasting.
- If the clipboard is restored before the remote side reads it, increase `RestoreClipboardDelayMs` or set `RestoreClipboard` to `false`.
- If the target runs elevated, test whether you actually need to run this app as administrator; it stays at least privilege by default.
- Remember "Reload config" after editing the file, or restart the app.

## Known limitations

- Monitoring is experimental: with `MonitoringAutoFocus=true` the letter that triggers the switch is dropped, and lowercase ASCII cannot be distinguished from pinyin. Disable the option to avoid both.
- Key classification targets Chinese pinyin IMEs; composition-start keys for other languages are not covered.
- Auto-focus is suspended during commit/paste so injected `Ctrl+V` is not sent back into the relay box.
- IME composition-state priority for the commit key is not handled; `Ctrl+Enter` is used to avoid it.
- Whether the target keeps its caret after losing focus needs real-world testing; password fields usually block paste.
- Clipboard restore delay vs. remote read is a race; tune `RestoreClipboardDelayMs` or disable `RestoreClipboard`.
- UIPI / higher-integrity windows are not worked around; elevate manually only if testing shows it is necessary.
- The focus round-trip is inherent: the IME must attach to a genuinely focused native control, so "always-on-top without taking focus" tricks such as `WS_EX_NOACTIVATE` would break it.

## Troubleshooting

| Symptom | Action |
| --- | --- |
| Hotkey does nothing | Check the tray balloon; pick another combination in "Edit config" and "Reload config" |
| Nothing pasted | Confirm the target was foreground before commit, the app is enabled, and the control allows paste |
| Relay box not visible | Tray menu → "Show relay box"; check `RelayOpacity` / `RelayTopMost` |
| A letter is swallowed in Monitoring | That letter triggered auto-focus; disable `MonitoringAutoFocus` or use OnDemand |
| Letters do not switch focus with CapsLock on | Expected — you can type uppercase English directly; turn CapsLock off to restore |
| Missing runtime prompt | Install .NET 10 Desktop Runtime (x64) and restart |
| Text lost | On failure text returns to the relay box; double-click the tray icon to show it |

## Development and verification

```powershell
dotnet build -c Release
```

Current build: **0 warnings, 0 errors**. Verified without launching the GUI:

- All 15 `config.json` keys load correctly via `AppConfig.Load`; a missing file falls back to defaults.
- All four default hotkeys resolve to valid modifier + VK; a modifierless single key takes the hook path; an invalid key throws `FormatException`.
- Monitoring key classification: `A-Z` and the IME processing key trigger auto-focus; digits, symbols, space, numpad, edit keys, function keys and modifiers are excluded; CapsLock disables all triggers.
- x64 P/Invoke layout: `INPUT=40` (matches Win32, so `SendInput`'s `cbSize` is correct), `INPUTUNION=32`, `KEYBDINPUT=24`, `KBDLLHOOKSTRUCT=24`, `RECT=16`.
- `MapVirtualKey(VK→VSC)` returns valid scan codes for Ctrl / V / Enter / F12 / Space.

**Still requires manual desktop testing**: hidden window receiving `WM_HOTKEY`, focus round-trip and RDP paste, IME composition behavior, clipboard restore timing, and whether the Monitoring key classification fits real typing habits.

## Feedback and contributing

- **Bug reports**: use the [issue form](.github/ISSUE_TEMPLATE/bug_report.yml) — target window type, mode, CapsLock state, `config.json` and key sequence matter most.
- **Security issues**: see [SECURITY.en.md](SECURITY.en.md); please do not open a public issue.
- **Contributing**: see [CONTRIBUTING.en.md](CONTRIBUTING.en.md); for PRs use the [PR template](.github/pull_request_template.md).

When changing the version, also update `CHANGELOG.md` / `CHANGELOG.en.md` and the README badges. By contributing you agree your contribution is provided under MPL-2.0.

## License

**Mozilla Public License 2.0** ([MPL-2.0](LICENSE), SPDX: `MPL-2.0`), Copyright (c) 2026 ENDERSRUOLI.

MPL-2.0 is **file-level weak copyleft**:

- You may use, modify and distribute (including commercially), with an express patent grant from contributors.
- When you **distribute modified MPL-covered files**, those files and your modifications must remain available as source under MPL-2.0, with copyright/license notices preserved and changes marked; you may not restrict recipients' rights in those files.
- You may combine these files with your own proprietary files into a Larger Work under terms of your choice; the newly added files need not be open-sourced.
- Obligations trigger on **distribution**; private modifications used locally need not be published.

C# source files carry the MPL Exhibit A notice and an SPDX identifier. Files where comments are impractical (such as `config.json`) are covered by the root `LICENSE`, which MPL Exhibit A permits.
