# FireReplace

A Windows desktop app for customising Fire TV devices over ADB: replace the stock launcher
experience, trim Amazon bloatware, and dial back advertising and animation settings — with explicit
confirmation before anything touches your TV.

## Status

**DEVELOPER BETA / PRE-RELEASE (v0.1.0-beta.1).** This is not a stable release. Features are
incomplete, only a small set of devices has been tried, and there will be bugs. Do not rely on it as
your only copy of a working TV setup — use the built-in backup page before making changes.

## Features

What actually exists in this build:

- **Connect** to a Fire TV over Wi-Fi ADB (`adb connect ip:5555`), with connection status and a
  clear error when ADB is missing or the TV is unreachable.
- **Dashboard** showing device model, Android/Fire OS version, animation and privacy settings, and
  which launcher components are active.
- **Debloat** a vetted list of Amazon packages (`pm disable-user --user 0`), with each package
  classified as *safe to disable*, *optional*, or *CORE — PROTECTED* and never touchable. Protected
  packages and the Fire TV launcher are hard-guarded.
- **Launcher setup**: install Home on Fire and/or Projectivy Launcher from a local `apks` folder,
  grant the permissions they need, and enable accessibility/notification listeners while preserving
  the other launcher's services.
- **Settings** groups (animations, privacy/ads) applied as explicit, reviewable plans.
- **Diagnostics & backups**: see exactly what FireReplace found, export the log, and back up the
  current settings/package state before changing it.
- **Dry-run mode** that previews every command without sending it.

What FireReplace deliberately does **not** do: no bootloader unlocking, no rooting, no firmware
flashing, no wiping or factory reset, no bypassing Fire OS protected-package restrictions, and no
arbitrary shell command box. See [Safety](#safety).

## Requirements

**To run FireReplace:**

- Windows 10 or 11 (x64)
- The .NET 8 Desktop Runtime — only for the framework-dependent build; a self-contained build
  carries its own runtime
- Android SDK Platform Tools (adb.exe) — not bundled; see Quick Start
- A Fire TV device with **Settings → My Fire TV → Developer Options → ADB debugging** enabled
- PC and TV on the same local network

**To build from source:**

- Windows 10/11
- .NET 8 SDK
- Git

## Quick Start

1. Download the latest developer-beta release zip and extract it.
2. Download [SDK Platform-Tools for Windows](https://developer.android.com/tools/releases/platform-tools)
   and copy the extracted `platform-tools` folder next to `FireReplace.exe`, so the layout looks
   like:

   ```
   FireReplace/
       FireReplace.exe
       platform-tools/
           adb.exe
           AdbWinApi.dll
           AdbWinUsbApi.dll
   ```

3. Launch `FireReplace.exe`.
4. On the TV, enable **Settings → My Fire TV → Developer Options → ADB debugging** and
   **Apps from Unknown Sources** (only needed if you plan to install launchers).
5. In FireReplace, enter the TV's IP address (find it under **Settings → My Fire TV → About →
   Network**) and click **Connect**.
6. When the TV asks whether to allow the ADB connection, accept it on the TV.
7. Use the dashboard to check status, the debloat page to disable bloat, and the launcher page to
   install a replacement launcher. A first-run checklist walks you through all of this inside the
   app.

If you already have adb somewhere else, point **Preferences → ADB → Platform tools location** at
that folder instead of copying it.

## Building From Source

```powershell
git clone https://github.com/FireReplace/FireReplace.git
cd FireReplace
dotnet restore FireReplace.sln
dotnet build FireReplace.sln
dotnet test FireReplace.sln
```

Or run the full verification pass (restore + build + test + asset checks + publish check):

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File scripts\verify.ps1
```

There is no Visual Studio requirement — the .NET 8 SDK plus any editor is enough.

## Development

Repository layout:

```
FireReplace.sln
src/
    FireReplace/           WPF application (views, view models, theming)
    FireReplace.Core/      UI-free core: ADB gateway, validation, catalogs, services
tests/
    FireReplace.Tests/     xUnit tests (all use recorded ADB output; no device needed)
scripts/                   build / verify / publish / smoke-test scripts
platform-tools/            (empty; you add adb.exe here — never committed)
apks/                      (empty; you add launcher APKs here — never committed)
legacy/                    the original PowerShell menu this app replaced, kept for reference
artifacts/                 build/test output (gitignored)
```

- `src/FireReplace.Core` is deliberately dependency-free and UI-free; it is where validation,
  argument building and ADB output parsing live, and it is covered by the unit tests.
- Every ADB call goes through `AdbArguments`, which validates every token before it reaches
  `ProcessStartInfo.ArgumentList`. No shell, ever.
- Configuration lives in `%APPDATA%\FireReplace\settings.json`; backups and logs go beside the
  executable (portable-friendly).
- CI (`.github/workflows/ci.yml`) restores, builds, tests and does a publish smoke check on every
  push and pull request. No Fire TV is needed or used by CI.

## Troubleshooting

**"adb.exe was not found"** — Copy `platform-tools` (containing `adb.exe`, `AdbWinApi.dll`,
`AdbWinUsbApi.dll`) next to `FireReplace.exe`, or point Preferences → ADB at your existing copy.
The app shows this state up front on the connect page instead of failing later.

**Fire TV not reachable / connection times out** — Check the IP on the TV (About → Network), make
sure the TV is awake, both devices are on the same network, and ADB debugging is on. Router
"client isolation" or a VPN on the PC will block it.

**The TV shows the authorization prompt but the app says unauthorized** — Accept the prompt on the
TV itself. FireReplace never bypasses that prompt. If it keeps reappearing, re-plug or reboot the TV.

**Install fails with `INSTALL_FAILED_*`** — The APK may be the wrong architecture. Most Fire TV
sticks are `armeabi-v7a`; check the architecture on the dashboard and re-download a matching APK.

**The TV stops responding after disabling packages** — Open the debloat page, re-enable the package
you last changed, and reboot. Every debloat action is reversible; the launcher itself
(`com.amazon.tv.launcher`) is protected and never disabled.

**A command reports a Fire OS "security exception"** — That package is protected by Fire OS.
FireReplace reports this instead of trying to work around it, by design.

**Logs** — The log panel is in-memory by default. Turn on automatic saving in Preferences, or export
from the Diagnostics page, to get a file you can share with a bug report (redact anything you do not
want to publish).

## Safety

FireReplace intentionally avoids dangerous/root-level operations. It will never unlock a bootloader,
root, flash firmware, wipe the device, or attempt to bypass Fire OS protected-package restrictions
— when Fire OS returns a security exception, FireReplace surfaces it as exactly that.

Every modification goes through an explicit confirm step (this can be reviewed per-action and
defaults to on), destructive and advanced groups are visually separated from safe ones, packages are
labelled SAFE / OPTIONAL / PROTECTED, and a configuration backup is offered before the first change
of a session. Dry-run mode shows every command that would be sent without sending it.

FireReplace is an ADB client. It talks to your TV over your LAN, does not phone home, and does not
download anything.

## Known Limitations

- **Tested against one device**: an Insignia Fire TV (Amazon `AFTALMO`, Fire OS 7). Other models and
  Fire OS versions will have different package lists and behaviour.
- Network ADB must be enabled by hand on the TV for the first connect; USB-ADB flows exist but are
  secondary.
- No first-run wizard beyond the in-app checklist, no signed installer, and no auto-update.
- arm64 Windows builds are configured but not exercised in CI.

## Roadmap

- Self-contained single-file release packaging (`FireReplace-win-x64.zip`) via
  `scripts/publish.ps1` (the script exists; wiring it to GitHub Releases is future work).
- Wider device testing and a community-vetted debloat catalog.
- Per-device profiles so settings can be saved and restored across TVs.

## Licence

MIT — see [LICENSE](LICENSE). Android Platform Tools and the launcher APKs are **not** covered by
this licence and are **not** redistributed; you download them from their own projects.
