<p align="center">
  <img src="https://raw.githubusercontent.com/codeboy2012/FireReplace/refs/heads/main/assets/icon.png" width="140" alt="FireReplace">
</p>

<h1 align="center">FireReplace</h1>

<p align="center">
  <strong>Make your Fire TV yours.</strong>
</p>

<p align="center">
  Windows desktop customization for Fire TV over ADB.
</p>

<p align="center">
  <a href="https://github.com/codeboy2012/FireReplace/releases">Download</a>
  ·
  <a href="https://github.com/codeboy2012/FireReplace/issues">Report an Issue</a>
</p>

> FireReplace is still in beta and has **a lot more to come**.
>
> Expect bugs, incomplete features, compatibility issues, and changes between releases.
>
> **Please report problems through the GitHub Issues tab.**
>
> If you like FireReplace, consider **starring the repository**. It helps the project grow!

[![Developer Beta](https://img.shields.io/badge/status-DEVELOPER%20BETA-orange)](https://github.com/codeboy2012/FireReplace/releases)
[![Latest Release](https://img.shields.io/github/v/release/codeboy2012/FireReplace?include_prereleases&label=latest%20beta)](https://github.com/codeboy2012/FireReplace/releases)
[![Issues](https://img.shields.io/github/issues/codeboy2012/FireReplace)](https://github.com/codeboy2012/FireReplace/issues)
[![License](https://img.shields.io/github/license/codeboy2012/FireReplace)](LICENSE)

---

# Getting Started

## What do I need?

You need:

-  A Windows 10 or Windows 11 PC
-  A compatible Fire TV
-  Your PC and Fire TV connected to the same network
-  ADB Debugging enabled on the Fire TV
-  Android Platform-Tools (ADB)

**You do NOT need to install .NET for the current Windows release.**

The developer-beta release is distributed as a self-contained Windows build.

---

#  Step 1 — Download FireReplace

Go to:

 **[Download the latest FireReplace release](https://github.com/codeboy2012/FireReplace/releases)**

Download the newest:

```text
FireReplace-win-x64.zip
```

Extract the ZIP somewhere convenient.

For example:

```text
C:\Apps\FireReplace\
```

or:

```text
Downloads\FireReplace\
```

Then run:

```text
FireReplace.exe
```

---

#  Step 2 — Install ADB

FireReplace uses Android Debug Bridge (ADB) to communicate with your Fire TV.

Download **Android SDK Platform-Tools for Windows** from Google:

 https://developer.android.com/tools/releases/platform-tools

Download the Windows ZIP and extract it.

Your folder should contain:

```text
platform-tools/
├── adb.exe
├── AdbWinApi.dll
├── AdbWinUsbApi.dll
└── ...
```

### Recommended setup

Put the `platform-tools` folder next to FireReplace:

```text
FireReplace/
├── FireReplace.exe
└── platform-tools/
    ├── adb.exe
    ├── AdbWinApi.dll
    └── AdbWinUsbApi.dll
```

That's it.

You don't need to install Android Studio.

If you already have ADB installed somewhere else, FireReplace can use it through:

**Preferences → ADB → Platform Tools Location**

---

#  Step 3 — Enable ADB on Your Fire TV

On your Fire TV:

**Settings → My Fire TV → Developer Options**

Make sure:

### ADB Debugging

is enabled.

If you plan to install APKs or launchers, you may also need:

### Apps from Unknown Sources

depending on your Fire OS version.

---

#  Step 4 — Find Your Fire TV's IP Address

On the Fire TV:

**Settings → My Fire TV → About → Network**

Find:

```text
IP Address
```

It will look something like:

```text
192.168.1.147
```

Your PC and Fire TV need to be on the same local network.

---

# 🔌 Step 5 — Connect

Open:

```text
FireReplace.exe
```

Enter your Fire TV's IP address.

Click:

**Connect**

The first time you connect, your Fire TV may display an authorization prompt.

Look at your TV and select:

**Allow**

FireReplace will then connect to the device.

>  FireReplace does not bypass the Android ADB authorization system.

---

#  You're Connected!

Once connected, FireReplace can show information about your Fire TV and provide access to the supported customization tools.

Before changing anything:

### Recommended first steps

1.  Check the Dashboard
2.  Create a backup
3.  Use Dry Run when available
4.  Review what a change will do
5.  Confirm the change
6.  Test your TV before making more changes

**Don't change everything at once.**

Especially while FireReplace is still in beta.

---

#  What FireReplace Can Do

##  Dashboard

See useful information about your connected Fire TV, including supported device and software information.

---

##  Debloat

FireReplace includes a curated list of Fire TV packages that can be disabled.

Packages are categorized as:

 **SAFE**

Generally intended for supported customization.

 **OPTIONAL**

May affect functionality depending on your setup.

 **PROTECTED / CORE**

Protected from modification.

FireReplace intentionally does **not** attempt to bypass Fire OS security restrictions.

---

##  Launcher Setup

FireReplace can help configure supported launcher tools such as:

- Home on Fire
- Projectivy Launcher

The application handles supported installation and permission steps while attempting to preserve required Fire TV services.

---

##  Performance

Manage supported Fire TV animation settings and other available performance-related options.

---

##  Privacy & Advertising

Manage supported Fire TV privacy and advertising-related settings.

---

##  Backups

Create backups of supported package and settings information before making changes.

**Use backups before making significant changes.**

---

##  Dry Run

Not sure what something will do?

Use **Dry Run**.

It allows you to preview supported operations before they are sent to your Fire TV.

---

##  Diagnostics & Logs

FireReplace provides logs and diagnostic information to help understand what happened when something goes wrong.

When reporting an issue, you may be asked to provide relevant logs.

**Remove private information before posting logs publicly.**

---

#  Safety

FireReplace is designed to make supported Fire TV customization easier without encouraging dangerous system modifications.

FireReplace does **not** provide:

-  Bootloader unlocking
-  Rooting
-  Firmware flashing
-  Factory reset / wiping
-  Recovery destruction
-  Protected-package bypasses
-  Arbitrary unrestricted shell commands

If Fire OS says that something is protected, FireReplace reports the restriction rather than trying to bypass it.

---

# IMPORTANT: This Is a Beta

FireReplace is **not a finished 1.0 application**.

This is a:

##  DEVELOPER BETA

That means you may encounter:

- Bugs
- Crashes
- Missing features
- UI changes
- Device compatibility problems
- Incorrect package classifications
- Fire OS version differences
- Features that don't work on your particular TV

Only a limited number of Fire TV devices have been tested so far.

**Please don't assume that something tested on one Fire TV will behave identically on another.**

---

#  Found a Bug?

**Please report it!**

Go to:

 **[GitHub Issues](https://github.com/codeboy2012/FireReplace/issues)**

Before opening an issue:

1. Search for an existing issue first.
2. Include your Fire TV model.
3. Include your Fire OS version.
4. Explain what you were doing.
5. Explain what you expected.
6. Explain what actually happened.
7. Include relevant logs if possible.

Please remove passwords, private information, tokens, or other sensitive information before posting logs.

### Example

```text
Fire TV:
Insignia AFTALMO

Fire OS:
7.x

FireReplace:
0.1.0-beta.1

What I did:
Connected the TV and attempted to disable a package.

Expected:
The package would be disabled.

What happened:
FireReplace displayed an error.

Additional information:
[relevant log]
```

The more information you provide, the easier it is to reproduce and fix the problem.

---

#  Updating FireReplace

FireReplace is actively being developed, so new beta releases may appear regularly.

Check:

 **[GitHub Releases](https://github.com/codeboy2012/FireReplace/releases)**

for the newest version.

You'll see versions such as:

```text
0.1.0-beta.1
0.1.0-beta.2
0.1.0-beta.3
```

### Before updating

Read the release notes.

A new beta may contain:

-  Bug fixes
-  New features
-  Compatibility changes
-  UI improvements
-  Additional Fire TV support
-  Safety changes

### Current beta

**Automatic updating is not implemented yet.**

For now:

1. Open the Releases page.
2. Download the newest ZIP.
3. Extract it.
4. Replace your previous FireReplace application.
5. Keep your backups.

Automatic update support is planned for a future version.

---

#  Developers

Want to build FireReplace yourself?

## Requirements

- Windows 10/11
- .NET 8 SDK
- Git

Clone the repository:

```powershell
git clone https://github.com/codeboy2012/FireReplace.git
cd FireReplace
```

Restore:

```powershell
dotnet restore FireReplace.sln
```

Build:

```powershell
dotnet build FireReplace.sln
```

Run tests:

```powershell
dotnet test FireReplace.sln
```

Full verification:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File scripts\verify.ps1
```

---

#  Project Structure

```text
FireReplace/
│
├── src/
│   ├── FireReplace/
│   │   └── WPF application
│   │
│   └── FireReplace.Core/
│       └── UI-independent core
│
├── tests/
│   └── FireReplace.Tests/
│
├── scripts/
│   └── Build / test / publish tools
│
├── assets/
│   └── Application assets
│
├── platform-tools/
│   └── Local ADB location
│
├── apks/
│   └── Local APK location
│
├── legacy/
│   └── Original PowerShell predecessor
│
├── .github/
│   └── GitHub Actions
│
├── FireReplace.sln
├── README.md
├── RELEASE_NOTES.md
├── LICENSE
└── SECURITY.md
```

Build output and other machine-specific files are intentionally excluded from GitHub.

---

#  Privacy

FireReplace communicates with your Fire TV over your local network.

It does not require an online account.

It does not need to phone home to perform its core functions.

FireReplace does not intentionally collect your Fire TV information for a remote service.

---

#  What's Coming?

FireReplace is still very early in development.

There is **a LOT more planned.**

Some areas being worked toward include:

-  Easier app installation
-  Automatic update support
-  More Fire TV device testing
-  Expanded debloat support
-  Better backup/restore
-  Per-device profiles
-  More diagnostics
-  More customization options
-  Easier standalone distribution
-  Continued UI improvements

The roadmap can change as development and beta testing continue.

---

# Like FireReplace?

If you find FireReplace useful:

## ⭐ Star the repository

It helps the project get noticed.

 **[⭐ Star FireReplace on GitHub](https://github.com/codeboy2012/FireReplace)**

##  Report issues

 **[Open an Issue](https://github.com/codeboy2012/FireReplace/issues)**

##  Suggest improvements

Feature ideas and compatibility reports are welcome.

##  Contribute

Developers are welcome to fork the project, make improvements, test them, and submit pull requests.

---

#  License

FireReplace is licensed under the **MIT License**.

See [LICENSE](LICENSE).

Android SDK Platform-Tools and third-party launcher APKs are not covered by the FireReplace license and are not redistributed by this repository. Obtain them from their respective projects.

---

# FireReplace

**Customize your Fire TV without living in a terminal.**

**Developer Beta — expect bugs, report issues, and stay tuned.**

**There's a lot more coming.**
