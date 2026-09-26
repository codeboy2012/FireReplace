using FireReplace.Core.Adb;
using FireReplace.Core.Catalog;
using FireReplace.Core.Models;
using FireReplace.Core.Services;
using FireReplace.Tests.Fakes;
using Xunit;

namespace FireReplace.Tests;

public class DeviceStateTests
{
    private static string FakeAdbPath()
    {
        var path = Path.Combine(Path.GetTempPath(), "firereplace-tests", "adb.exe");
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        if (!File.Exists(path))
        {
            File.WriteAllText(path, "not a real adb");
        }

        return path;
    }

    private static string BatchOutputFor(params string[] sections) =>
        string.Join($"\n{AdbArguments.BatchSeparator}\n", sections);

    [Fact]
    public async Task A_full_refresh_costs_three_adb_invocations()
    {
        var settingsSections = DeviceStateService.ReadableSettings.Select(_ => "0").ToArray();

        var runner = new FakeProcessRunner()
            .WhenContains("shell getprop", MockAdbResponses.GetPropDump)
            .WhenContains("settings get", BatchOutputFor(settingsSections))
            .WhenContains("pm list packages", BatchOutputFor(MockAdbResponses.PackagesAll, MockAdbResponses.PackagesDisabled));

        var log = new RecordingLog();
        var adb = new AdbService(runner, log, FakeAdbPath()) { Serial = "192.168.1.147:5555" };
        var service = new DeviceStateService(adb, log);

        var state = await service.RefreshAsync();

        Assert.Equal(3, runner.Invocations.Count);
        Assert.NotNull(state.Device);
        Assert.Equal("AFTALMO", state.Device!.Model);
        Assert.True(state.IsPopulated);
        Assert.Same(state, service.Current);
    }

    [Fact]
    public async Task Package_state_combines_the_full_and_disabled_listings()
    {
        var settingsSections = DeviceStateService.ReadableSettings.Select(_ => "null").ToArray();

        var runner = new FakeProcessRunner()
            .WhenContains("shell getprop", MockAdbResponses.GetPropDump)
            .WhenContains("settings get", BatchOutputFor(settingsSections))
            .WhenContains("pm list packages", BatchOutputFor(MockAdbResponses.PackagesAll, MockAdbResponses.PackagesDisabled));

        var log = new RecordingLog();
        var adb = new AdbService(runner, log, FakeAdbPath());
        var state = await new DeviceStateService(adb, log).RefreshAsync();

        Assert.Equal(PackageState.Disabled, state.StateOf("com.amazon.bueller.photos"));
        Assert.Equal(PackageState.Enabled, state.StateOf("com.amazon.gamehub"));
        Assert.Equal(PackageState.NotInstalled, state.StateOf("com.amazon.sneakpeek"));

        // A package that only appears in the disabled listing must still count as installed.
        Assert.Contains("com.amazon.tv.releasenotes", state.InstalledPackages);
        Assert.Equal(PackageState.Disabled, state.StateOf("com.amazon.tv.releasenotes"));
    }

    [Fact]
    public async Task Settings_values_are_mapped_back_to_their_scoped_keys()
    {
        var definitions = DeviceStateService.ReadableSettings;
        var sections = definitions
            .Select(d => d.Key == "window_animation_scale" ? "0" : d.Key == "amz_limit_ad_tracking" ? "1" : "null")
            .ToArray();

        var runner = new FakeProcessRunner()
            .WhenContains("shell getprop", MockAdbResponses.GetPropDump)
            .WhenContains("settings get", BatchOutputFor(sections))
            .WhenContains("pm list packages", BatchOutputFor(MockAdbResponses.PackagesAll, string.Empty));

        var log = new RecordingLog();
        var adb = new AdbService(runner, log, FakeAdbPath());
        var state = await new DeviceStateService(adb, log).RefreshAsync();

        Assert.Equal("0", state.Settings["global/window_animation_scale"]);
        Assert.Equal("1", state.Settings["secure/amz_limit_ad_tracking"]);
        Assert.Null(state.Settings["secure/screensaver_enabled"]);
    }

    [Fact]
    public async Task The_accessibility_and_listener_lists_are_parsed_from_the_batch()
    {
        var definitions = DeviceStateService.ReadableSettings;
        var sections = definitions
            .Select(d => d.Key switch
            {
                "enabled_accessibility_services" => MockAdbResponses.AccessibilityWithThirdParty,
                "enabled_notification_listeners" => "com.spocky.projengmenu/.services.notification.NotificationListener",
                _ => "null",
            })
            .ToArray();

        var runner = new FakeProcessRunner()
            .WhenContains("shell getprop", MockAdbResponses.GetPropDump)
            .WhenContains("settings get", BatchOutputFor(sections))
            .WhenContains("pm list packages", BatchOutputFor(string.Empty, string.Empty));

        var log = new RecordingLog();
        var adb = new AdbService(runner, log, FakeAdbPath());
        var state = await new DeviceStateService(adb, log).RefreshAsync();

        Assert.Equal(2, state.AccessibilityServices.Count);
        Assert.True(state.HasAccessibilityService("io.github.toolicious.homeonfire/.HijackService"));
        Assert.True(state.HasNotificationListener("com.spocky.projengmenu/.services.notification.NotificationListener"));
        Assert.False(state.HasNotificationListener("com.example/.Other"));
    }

    [Fact]
    public void IsApplied_compares_against_the_value_the_catalog_writes()
    {
        var state = new DeviceState
        {
            Settings = new Dictionary<string, string?>(StringComparer.Ordinal)
            {
                ["global/window_animation_scale"] = "0",
                ["global/transition_animation_scale"] = "0",
                ["global/animator_duration_scale"] = "1",
            },
        };

        Assert.True(state.IsApplied(SettingsCatalog.Animations.Settings[0]));
        Assert.False(state.IsApplied(SettingsCatalog.Animations.Settings[2]));
        Assert.False(state.IsApplied(SettingsCatalog.Animations));
    }

    [Fact]
    public void IsApplied_for_a_group_requires_every_setting()
    {
        var state = new DeviceState
        {
            Settings = SettingsCatalog.Animations.Settings.ToDictionary(
                DeviceState.KeyOf,
                s => (string?)s.AppliedValue,
                StringComparer.Ordinal),
        };

        Assert.True(state.IsApplied(SettingsCatalog.Animations));
    }

    [Fact]
    public void PackageInfoFor_attaches_the_catalog_metadata()
    {
        var state = new DeviceState
        {
            InstalledPackages = new HashSet<string>(["com.amazon.gamehub", "com.amazon.tv.launcher"], StringComparer.Ordinal),
        };

        var gamehub = state.PackageInfoFor("com.amazon.gamehub");
        Assert.Equal(PackageState.Enabled, gamehub.State);
        Assert.Equal(PackageClassification.VerifiedDebloat, gamehub.Classification);
        Assert.NotNull(gamehub.Description);
        Assert.False(gamehub.IsGuarded);

        var launcher = state.PackageInfoFor("com.amazon.tv.launcher");
        Assert.True(launcher.IsGuarded);
        Assert.Equal("CORE — PROTECTED", launcher.GuardLabel);
    }

    [Fact]
    public void An_empty_snapshot_reports_unknown_rather_than_not_installed()
    {
        var state = new DeviceState();
        Assert.Equal(PackageState.Unknown, state.StateOf("com.amazon.gamehub"));
    }

    [Fact]
    public async Task A_package_only_refresh_keeps_the_cached_device_information()
    {
        var settingsSections = DeviceStateService.ReadableSettings.Select(_ => "null").ToArray();

        var runner = new FakeProcessRunner()
            .WhenContains("shell getprop", MockAdbResponses.GetPropDump)
            .WhenContains("settings get", BatchOutputFor(settingsSections))
            .WhenContains("pm list packages", BatchOutputFor(MockAdbResponses.PackagesAll, MockAdbResponses.PackagesDisabled));

        var log = new RecordingLog();
        var adb = new AdbService(runner, log, FakeAdbPath());
        var service = new DeviceStateService(adb, log);

        await service.RefreshAsync();
        var before = runner.Invocations.Count;

        var state = await service.RefreshPackagesAsync();

        Assert.Equal(before + 1, runner.Invocations.Count);
        Assert.NotNull(state.Device);
        Assert.Equal("AFTALMO", state.Device!.Model);
    }

    [Fact]
    public void Reset_drops_the_cached_snapshot()
    {
        var log = new RecordingLog();
        var service = new DeviceStateService(new AdbService(new FakeProcessRunner(), log, FakeAdbPath()), log);

        service.Reset();

        Assert.Null(service.Current);
    }
}
