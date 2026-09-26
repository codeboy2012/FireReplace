$ErrorActionPreference = "Stop"

# FireTV Command Menu
# Keep this script, adb.exe, home-on-fire.apk, and the Projectivy APK in the same folder.

$ScriptRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$Adb = Join-Path $ScriptRoot "adb.exe"
$HomeOnFireApk = Join-Path $ScriptRoot "home-on-fire.apk"

# Put the Projectivy APK in this folder. The script finds the first matching APK.
$ProjectivyApk = Get-ChildItem -Path $ScriptRoot -Filter "Projectivy*.apk" -File -ErrorAction SilentlyContinue |
    Select-Object -First 1

$DisabledPackages = @(
    "com.amazon.bueller.photos",
    "com.amazon.shoptv.firetv.client",
    "com.android.nfc",
    "com.amazon.perfc",
    "com.amazon.tv.democontent.provider",
    "com.amazon.sneakpeek",
    "com.amazon.storm.lightning.tutorial",
    "com.amazon.wirelessmetrics.service",
    "com.amazon.tmm.tutorial",
    "com.amazon.audiohome",
    "com.amazon.tv.support",
    "com.amazon.kso.blackbird",
    "com.amazon.shoptv.client",
    "com.amazon.tv.releasenotes",
    "com.amazon.gamehub",
    "com.amazon.perfcollection",
    "com.amazon.bueller.music",
    "com.amazon.media.recommendations"
)

function Pause-Menu {
    Write-Host ""
    Read-Host "Press Enter to continue"
}

function Header {
    Clear-Host
    Write-Host "=============================================" -ForegroundColor Cyan
    Write-Host "       Fire TV Command Menu" -ForegroundColor Cyan
    Write-Host "=============================================" -ForegroundColor Cyan
    if ($script:Device) {
        Write-Host "Connected device: $script:Device" -ForegroundColor Green
    } else {
        Write-Host "No device connected" -ForegroundColor Yellow
    }
    Write-Host ""
}

function Run-Adb {
    param([Parameter(Mandatory=$true)][string[]]$Args)

    & $Adb @Args
    if ($LASTEXITCODE -ne 0) {
        throw "ADB command failed with exit code $LASTEXITCODE."
    }
}

function Confirm-Action {
    param([string]$Message)

    $answer = Read-Host "$Message [y/N]"
    return $answer -match '^(y|yes)$'
}

function Require-Files {
    if (-not (Test-Path -LiteralPath $Adb)) {
        throw "adb.exe was not found next to this script: $Adb"
    }

    if (-not (Test-Path -LiteralPath $HomeOnFireApk)) {
        throw "home-on-fire.apk was not found next to this script."
    }

    if (-not $ProjectivyApk) {
        throw "No Projectivy*.apk was found next to this script."
    }

    Write-Host "ADB:       $Adb" -ForegroundColor DarkGray
    Write-Host "Home Fire: $HomeOnFireApk" -ForegroundColor DarkGray
    Write-Host "Projectivy:$($ProjectivyApk.FullName)" -ForegroundColor DarkGray
}

function Connect-Device {
    Header
    $ip = Read-Host "Enter the Fire TV IP address (example: 192.168.1.147)"

    if ([string]::IsNullOrWhiteSpace($ip)) {
        Write-Host "No IP entered." -ForegroundColor Red
        Pause-Menu
        return
    }

    Write-Host ""
    Write-Host "Connecting to $ip`:5555..." -ForegroundColor Cyan
    & $Adb connect "$ip`:5555"

    if ($LASTEXITCODE -ne 0) {
        Write-Host "ADB could not connect." -ForegroundColor Red
        Pause-Menu
        return
    }

    $script:Device = "$ip`:5555"

    Write-Host ""
    Write-Host "Checking device..." -ForegroundColor Cyan
    & $Adb devices
    Write-Host ""
    Write-Host "Model:      " -NoNewline
    & $Adb shell getprop ro.product.model
    Write-Host "Product:    " -NoNewline
    & $Adb shell getprop ro.build.product
    Write-Host "Android:    " -NoNewline
    & $Adb shell getprop ro.build.version.release
    Write-Host "Build:      " -NoNewline
    & $Adb shell getprop ro.build.version.incremental

    Pause-Menu
}

function Require-Connection {
    if (-not $script:Device) {
        Write-Host "Connect to the TV first." -ForegroundColor Yellow
        Pause-Menu
        return $false
    }
    return $true
}

function Install-Apps {
    if (-not (Require-Connection)) { return }

    Header
    Write-Host "Install APKs" -ForegroundColor Cyan
    Write-Host "1. Home on Fire"
    Write-Host "2. Projectivy"
    Write-Host "3. Both"
    Write-Host "0. Cancel"
    $choice = Read-Host "Choose"

    try {
        switch ($choice) {
            "1" {
                Run-Adb @("install","-r",$HomeOnFireApk)
                Write-Host "Home on Fire installed." -ForegroundColor Green
            }
            "2" {
                Run-Adb @("install","-r",$ProjectivyApk.FullName)
                Write-Host "Projectivy installed." -ForegroundColor Green
            }
            "3" {
                Run-Adb @("install","-r",$HomeOnFireApk)
                Run-Adb @("install","-r",$ProjectivyApk.FullName)
                Write-Host "Both apps installed." -ForegroundColor Green
            }
        }
    } catch {
        Write-Host $_ -ForegroundColor Red
    }
    Pause-Menu
}

function Grant-HomeOnFire {
    if (-not (Require-Connection)) { return }

    Header
    if (Confirm-Action "Grant Home on Fire WRITE_SECURE_SETTINGS?") {
        try {
            Run-Adb @("shell","pm","grant","io.github.toolicious.homeonfire","android.permission.WRITE_SECURE_SETTINGS")
            Write-Host "Granted." -ForegroundColor Green
        } catch {
            Write-Host $_ -ForegroundColor Red
        }
    }
    Pause-Menu
}

function Enable-ProjectivyAccessibility {
    if (-not (Require-Connection)) { return }

    Header
    Write-Host "This preserves the existing Home on Fire accessibility service." -ForegroundColor Yellow

    $current = (& $Adb shell settings get secure enabled_accessibility_services 2>$null).Trim()
    if ([string]::IsNullOrWhiteSpace($current) -or $current -eq "null") {
        $current = ""
    }

    $projectivy = "com.spocky.projengmenu/.services.ProjectivyAccessibilityService"
    $homefire = "io.github.toolicious.homeonfire/.HijackService"

    $services = @()
    if ($current) {
        $services = $current -split ":" | Where-Object { $_ }
    }

    if ($services -notcontains $homefire) { $services += $homefire }
    if ($services -notcontains $projectivy) { $services += $projectivy }

    $newValue = ($services -join ":")
    Write-Host ""
    Write-Host "New accessibility list:" -ForegroundColor DarkGray
    Write-Host $newValue

    if (Confirm-Action "Apply this accessibility configuration?") {
        try {
            Run-Adb @("shell","settings","put","secure","enabled_accessibility_services",$newValue)
            Write-Host "Accessibility services updated." -ForegroundColor Green
        } catch {
            Write-Host $_ -ForegroundColor Red
        }
    }
    Pause-Menu
}

function Enable-ProjectivyNotifications {
    if (-not (Require-Connection)) { return }

    Header
    if (Confirm-Action "Allow Projectivy notification access?") {
        try {
            Run-Adb @("shell","cmd","notification","allow_listener","com.spocky.projengmenu/.services.notification.NotificationListener")
            Write-Host "Notification listener command completed." -ForegroundColor Green
        } catch {
            Write-Host $_ -ForegroundColor Red
            Write-Host "If Fire OS rejects the command, open the system notification-listener settings manually." -ForegroundColor Yellow
            & $Adb shell am start -a android.settings.ACTION_NOTIFICATION_LISTENER_SETTINGS
        }
    }
    Pause-Menu
}

function Apply-Animation {
    if (-not (Require-Connection)) { return }

    Header
    if (Confirm-Action "Set all three animation scales to 0?") {
        try {
            Run-Adb @("shell","settings","put","global","window_animation_scale","0")
            Run-Adb @("shell","settings","put","global","transition_animation_scale","0")
            Run-Adb @("shell","settings","put","global","animator_duration_scale","0")
            Write-Host "Animation scales set to 0." -ForegroundColor Green
        } catch {
            Write-Host $_ -ForegroundColor Red
        }
    }
    Pause-Menu
}

function Apply-Privacy {
    if (-not (Require-Connection)) { return }

    Header
    Write-Host "This applies the privacy/ad settings already tested on the reference TV." -ForegroundColor Yellow

    if (Confirm-Action "Apply privacy and advertising settings?") {
        try {
            Run-Adb @("shell","settings","put","secure","USAGE_METRICS_UPLOAD_ENABLED","0")
            Run-Adb @("shell","settings","put","secure","amz_limit_ad_tracking","1")
            Run-Adb @("shell","settings","put","secure","advertisingIdApp:interestBasedAds:value","0")
            Write-Host "Privacy/ad settings applied." -ForegroundColor Green
        } catch {
            Write-Host $_ -ForegroundColor Red
        }
    }
    Pause-Menu
}

function Disable-VerifiedPackages {
    if (-not (Require-Connection)) { return }

    Header
    Write-Host "18 packages will be disabled:" -ForegroundColor Yellow
    $DisabledPackages | ForEach-Object { Write-Host "  $_" }

    Write-Host ""
    Write-Host "This list does NOT include the Fire TV launcher, AirPlay/HomeKit, connectivity, Alexa, or OTA packages." -ForegroundColor Green

    if (Confirm-Action "Disable all 18 verified packages?") {
        try {
            foreach ($pkg in $DisabledPackages) {
                Write-Host "Disabling $pkg..." -ForegroundColor Cyan
                Run-Adb @("shell","pm","disable-user","--user","0",$pkg)
            }
            Write-Host "Finished." -ForegroundColor Green
        } catch {
            Write-Host $_ -ForegroundColor Red
        }
    }
    Pause-Menu
}

function Apply-OptionalSettings {
    if (-not (Require-Connection)) { return }

    Header
    Write-Host "These settings were recommended during the original setup but were NOT confirmed as completed." -ForegroundColor Yellow
    Write-Host ""
    Write-Host "1. Screen saver activation settings"
    Write-Host "2. Additional usage/marketing settings"
    Write-Host "3. Both"
    Write-Host "0. Cancel"
    $choice = Read-Host "Choose"

    if ($choice -eq "0") { return }

    if (-not (Confirm-Action "These are optional/unconfirmed settings. Continue?")) {
        Pause-Menu
        return
    }

    try {
        if ($choice -in @("1","3")) {
            Run-Adb @("shell","settings","put","secure","screensaver_enabled","0")
            Run-Adb @("shell","settings","put","secure","screensaver_activate_on_sleep","0")
            Run-Adb @("shell","settings","put","secure","screensaver_activate_on_dock","0")
            Write-Host "Screen saver activation settings applied." -ForegroundColor Green
        }

        if ($choice -in @("2","3")) {
            Run-Adb @("shell","settings","put","secure","pact_usage_collection_enabled","0")
            Run-Adb @("shell","settings","put","secure","usage_metrics_marketing_enabled","0")
            Run-Adb @("shell","settings","put","secure","settingsApp:appUsageData:value","0")
            Run-Adb @("shell","settings","put","secure","settingsApp:deviceUsageData:value","0")
            Write-Host "Additional usage/marketing settings applied." -ForegroundColor Green
        }
    } catch {
        Write-Host $_ -ForegroundColor Red
    }

    Pause-Menu
}

function Show-Status {
    if (-not (Require-Connection)) { return }

    Header
    Write-Host "Device:" -ForegroundColor Cyan
    & $Adb shell getprop ro.product.model
    & $Adb shell getprop ro.build.version.release
    & $Adb shell getprop ro.build.version.incremental

    Write-Host ""
    Write-Host "Disabled packages:" -ForegroundColor Cyan
    & $Adb shell pm list packages -d

    Write-Host ""
    Write-Host "Animation scales:" -ForegroundColor Cyan
    Write-Host "window_animation_scale: " -NoNewline
    & $Adb shell settings get global window_animation_scale
    Write-Host "transition_animation_scale: " -NoNewline
    & $Adb shell settings get global transition_animation_scale
    Write-Host "animator_duration_scale: " -NoNewline
    & $Adb shell settings get global animator_duration_scale

    Write-Host ""
    Write-Host "Privacy/ad settings:" -ForegroundColor Cyan
    Write-Host "USAGE_METRICS_UPLOAD_ENABLED: " -NoNewline
    & $Adb shell settings get secure USAGE_METRICS_UPLOAD_ENABLED
    Write-Host "amz_limit_ad_tracking: " -NoNewline
    & $Adb shell settings get secure amz_limit_ad_tracking
    Write-Host "interestBasedAds: " -NoNewline
    & $Adb shell settings get secure "advertisingIdApp:interestBasedAds:value"

    Write-Host ""
    Write-Host "Accessibility:" -ForegroundColor Cyan
    & $Adb shell settings get secure enabled_accessibility_services

    Write-Host ""
    Write-Host "Notification listeners:" -ForegroundColor Cyan
    & $Adb shell settings get secure enabled_notification_listeners

    Pause-Menu
}

function ReEnable-Package {
    if (-not (Require-Connection)) { return }

    Header
    Write-Host "Enter a package to re-enable." -ForegroundColor Cyan
    Write-Host "Examples: com.amazon.gamehub"
    $pkg = Read-Host "Package"

    if ([string]::IsNullOrWhiteSpace($pkg)) {
        Pause-Menu
        return
    }

    if (Confirm-Action "Re-enable $pkg?") {
        try {
            Run-Adb @("shell","pm","enable",$pkg)
            Write-Host "$pkg re-enabled." -ForegroundColor Green
        } catch {
            Write-Host $_ -ForegroundColor Red
        }
    }
    Pause-Menu
}

function Reboot-Device {
    if (-not (Require-Connection)) { return }

    Header
    if (Confirm-Action "Reboot the Fire TV now?") {
        Run-Adb @("reboot")
        $script:Device = $null
        Write-Host "Reboot sent." -ForegroundColor Green
        Start-Sleep -Seconds 2
    }
    Pause-Menu
}

function Recommended-Setup {
    if (-not (Require-Connection)) { return }

    Header
    Write-Host "RECOMMENDED SETUP — preview" -ForegroundColor Cyan
    Write-Host ""
    Write-Host "This will:"
    Write-Host "  • Install Home on Fire"
    Write-Host "  • Install Projectivy"
    Write-Host "  • Grant Home on Fire WRITE_SECURE_SETTINGS"
    Write-Host "  • Preserve Home on Fire accessibility and add Projectivy accessibility"
    Write-Host "  • Apply animation scale = 0"
    Write-Host "  • Apply tested privacy/ad settings"
    Write-Host "  • Disable the 18 verified low-risk packages"
    Write-Host ""
    Write-Host "It will NOT:"
    Write-Host "  • Bypass protected packages"
    Write-Host "  • Disable the Fire TV launcher"
    Write-Host "  • Disable AirPlay/HomeKit components"
    Write-Host "  • Disable OTA"
    Write-Host "  • Factory reset, wipe, unlock, or flash the TV"
    Write-Host ""

    if (-not (Confirm-Action "Run the recommended setup?")) {
        Pause-Menu
        return
    }

    try {
        Run-Adb @("install","-r",$HomeOnFireApk)
        Run-Adb @("install","-r",$ProjectivyApk.FullName)
        Run-Adb @("shell","pm","grant","io.github.toolicious.homeonfire","android.permission.WRITE_SECURE_SETTINGS")

        $current = (& $Adb shell settings get secure enabled_accessibility_services 2>$null).Trim()
        if ([string]::IsNullOrWhiteSpace($current) -or $current -eq "null") { $current = "" }

        $services = @()
        if ($current) { $services = $current -split ":" | Where-Object { $_ } }

        $homefire = "io.github.toolicious.homeonfire/.HijackService"
        $projectivy = "com.spocky.projengmenu/.services.ProjectivyAccessibilityService"

        if ($services -notcontains $homefire) { $services += $homefire }
        if ($services -notcontains $projectivy) { $services += $projectivy }

        Run-Adb @("shell","settings","put","secure","enabled_accessibility_services",($services -join ":"))

        Run-Adb @("shell","settings","put","global","window_animation_scale","0")
        Run-Adb @("shell","settings","put","global","transition_animation_scale","0")
        Run-Adb @("shell","settings","put","global","animator_duration_scale","0")

        Run-Adb @("shell","settings","put","secure","USAGE_METRICS_UPLOAD_ENABLED","0")
        Run-Adb @("shell","settings","put","secure","amz_limit_ad_tracking","1")
        Run-Adb @("shell","settings","put","secure","advertisingIdApp:interestBasedAds:value","0")

        foreach ($pkg in $DisabledPackages) {
            Write-Host "Disabling $pkg..." -ForegroundColor Cyan
            Run-Adb @("shell","pm","disable-user","--user","0",$pkg)
        }

        Write-Host ""
        Write-Host "Recommended setup complete." -ForegroundColor Green
        Write-Host "Notification access is intentionally separate so the user can opt in." -ForegroundColor Yellow
    } catch {
        Write-Host $_ -ForegroundColor Red
    }

    Pause-Menu
}

try {
    Require-Files
} catch {
    Write-Host ""
    Write-Host "SETUP ERROR:" -ForegroundColor Red
    Write-Host $_ -ForegroundColor Red
    Write-Host ""
    Write-Host "Place adb.exe, home-on-fire.apk, and the Projectivy APK next to this script." -ForegroundColor Yellow
    exit 1
}

while ($true) {
    Header
    Write-Host "1. Connect to Fire TV"
    Write-Host "2. Check device / ADB status"
    Write-Host "3. Install Home on Fire / Projectivy"
    Write-Host "4. Grant Home on Fire WRITE_SECURE_SETTINGS"
    Write-Host "5. Enable Projectivy accessibility (preserves Home on Fire)"
    Write-Host "6. Enable Projectivy notification access"
    Write-Host "7. Apply animation speed settings"
    Write-Host "8. Apply privacy / advertising settings"
    Write-Host "9. Disable the 18 verified packages"
    Write-Host "10. Optional / unconfirmed settings"
    Write-Host "11. Show current customization status"
    Write-Host "12. Re-enable a package"
    Write-Host "13. Recommended setup (preview + confirmation)"
    Write-Host "14. Reboot Fire TV"
    Write-Host "0. Exit"
    Write-Host ""

    $choice = Read-Host "What do you want to run?"

    try {
        switch ($choice) {
            "1"  { Connect-Device }
            "2"  { if (Require-Connection) { Show-Status } }
            "3"  { Install-Apps }
            "4"  { Grant-HomeOnFire }
            "5"  { Enable-ProjectivyAccessibility }
            "6"  { Enable-ProjectivyNotifications }
            "7"  { Apply-Animation }
            "8"  { Apply-Privacy }
            "9"  { Disable-VerifiedPackages }
            "10" { Apply-OptionalSettings }
            "11" { Show-Status }
            "12" { ReEnable-Package }
            "13" { Recommended-Setup }
            "14" { Reboot-Device }
            "0"  { exit 0 }
            default {
                Write-Host "Invalid choice." -ForegroundColor Yellow
                Start-Sleep -Seconds 1
            }
        }
    } catch {
        Write-Host ""
        Write-Host "ERROR: $($_.Exception.Message)" -ForegroundColor Red
        Pause-Menu
    }
}
