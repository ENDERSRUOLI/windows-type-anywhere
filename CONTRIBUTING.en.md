# Contributing Guide

[简体中文](CONTRIBUTING.md) | **English**

Thanks for your interest. This project is at an early stage; reproducible bug reports and focused changes are both valuable.

## Environment

- Target framework `net10.0-windows`; building requires the **.NET 10 SDK**, running requires the **.NET 10 Desktop Runtime (x64)**.
- This is a WinForms tray application with no main window. `EnableWindowsTargeting` allows cross-building from WSL later.
- Interactive behavior (focus switching, IME composition, RDP paste) **can only be verified on a real Windows desktop**; automated checks cannot cover it. Please state your test environment in the PR description.

## Build

```powershell
dotnet build -c Release
```

Confirm **0 warnings, 0 errors** before submitting. Basic non-GUI checks (config loading, hotkey parsing, P/Invoke struct layout, key classification) can be done by reflecting over the built assembly.

## Submitting changes

- Keep one commit focused on one problem; describe what changed and why.
- When changing the version, update `<Version>` in `WindowsTypeAnywhere.csproj`, both `CHANGELOG.md` / `CHANGELOG.en.md`, and the README badges.
- Keep the MPL Exhibit A notice, copyright line and `SPDX-License-Identifier: MPL-2.0` at the top of C# files you add or edit.
- For bug reports use the [issue form](.github/ISSUE_TEMPLATE/bug_report.yml), especially the key sequence and `config.json`. For security issues see [SECURITY.en.md](SECURITY.en.md).

## Design conventions

- The relay box must be a **native Windows control**: Windows IMEs (TSF/IMM32) only attach to a native input context, so it cannot live inside WSLg.
- The target window is recorded automatically via `EVENT_SYSTEM_FOREGROUND`; do not add a manual target-selection step.
- No clipboard-change listeners and no polling; foreground switching uses an event-driven wait.
- Least privilege by default (`asInvoker`); no auto-elevation and no UIPI workarounds.
- No invented text-interaction model: replacing text follows the standard Windows "select first, then paste" behavior.
- Text is delivered via scan codes rather than Unicode characters, because RDP/WSLg carries keyboard input mainly as scan codes.

## Documentation languages

User-facing documents come in Chinese (main file) and English (`.en.md`), linked at the top of each. Short template files use bilingual labels in a single file. Keep both language versions in sync when editing behavior or version numbers. `LICENSE` stays the official English MPL-2.0 text.

## License

This project uses [MPL-2.0](LICENSE). By contributing you agree your contribution is provided under MPL-2.0; when distributing modified MPL-covered files, those files and their modifications must remain available as source under MPL-2.0.
