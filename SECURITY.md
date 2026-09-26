# Security Policy

## Reporting a vulnerability

Please report security issues privately rather than in a public issue.

Use GitHub's **Report a vulnerability** button on the Security tab of this repository (Private
vulnerability reporting). If that is unavailable, open an issue titled `Security contact request`
without any technical detail and a maintainer will arrange a private channel.

Please include:

- What the issue allows an attacker to do
- The steps to reproduce it
- FireReplace version, Windows version, Fire OS version and device model
- Whether a real device was needed to reproduce it

Please do not include ADB keys, network captures containing credentials, or personal data.

### What to expect

- Acknowledgement within 7 days
- An assessment and a plan within 14 days
- Credit in the release notes if you would like it

FireReplace is a volunteer open-source project with no bug bounty.

## Scope

In scope:

- Command injection through any input FireReplace accepts (IP address, package name, file path,
  settings value, APK selection)
- Execution of content returned by the device
- Writing outside the application folder and `%APPDATA%\FireReplace`
- Credentials, tokens or private data appearing in logs, backups or the settings file
- Any path that modifies the device without the confirmation step
- Any path that bypasses the protected-package guards

Out of scope:

- The behaviour of `adb.exe` itself — report that to the Android Platform Tools project
- The behaviour of Home on Fire, Projectivy Launcher or any other APK you install
- Fire OS behaviour and Amazon's own telemetry
- The fact that ADB over the network is unencrypted and unauthenticated beyond the device's own
  key prompt. This is how ADB works; FireReplace does not change it. Enable ADB debugging only on a
  network you trust, and switch it off when you are done.

## How FireReplace handles risk

These are design properties, not aspirations. They are covered by tests in `tests/FireReplace.Tests`.

**No shell, ever.** Every ADB call is an argument array passed to `ProcessStartInfo.ArgumentList`.
FireReplace never builds a command line by string concatenation, and never routes through `cmd /c`
or `powershell -Command`. See `src/FireReplace.Core/Adb/AdbArguments.cs`.

**Everything dynamic is validated first.** IP addresses, package names, component names, settings
keys and file paths pass through `Validate` before they can reach an argument array. Values that
fail validation raise an error instead of being escaped and hoped for.

**Batched reads are restricted.** Status refresh sends several read-only commands in one `adb shell`
invocation for speed. Every token in a batched line must satisfy `Validate.IsShellSafeToken`, which
rejects whitespace, quotes, `;`, `&`, `|`, `$`, backticks, redirection and parentheses. Writes are
never batched.

**Device output is data, not code.** Everything the TV returns is parsed and displayed. Nothing
read from the device is ever executed, and nothing is fed back into a command without validation.
Control characters are stripped before output reaches the log view.

**No arbitrary command surface.** There is no textbox that runs an arbitrary ADB shell command.
The only operations available are the specific ones on the pages, plus a fixed allow-list of three
system settings screens that may be opened on the TV.

**No privilege escalation.** FireReplace runs `asInvoker`. It never asks for elevation, and it does
not need it.

**No outbound network traffic.** FireReplace talks to `adb.exe` on the local machine, which talks to
your TV on your LAN. It does not phone home, check for updates, or send telemetry. It never downloads
an executable or an APK.

**Nothing sensitive is stored.** `%APPDATA%\FireReplace\settings.json` holds the last IP address,
folder paths and UI preferences. No passwords, no tokens, no ADB keys. Backups hold device
properties, settings values and package names only.

**Logs are yours.** Logging is in-memory by default. Writing to a file and exporting are both
explicit user actions.
