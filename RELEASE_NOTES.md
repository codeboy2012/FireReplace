# FireReplace 0.1.0-beta.1 — Developer Beta

**This is a pre-release.** Expect bugs, incomplete polish, and limited device coverage. It is not a
stable 1.0.

## What's Included

- WPF desktop app (Windows 10/11 x64, .NET 8) for managing Fire TV devices over Wi-Fi ADB
- Connect to a Fire TV (`adb connect ip:5555`) with clear status and actionable errors
- Dashboard: device model, Android/Fire OS version, animation and privacy settings, launcher state
- Debloat page: disable/enable a vetted list of Amazon packages, with SAFE / OPTIONAL / PROTECTED
  classification; the Fire TV launcher and other core packages are hard-guarded and never offered
- Launcher page: install Home on Fire and/or Projectivy Launcher from a local `apks` folder, grant
  permissions, and enable accessibility/notification listeners while preserving the other launcher
- Settings groups (animations, privacy/ads) applied as reviewable plans
- Diagnostics & backups: environment facts, log export, and device-state backup/restore
- Dry-run mode that previews every command without sending it
- First-run safety checklist shown before the main UI

## What's New

This is the first public developer beta — everything above is new in this release. Compared with the
legacy PowerShell menu (kept in `legacy/` for reference), the app adds a full UI, per-action
confirmation, backups, dry-run mode, and input validation on every ADB call.

## Known Issues

- Only verified against one device (Insignia Fire TV, Amazon model `AFTALMO`, Fire OS 7). Other
  models/Fire OS versions may have different package names and behaviour.
- The debloat catalog is small and conservative by design.
- No installer and no auto-update: you download a zip (or build from source).
- arm64 Windows builds are configured but not exercised in CI.
- Network ADB must be enabled manually on the TV for a first connect.

## Testing

What was tested:

- `dotnet build` and `dotnet test` (298 unit tests, all passing) — all tests use recorded ADB
  output; none require a Fire TV
- Release build and publish succeed for win-x64
- Application launches, renders the main window, and handles missing adb.exe, missing APKs, a
  corrupt/absent settings file, and a missing Fire TV without crashing

What was **not** tested:

- NOT TESTED — REQUIRES FIRE TV: connect, debloat, launcher installation, settings writes, backups
  against a real device in this build
- NOT TESTED — ENVIRONMENT LIMITATION: the arm64 publish target and Windows 7/8 compatibility

## Installation

1. Download `FireReplace-win-x64.zip` from the release page (or build from source with
   `dotnet build` and run from `src/FireReplace/bin/Release/net8.0-windows/`).
2. Download [SDK Platform-Tools for Windows](https://developer.android.com/tools/releases/platform-tools)
   and place the `platform-tools` folder next to `FireReplace.exe`.
3. Run `FireReplace.exe`. The .NET 8 Desktop Runtime must be installed for a framework-dependent
   build; a self-contained build does not need it.

## Feedback

Please report bugs on the GitHub issue tracker, including your Fire TV model, Fire OS version, and
what you expected versus what happened. Security issues: see [SECURITY.md](SECURITY.md) — do not
post them publicly.
