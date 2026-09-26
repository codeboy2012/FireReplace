using FireReplace.Core.Adb;
using FireReplace.Core.Catalog;
using Xunit;

namespace FireReplace.Tests;

public class AdbArgumentTests
{
    private const string Serial = "192.168.1.147:5555";

    [Fact]
    public void Connect_builds_the_endpoint_argument()
    {
        var args = AdbArguments.Connect("192.168.1.147", 5555);
        Assert.Equal(["connect", "192.168.1.147:5555"], args);
    }

    [Fact]
    public void Connect_brackets_ipv6_literals()
    {
        var args = AdbArguments.Connect("fe80::1", 5555);
        Assert.Equal(["connect", "[fe80::1]:5555"], args);
    }

    [Fact]
    public void Connect_rejects_an_invalid_address() =>
        Assert.Throws<ArgumentException>(() => AdbArguments.Connect("192.168.1.147; reboot", 5555));

    [Fact]
    public void Connect_rejects_an_out_of_range_port() =>
        Assert.Throws<ArgumentOutOfRangeException>(() => AdbArguments.Connect("192.168.1.147", 0));

    [Fact]
    public void WithTarget_prefixes_the_serial()
    {
        var args = AdbArguments.WithTarget(Serial, "shell", "getprop");
        Assert.Equal(["-s", Serial, "shell", "getprop"], args);
    }

    [Fact]
    public void WithTarget_omits_the_prefix_when_no_serial_is_known()
    {
        var args = AdbArguments.WithTarget(null, "devices");
        Assert.Equal(["devices"], args);
    }

    [Fact]
    public void WithTarget_rejects_a_malformed_serial() =>
        Assert.Throws<ArgumentException>(() => AdbArguments.WithTarget("bad serial;rm", "shell"));

    [Theory]
    [InlineData("192.168.1.147:5555", true)]
    [InlineData("1A2B3C4D", true)]
    [InlineData("emulator-5554", true)]
    [InlineData("192.168.1.147:99999", false)]
    [InlineData("192.168.1:5555", false)]
    [InlineData("serial with space", false)]
    [InlineData("serial;rm", false)]
    [InlineData("", false)]
    public void IsSerial_validates_the_serial_shape(string serial, bool expected) =>
        Assert.Equal(expected, AdbArguments.IsSerial(serial));

    [Fact]
    public void DisablePackage_targets_user_zero()
    {
        var args = AdbArguments.DisablePackage(Serial, "com.amazon.gamehub");
        Assert.Equal(
            ["-s", Serial, "shell", "pm", "disable-user", "--user", "0", "com.amazon.gamehub"],
            args);
    }

    [Fact]
    public void DisablePackage_rejects_an_injected_package_name() =>
        Assert.Throws<ArgumentException>(() =>
            AdbArguments.DisablePackage(Serial, "com.amazon.gamehub; pm uninstall com.amazon.tv.launcher"));

    [Fact]
    public void EnablePackage_builds_pm_enable()
    {
        var args = AdbArguments.EnablePackage(Serial, "com.amazon.gamehub");
        Assert.Equal(["-s", Serial, "shell", "pm", "enable", "com.amazon.gamehub"], args);
    }

    [Fact]
    public void GetSetting_uses_the_scope_token()
    {
        Assert.Equal(
            ["-s", Serial, "shell", "settings", "get", "global", "window_animation_scale"],
            AdbArguments.GetSetting(Serial, SettingScope.Global, "window_animation_scale"));

        Assert.Equal(
            ["-s", Serial, "shell", "settings", "get", "secure", "amz_limit_ad_tracking"],
            AdbArguments.GetSetting(Serial, SettingScope.Secure, "amz_limit_ad_tracking"));
    }

    [Fact]
    public void PutSetting_keeps_a_colon_bearing_key_as_one_token()
    {
        var args = AdbArguments.PutSetting(Serial, SettingScope.Secure, "advertisingIdApp:interestBasedAds:value", "0");
        Assert.Equal(
            ["-s", Serial, "shell", "settings", "put", "secure", "advertisingIdApp:interestBasedAds:value", "0"],
            args);
    }

    [Fact]
    public void PutSetting_keeps_a_component_list_value_as_one_token()
    {
        const string value = "io.github.toolicious.homeonfire/.HijackService:com.spocky.projengmenu/.services.ProjectivyAccessibilityService";
        var args = AdbArguments.PutSetting(Serial, SettingScope.Secure, "enabled_accessibility_services", value);

        Assert.Equal(value, args[^1]);
        Assert.Equal(8, args.Count);
    }

    [Fact]
    public void PutSetting_rejects_control_characters_in_the_value() =>
        Assert.Throws<ArgumentException>(() =>
            AdbArguments.PutSetting(Serial, SettingScope.Secure, "key", "value\nsecond line"));

    [Fact]
    public void PutSetting_rejects_an_invalid_key() =>
        Assert.Throws<ArgumentException>(() =>
            AdbArguments.PutSetting(Serial, SettingScope.Secure, "key; reboot", "0"));

    [Fact]
    public void GrantPermission_builds_pm_grant()
    {
        var args = AdbArguments.GrantPermission(
            Serial,
            "io.github.toolicious.homeonfire",
            "android.permission.WRITE_SECURE_SETTINGS");

        Assert.Equal(
            ["-s", Serial, "shell", "pm", "grant", "io.github.toolicious.homeonfire", "android.permission.WRITE_SECURE_SETTINGS"],
            args);
    }

    [Fact]
    public void GrantPermission_rejects_a_permission_without_a_namespace() =>
        Assert.Throws<ArgumentException>(() =>
            AdbArguments.GrantPermission(Serial, "com.example", "WRITE_SECURE_SETTINGS"));

    [Fact]
    public void AllowNotificationListener_builds_the_cmd_notification_call()
    {
        var args = AdbArguments.AllowNotificationListener(
            Serial,
            "com.spocky.projengmenu/.services.notification.NotificationListener");

        Assert.Equal(
            ["-s", Serial, "shell", "cmd", "notification", "allow_listener", "com.spocky.projengmenu/.services.notification.NotificationListener"],
            args);
    }

    [Fact]
    public void StartSettingsActivity_accepts_only_allow_listed_actions()
    {
        var args = AdbArguments.StartSettingsActivity(Serial, "android.settings.ACTION_NOTIFICATION_LISTENER_SETTINGS");
        Assert.Equal(
            ["-s", Serial, "shell", "am", "start", "-a", "android.settings.ACTION_NOTIFICATION_LISTENER_SETTINGS"],
            args);

        Assert.Throws<ArgumentException>(() =>
            AdbArguments.StartSettingsActivity(Serial, "android.intent.action.MAIN"));
    }

    [Fact]
    public void Install_rejects_a_path_that_is_not_an_existing_apk() =>
        Assert.Throws<ArgumentException>(() =>
            AdbArguments.Install(Serial, Path.Combine(Path.GetTempPath(), "missing.apk"), false));

    [Fact]
    public void Install_uses_reinstall_and_an_absolute_path()
    {
        var path = Path.Combine(Path.GetTempPath(), $"firereplace-{Guid.NewGuid():N}.apk");
        File.WriteAllText(path, "pretend apk");

        try
        {
            var args = AdbArguments.Install(Serial, path, grantRuntimePermissions: false);
            Assert.Equal(["-s", Serial, "install", "-r", Path.GetFullPath(path)], args);

            var granting = AdbArguments.Install(Serial, path, grantRuntimePermissions: true);
            Assert.Contains("-g", granting);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void ListPackages_maps_each_filter()
    {
        Assert.DoesNotContain("-d", AdbArguments.ListPackages(Serial, PackageListFilter.All));
        Assert.Contains("-d", AdbArguments.ListPackages(Serial, PackageListFilter.Disabled));
        Assert.Contains("-e", AdbArguments.ListPackages(Serial, PackageListFilter.Enabled));
        Assert.Contains("-s", AdbArguments.ListPackages(Serial, PackageListFilter.System));
    }

    [Fact]
    public void ShellBatch_joins_commands_with_an_echoed_separator()
    {
        var args = AdbArguments.ShellBatch(Serial, [
            ["settings", "get", "global", "window_animation_scale"],
            ["pm", "list", "packages", "-d"],
        ]);

        Assert.Equal("-s", args[0]);
        Assert.Equal(Serial, args[1]);
        Assert.Equal("shell", args[2]);
        Assert.Equal(
            $"settings get global window_animation_scale; echo {AdbArguments.BatchSeparator}; pm list packages -d",
            args[3]);
    }

    [Fact]
    public void ShellBatch_refuses_a_token_that_could_inject_a_command() =>
        Assert.Throws<ArgumentException>(() => AdbArguments.ShellBatch(Serial, [
            ["settings", "get", "secure", "key; reboot"],
        ]));

    [Fact]
    public void ShellBatch_refuses_an_empty_command_list() =>
        Assert.Throws<ArgumentException>(() => AdbArguments.ShellBatch(Serial, []));

    [Fact]
    public void Format_renders_a_readable_command_line()
    {
        var text = AdbCommandResult.Format(AdbArguments.DisablePackage(Serial, "com.amazon.gamehub"));
        Assert.Equal($"adb -s {Serial} shell pm disable-user --user 0 com.amazon.gamehub", text);
    }

    [Fact]
    public void Format_quotes_arguments_containing_spaces()
    {
        var text = AdbCommandResult.Format(["install", "-r", @"C:\path with space\app.apk"]);
        Assert.Contains("\"C:\\path with space\\app.apk\"", text);
    }
}
