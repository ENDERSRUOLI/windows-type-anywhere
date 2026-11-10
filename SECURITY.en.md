# Security Policy

[简体中文](SECURITY.md) | **English**

## Supported versions

| Version | Supported |
| --- | --- |
| 0.2.x | Yes |
| 0.1.x and earlier | No |

The project is at an early stage; security fixes target only the latest release. Please upgrade from older versions first.

## Reporting a vulnerability

Please **do not** report security issues in a public issue.

Use GitHub's private reporting channel first: repository → **Security** → **Report a vulnerability** (Private vulnerability reporting). If that is not enabled yet, contact the maintainer `ENDERSRUOLI` via a private GitHub message with `SECURITY` in the subject, and do not post details publicly.

Include the affected version, reproduction conditions (target window type / mode / CapsLock state / key sequence), impact, and any suggested fix. We will coordinate the fix and disclosure with you after confirmation.

## Security boundaries

This is a local Windows tray tool that **does not listen on the network, provide a network service, or upload any data**. It does, however, install a global low-level keyboard hook, read and write the system clipboard, and inject keystrokes into the foreground window — the boundaries of those capabilities are the security-relevant part:

- The keyboard hook is installed only when Monitoring auto-focus is enabled or a modifierless single-key hotkey is configured; it is uninstalled immediately when the app is disabled or exits.
- Clipboard contents are snapshotted and restored only at commit time; there is no continuous monitoring, and nothing is written to disk or logs.
- The app runs at `asInvoker` least privilege and does not auto-elevate; injection into higher-integrity (UIPI) windows is not worked around.

For functional bugs rather than security issues, please use the [issue form](.github/ISSUE_TEMPLATE/bug_report.yml) publicly.
