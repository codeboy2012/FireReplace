namespace FireReplace.Tests.Fakes;

/// <summary>
/// Recorded ADB output used across the tests. The device text mirrors what the reference Insignia
/// Fire TV (AFTALMO / almond / Fire OS 7.01065.5585) actually returns, so the parsers are exercised
/// against realistic input rather than idealised samples.
/// </summary>
public static class MockAdbResponses
{
    /// <summary>A normal <c>adb devices</c> listing with one authorized network device.</summary>
    public const string DevicesConnected = """
        List of devices attached
        192.168.1.147:5555	device

        """;

    /// <summary>An <c>adb devices</c> listing where the TV has not accepted the ADB prompt.</summary>
    public const string DevicesUnauthorized = """
        List of devices attached
        192.168.1.147:5555	unauthorized

        """;

    /// <summary>An <c>adb devices</c> listing containing daemon start-up chatter.</summary>
    public const string DevicesWithDaemonNoise = """
        * daemon not running; starting now at tcp:5037
        * daemon started successfully
        List of devices attached
        192.168.1.147:5555	device
        1A2B3C4D	offline
        """;

    /// <summary>A trimmed but realistic <c>getprop</c> dump.</summary>
    public const string GetPropDump = """
        [ro.build.product]: [almond]
        [ro.build.version.incremental]: [0035736899972]
        [ro.build.version.name]: [Fire OS 7.01065.5585]
        [ro.build.version.release]: [9]
        [ro.build.version.sdk]: [28]
        [ro.product.cpu.abi]: [armeabi-v7a]
        [ro.product.manufacturer]: [Amazon]
        [ro.product.model]: [AFTALMO]
        [ro.product.name]: [almond]
        [ro.product.vendor.model]: [Insignia Fire TV]
        [persist.sys.timezone]: [America/New_York]
        [malformed line without brackets]
        """;

    /// <summary>Output of <c>pm list packages</c>.</summary>
    public const string PackagesAll = """
        package:com.amazon.tv.launcher
        package:com.amazon.gamehub
        package:com.amazon.bueller.photos
        package:com.android.nfc
        package:io.github.toolicious.homeonfire
        package:com.spocky.projengmenu
        package:com.amazon.client.metrics
        """;

    /// <summary>Output of <c>pm list packages -d</c>.</summary>
    public const string PackagesDisabled = """
        package:com.amazon.bueller.photos
        package:com.amazon.tv.releasenotes
        """;

    /// <summary>Output of <c>pm list packages -f</c>, which appends the APK path.</summary>
    public const string PackagesWithPaths = """
        package:/system/priv-app/Launcher/Launcher.apk=com.amazon.tv.launcher
        package:/product/app/GameHub/GameHub.apk=com.amazon.gamehub
        """;

    /// <summary>A successful install.</summary>
    public const string InstallSuccess = """
        Performing Streamed Install
        Success
        """;

    /// <summary>An install rejected because the APK has no matching ABI.</summary>
    public const string InstallAbiFailure =
        "adb: failed to install Projectivy.apk: Failure [INSTALL_FAILED_NO_MATCHING_ABIS: Failed to extract native libraries]";

    /// <summary>The Fire OS refusal seen when touching a protected package.</summary>
    public const string SecurityException =
        "java.lang.SecurityException: Shell cannot change component state for com.amazon.client.metrics/null to 2";

    /// <summary>A failed network connection attempt. Note that adb still exits 0 here.</summary>
    public const string ConnectFailed =
        "failed to connect to '192.168.1.200:5555': Connection refused";

    /// <summary>A successful connection.</summary>
    public const string ConnectSucceeded = "connected to 192.168.1.147:5555";

    /// <summary>The <c>pm disable-user</c> confirmation line.</summary>
    public const string DisableUserSuccess =
        "Package com.amazon.gamehub new state: disabled-user";

    /// <summary>The <c>pm enable</c> confirmation line.</summary>
    public const string EnableSuccess =
        "Package com.amazon.gamehub new state: enabled";

    /// <summary>An accessibility list that already contains the Home on Fire hijack service.</summary>
    public const string AccessibilityWithHomeOnFire =
        "io.github.toolicious.homeonfire/.HijackService";

    /// <summary>An accessibility list holding two services, one unrelated to FireReplace.</summary>
    public const string AccessibilityWithThirdParty =
        "com.example.reader/.ReaderService:io.github.toolicious.homeonfire/.HijackService";
}
