# Fire TV Command Menu

A beginner-friendly Windows PowerShell menu for repeating the Fire TV customization setup.

## Folder layout

Put these files in the same folder:

```text
FireTV-Command-Menu/
├── FireTV-Command-Menu.ps1
├── adb.exe
├── home-on-fire.apk
└── Projectivy-*.apk
```

The Projectivy APK can keep its release filename as long as it starts with `Projectivy` and ends in `.apk`.

## What it does

The menu lets you choose individual operations instead of automatically changing everything.

Included:

- Connect to a Fire TV over ADB
- Check model / Android / build
- Install Home on Fire
- Install Projectivy Launcher
- Grant Home on Fire `WRITE_SECURE_SETTINGS`
- Enable Projectivy accessibility while preserving Home on Fire accessibility
- Enable Projectivy notification access
- Disable the 18 verified packages from the original setup
- Apply the tested animation settings
- Apply the tested privacy/advertising settings
- View current status
- Re-enable a package
- Reboot the TV
- Run a recommended setup with an explicit confirmation
- Apply the two groups of optional/unconfirmed settings only after a separate confirmation

## What it deliberately does NOT do

The script does not:

- Bypass protected Fire OS packages
- Require root
- Unlock the bootloader
- Flash firmware
- Factory reset or wipe the TV
- Disable the Fire TV launcher
- Disable AirPlay/HomeKit components
- Disable OTA updates
- Automatically grant every permission requested by an app

## Prerequisites

1. A Windows PC.
2. `adb.exe` from Android SDK Platform Tools.
3. Home on Fire APK.
4. Projectivy Launcher APK compatible with the Fire TV architecture.
5. A Fire TV with ADB/network debugging enabled.
6. The PC and TV on the same network.

The original reference device was:

- Insignia Fire TV
- Amazon model `AFTALMO`
- Product `almond`
- Android 9
- Fire OS `7.01065.5585`
- ARMv7 / `armeabi-v7a`

Other Fire OS versions/models may have different package names or behavior. Test carefully.

## Running it

Open PowerShell in the folder and run:

```powershell
Set-ExecutionPolicy -Scope Process Bypass
.\FireTV-Command-Menu.ps1
```

The execution-policy command only affects the current PowerShell session.

The menu will first ask for the Fire TV's IP address and connect to:

```text
IP:5555
```

Example:

```text
192.168.1.147
```

## ADB setup on the TV

On Fire TV, enable the developer/ADB debugging options through the normal Fire TV settings.

When the TV asks whether to allow the computer's ADB connection, accept it on the TV.

Do not use this project to bypass an ADB authorization prompt.

## Recommended workflow

For a first setup:

1. Connect to the TV.
2. Run device/status checks.
3. Install the two APKs.
4. Grant Home on Fire `WRITE_SECURE_SETTINGS`.
5. Enable Projectivy accessibility.
6. Optionally enable Projectivy notification access.
7. Apply animation settings.
8. Apply privacy/ad settings.
9. Review the 18-package list.
10. Disable the 18 verified packages.
11. Reboot.
12. Test the Home button, launcher, streaming apps, HDMI/TV input, audio, AirPlay/HomeKit, and Home Assistant automations.

## Important safety notes

The package list was tested against one Insignia Fire TV configuration. Do not assume every Fire TV has identical internals.

Core packages intentionally preserved by the original setup include:

```text
com.amazon.tv.launcher
com.amazon.airplaydaemon
com.amazon.connectivitycontroller
com.amazon.device.messaging
com.amazon.whisperlink.core.android
com.amazon.whisperjoin.middleware.np
com.amazon.firehomestarter
com.amazon.device.software.ota
com.amazon.vizzini
com.amazon.aria
com.amazon.hedwig
com.amazon.dp.logger
```

Protected packages encountered during the original work:

```text
com.amazon.ftvads.deeplinking
com.amazon.client.metrics
com.amazon.device.metrics
```

The original setup received Fire OS Security Exceptions when trying to modify protected packages. This project intentionally does not attempt to bypass those protections.

## Reverting a package

Use menu option `12` and enter the package name.

Or manually:

```powershell
.\adb.exe shell pm enable PACKAGE.NAME
```

## Troubleshooting

### ADB cannot connect

Check:

```powershell
.\adb.exe devices
```

Make sure:

- The TV is powered on.
- The IP is correct.
- The PC and TV are on the same LAN.
- ADB/network debugging is enabled.
- The TV has accepted the PC's authorization prompt.

### Projectivy accessibility replaced Home on Fire

The script is specifically written to preserve:

```text
io.github.toolicious.homeonfire/.HijackService
```

and add:

```text
com.spocky.projengmenu/.services.ProjectivyAccessibilityService
```

Check the current value with:

```powershell
.\adb.exe shell settings get secure enabled_accessibility_services
```

### Notification access command fails

Open the Fire TV notification-listener settings:

```powershell
.\adb.exe shell am start -a android.settings.ACTION_NOTIFICATION_LISTENER_SETTINGS
```

Then enable Projectivy from the system UI.

## License

Choose a license before publishing this repository. MIT is a simple option if you want others to freely use and modify the menu, but the repository owner should make the final licensing choice.

## Credits / sources

This project is a practical automation wrapper around standard Android ADB operations and the specific Fire TV customization workflow documented in this repository.

The APKs themselves remain subject to their respective projects' licenses and distribution terms. Do not redistribute an APK unless its license/source permits it.
