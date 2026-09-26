using FireReplace.Core.Actions;
using FireReplace.Core.Adb;
using FireReplace.Core.Catalog;
using FireReplace.Core.Logging;
using FireReplace.Core.Models;
using FireReplace.Core.Services;
using FireReplace.Tests.Fakes;
using Xunit;

namespace FireReplace.Tests;

public class ActionEngineTests
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

    private static (ActionEngine Engine, AdbService Adb, FakeProcessRunner Runner, RecordingLog Log, ScriptedConfirmationHost Host)
        Build(bool approve = true, Action<FakeProcessRunner>? configure = null)
    {
        var runner = new FakeProcessRunner();
        configure?.Invoke(runner);

        var log = new RecordingLog();
        var adb = new AdbService(runner, log, FakeAdbPath()) { Serial = "192.168.1.147:5555" };
        var host = new ScriptedConfirmationHost(approve);
        return (new ActionEngine(adb, log, host), adb, runner, log, host);
    }

    private static DeviceState StateWith(params string[] enabledPackages) => new()
    {
        Device = new DeviceInfo { Serial = "192.168.1.147:5555", Model = "AFTALMO" },
        InstalledPackages = new HashSet<string>(enabledPackages, StringComparer.Ordinal),
        DisabledPackages = new HashSet<string>(StringComparer.Ordinal),
    };

    [Fact]
    public async Task A_plan_is_confirmed_before_anything_runs()
    {
        var (engine, adb, runner, _, host) = Build(
            approve: true,
            r => r.WhenContains("disable-user", MockAdbResponses.DisableUserSuccess));

        var packages = new PackageActionService(adb);
        var plan = packages.BuildDisablePlan(["com.amazon.gamehub"], StateWith("com.amazon.gamehub"), out _);

        var report = await engine.RunAsync(plan);

        Assert.Single(host.Requests);
        Assert.NotNull(report);
        Assert.True(report!.AllSucceeded);
        Assert.Single(runner.Invocations);
    }

    [Fact]
    public async Task Declining_the_confirmation_leaves_the_device_untouched()
    {
        var (engine, adb, runner, log, host) = Build(approve: false);

        var packages = new PackageActionService(adb);
        var plan = packages.BuildDisablePlan(["com.amazon.gamehub"], StateWith("com.amazon.gamehub"), out _);

        var report = await engine.RunAsync(plan);

        Assert.Null(report);
        Assert.Empty(runner.Invocations);
        Assert.Single(host.Requests);
        Assert.True(log.Contains(LogLevel.Info, "cancelled before any change"));
    }

    [Fact]
    public async Task A_plan_whose_changes_are_already_satisfied_is_skipped()
    {
        var (engine, adb, runner, _, host) = Build();

        var state = new DeviceState
        {
            InstalledPackages = new HashSet<string>(["com.amazon.gamehub"], StringComparer.Ordinal),
            DisabledPackages = new HashSet<string>(["com.amazon.gamehub"], StringComparer.Ordinal),
        };

        var plan = new PackageActionService(adb).BuildDisablePlan(["com.amazon.gamehub"], state, out _);

        Assert.True(plan.IsEmpty);
        Assert.Null(await engine.RunAsync(plan));
        Assert.Empty(host.Requests);
        Assert.Empty(runner.Invocations);
    }

    [Fact]
    public async Task A_plan_that_targets_a_guarded_package_is_refused_outright()
    {
        var (engine, adb, runner, log, _) = Build();

        // Building a guarded change is itself blocked, so the plan has to be assembled by hand to
        // prove the engine is a second line of defence rather than the only one.
        var plan = new ActionPlan(
            "Hand built plan",
            "Attempts to disable a core package.",
            [
                new PlannedChange(
                    ChangeKind.PackageDisable,
                    "Disable package",
                    "com.amazon.tv.launcher",
                    "Enabled",
                    "Disabled",
                    _ => Task.FromResult(AdbCommandResult.DryRun(["noop"]))),
            ]);

        await Assert.ThrowsAsync<ProtectedPackageException>(() => engine.RunAsync(plan));
        Assert.Empty(runner.Invocations);
        Assert.True(log.Contains(LogLevel.Warning, "Refused to disable protected package"));
    }

    [Fact]
    public void Building_a_disable_change_for_a_guarded_package_throws()
    {
        var adb = new AdbService(new FakeProcessRunner(), new RecordingLog(), FakeAdbPath());
        var packages = new PackageActionService(adb);

        Assert.Throws<ProtectedPackageException>(() =>
            packages.BuildDisableChange("com.amazon.tv.launcher", StateWith("com.amazon.tv.launcher")));
    }

    [Fact]
    public void A_bulk_disable_plan_silently_drops_nothing_and_reports_what_it_refused()
    {
        var adb = new AdbService(new FakeProcessRunner(), new RecordingLog(), FakeAdbPath());
        var packages = new PackageActionService(adb);

        var state = StateWith("com.amazon.gamehub", "com.amazon.tv.launcher", "com.amazon.client.metrics");
        var plan = packages.BuildDisablePlan(
            ["com.amazon.gamehub", "com.amazon.tv.launcher", "com.amazon.client.metrics"],
            state,
            out var refused);

        Assert.Equal(["com.amazon.tv.launcher", "com.amazon.client.metrics"], refused);
        Assert.Single(plan.Changes);
        Assert.Equal("com.amazon.gamehub", plan.Changes[0].Target);
        Assert.NotNull(plan.Warning);
    }

    [Fact]
    public void Packages_that_are_not_installed_are_left_out_of_the_plan()
    {
        var adb = new AdbService(new FakeProcessRunner(), new RecordingLog(), FakeAdbPath());
        var packages = new PackageActionService(adb);

        var plan = packages.BuildDisablePlan(
            PackageCatalog.VerifiedDebloatNames,
            StateWith("com.amazon.gamehub", "com.android.nfc"),
            out _);

        Assert.Equal(2, plan.Changes.Count);
    }

    [Fact]
    public async Task A_security_exception_is_reported_as_a_protected_package_not_a_raw_error()
    {
        var (engine, adb, _, _, _) = Build(
            approve: true,
            r => r.WhenContains("disable-user", MockAdbResponses.SecurityException, string.Empty, 0));

        var plan = new PackageActionService(adb)
            .BuildDisablePlan(["com.amazon.gamehub"], StateWith("com.amazon.gamehub"), out _);

        var report = await engine.RunAsync(plan);

        Assert.NotNull(report);
        Assert.Equal(1, report!.FailedCount);
        var failure = Assert.Single(report.ProtectedFailures);
        Assert.True(failure.Failure!.IsProtectedByFireOs);
        Assert.Equal("Fire OS protected this package.", failure.Failure.Headline);
        Assert.Contains("will not attempt to bypass", string.Join(' ', failure.Failure.Causes));
    }

    [Fact]
    public async Task An_install_that_prints_failure_while_exiting_zero_is_treated_as_a_failure()
    {
        var apk = Path.Combine(Path.GetTempPath(), $"firereplace-{Guid.NewGuid():N}.apk");
        File.WriteAllText(apk, "pretend apk");

        try
        {
            var (engine, adb, _, _, _) = Build(
                approve: true,
                r => r.WhenContains("install", MockAdbResponses.InstallAbiFailure, string.Empty, 0));

            var launchers = new LauncherActionService(adb);
            var apkLocation = new Core.Discovery.ApkLocation("Projectivy Launcher", "Projectivy*.apk", "com.spocky.projengmenu", apk);
            var plan = launchers.BuildInstallPlan(LauncherCatalog.Projectivy, apkLocation, StateWith());

            var report = await engine.RunAsync(plan);

            Assert.NotNull(report);
            Assert.Equal(1, report!.FailedCount);
            Assert.Contains("NO_MATCHING_ABIS", report.Outcomes[0].Failure!.Headline);
        }
        finally
        {
            File.Delete(apk);
        }
    }

    [Fact]
    public async Task Dry_run_reports_every_change_as_skipped()
    {
        var (engine, adb, runner, _, _) = Build();
        adb.DryRun = true;

        var plan = new PackageActionService(adb)
            .BuildDisablePlan(["com.amazon.gamehub"], StateWith("com.amazon.gamehub"), out _);

        var report = await engine.RunAsync(plan);

        Assert.NotNull(report);
        Assert.True(report!.WasDryRun);
        Assert.True(report.AllSucceeded);
        Assert.All(report.Outcomes, o => Assert.True(o.WasDryRun));
        Assert.Empty(runner.Invocations);
        Assert.Contains("No changes were made", report.Summary);
    }

    [Fact]
    public async Task A_partial_failure_reports_both_counts()
    {
        var runner = new FakeProcessRunner()
            .WhenContains("com.amazon.gamehub", MockAdbResponses.DisableUserSuccess)
            .WhenContains("com.android.nfc", string.Empty, "Error: java.lang.IllegalArgumentException", 1);

        var log = new RecordingLog();
        var adb = new AdbService(runner, log, FakeAdbPath()) { Serial = "192.168.1.147:5555" };
        var engine = new ActionEngine(adb, log, new ScriptedConfirmationHost(true));

        var plan = new PackageActionService(adb).BuildDisablePlan(
            ["com.amazon.gamehub", "com.android.nfc"],
            StateWith("com.amazon.gamehub", "com.android.nfc"),
            out _);

        var report = await engine.RunAsync(plan);

        Assert.NotNull(report);
        Assert.Equal(1, report!.SucceededCount);
        Assert.Equal(1, report.FailedCount);
        Assert.Equal("1 applied, 1 failed.", report.Summary);
    }

    [Fact]
    public async Task Every_change_carries_a_command_preview_for_the_confirmation_panel()
    {
        var (engine, adb, _, _, host) = Build(
            approve: false);

        var plan = new SettingsActionService(adb).BuildApplyPlan(
            SettingsCatalog.Animations,
            new DeviceState());

        await engine.RunAsync(plan);

        var shown = Assert.Single(host.Requests);
        Assert.All(shown.Changes, change =>
        {
            Assert.False(string.IsNullOrWhiteSpace(change.CommandPreview));
            Assert.StartsWith("adb -s 192.168.1.147:5555 shell settings put global", change.CommandPreview);
        });
    }

    [Fact]
    public async Task The_accessibility_plan_writes_the_merged_list_and_nothing_else()
    {
        var (engine, adb, runner, _, _) = Build(
            approve: true,
            r => r.WhenContains("settings put", string.Empty));

        var state = new DeviceState
        {
            AccessibilityServices = ["io.github.toolicious.homeonfire/.HijackService"],
        };

        var plan = new LauncherActionService(adb)
            .BuildAccessibilityPlan(state, LauncherCatalog.KnownAccessibilityServices);

        var report = await engine.RunAsync(plan);

        Assert.NotNull(report);
        Assert.True(report!.AllSucceeded);

        var accessibilityCall = Assert.Single(
            runner.ArgumentLists,
            a => a.Contains("enabled_accessibility_services"));

        Assert.Equal(
            "io.github.toolicious.homeonfire/.HijackService:com.spocky.projengmenu/.services.ProjectivyAccessibilityService",
            accessibilityCall[^1]);
    }

    [Fact]
    public void The_recommended_setup_excludes_notification_access_and_experimental_settings()
    {
        var adb = new AdbService(new FakeProcessRunner(), new RecordingLog(), FakeAdbPath());
        var setup = new RecommendedSetupService(
            new LauncherActionService(adb),
            new SettingsActionService(adb),
            new PackageActionService(adb));

        var state = StateWith("com.amazon.gamehub", "com.android.nfc");
        var plan = setup.BuildPlan(state, []);

        Assert.DoesNotContain(plan.Changes, c => c.Kind == ChangeKind.NotificationListener);

        var experimentalKeys = SettingsCatalog.ExperimentalGroups
            .SelectMany(g => g.Settings)
            .Select(s => s.Key)
            .ToHashSet(StringComparer.Ordinal);

        Assert.DoesNotContain(plan.Changes, c => experimentalKeys.Any(k => c.Target.EndsWith(k, StringComparison.Ordinal)));
        Assert.DoesNotContain(plan.Changes, c => PackageCatalog.IsGuarded(c.Target));
        Assert.True(plan.IsDestructive);
    }

    [Fact]
    public void The_notification_plan_warns_before_enabling_a_sensitive_permission()
    {
        var adb = new AdbService(new FakeProcessRunner(), new RecordingLog(), FakeAdbPath());
        var plan = new LauncherActionService(adb).BuildNotificationPlan(LauncherCatalog.Projectivy, new DeviceState());

        Assert.NotNull(plan.Warning);
        Assert.Contains("sensitive", plan.Warning!, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("read", plan.Changes[0].Detail!, StringComparison.OrdinalIgnoreCase);
    }
}
