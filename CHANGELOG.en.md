# Changelog

[简体中文](CHANGELOG.md) | **English**

This project uses semantic versioning `MAJOR.MINOR.PATCH`. The version is set by `<Version>` in `WindowsTypeAnywhere.csproj` and shown in the tray tooltip. The first Git tag and GitHub Release are `v0.2.2` (Pre-release).

## 0.2.2

- Added a CapsLock heuristic: when CapsLock is on, mainstream Chinese IMEs emit uppercase English and never enter a composition state, so letter keys no longer trigger auto-focus and uppercase ASCII can be typed directly. Turning CapsLock off restores Chinese input triggering.
- Documented that the auto-focus decision uses Chinese pinyin IMEs as its reference model; other languages may need different trigger rules.
- Digits, symbols, space, numpad and editing/function/modifier combinations remain excluded; those keys still work for candidate paging when the relay box has focus.
- Adopted MPL-2.0: `LICENSE` is the official text and C# sources carry the Exhibit A notice. First release set to `v0.2.2` (Pre-release).

## 0.2.1

- Fixed Monitoring mode leaving focus on the relay box after commit: focus is now returned to the target window after the commit delay, while the relay box stays visible without taking focus — no manual click needed.
- Narrowed auto-focus triggers to letter keys `A-Z` and the IME processing key; digits, symbols, space, numpad, edit keys, function keys and `Ctrl` / `Alt` / `Win` combinations stay in the target window.
- The triggering key is now swallowed so it cannot be typed into the target window; the tradeoff is that the first key is still lost.
- Added `MonitoringAutoFocus` (default `true`): setting it to `false` disables auto-focus, uses the summon hotkey for explicit switching, and returns focus to the target after commit.
- Documented the limitation that letter keys cannot distinguish ASCII from pinyin input, and how to avoid it.

## 0.2.0

- Monitoring mode now routes only "character-writing" keys to the relay box; `Enter`, `Backspace`, `Delete`, arrows, function keys and `Ctrl` / `Alt` / `Win` combinations stay in the target, so deleting, newlines and shortcuts work there.
- Fixed auto-focus reclaiming focus during commit/paste, which sent `Ctrl+V` into the relay box.
- Fixed WSLg/RDP asynchronously reclaiming the foreground, which prevented typing after the first summon.
- Added "Edit config" to the tray menu (opens `config.json` directly) and the version in the tray tooltip.
- Restructured README to standard GitHub conventions with installation, tips and feedback sections.

## 0.1.0

- Initial version: native Windows WinForms relay box + clipboard + scan-code `Ctrl+V`, covering WSLg, VM and remote-desktop windows.
- OnDemand / Monitoring modes, arbitrary hotkey combinations, clipboard snapshot and delayed restore, least-privilege manifest.
- Target framework `net10.0-windows` (LTS); build with 0 warnings, 0 errors.
