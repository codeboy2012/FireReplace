using FireReplace.Core.Adb;
using FireReplace.Core.Catalog;
using FireReplace.Core.Logging;
using FireReplace.Core.Models;
using FireReplace.Tests.Fakes;
using Xunit;

namespace FireReplace.Tests;

public class AdbServiceTests
{
    private static (AdbService Adb, FakeProcessRunner Runner, RecordingLog Log) Build(
        Action<FakeProcessRunner>? configure = null)
    {
        var runner = new FakeProcessRunner();
        configure?.Invoke(runner);

        var log = new RecordingLog();
        var adbPath = FakeAdbPath();
        var adb = new AdbService(runner, log, adbPath);
        return (adb, runner, log);
    }

    /// <summary>
    /// The runner is faked, but AdbService refuses to run without a path, so a throwaway file stands in.
    /// </summary>
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

    [Fact]
    public async Task ConnectAsync_records_the_serial_on_success()
    {
        var (adb, runner, _) = Build(r => r.WhenContains("connect", MockAdbResponses.ConnectSucceeded));

        var result = await adb.ConnectAsync("192.168.1.147");

        Assert.True(result.Success);
        Assert.Equal("192.168.1.147:5555", adb.Serial);
        Assert.Equal("connect 192.168.1.147:5555", runner.Invocations[0]);
    }

    [Fact]
    public async Task ConnectAsync_does_not_record_a_serial_when_adb_reports_failure_with_exit_code_zero()
    {
        var (adb, _, _) = Build(r => r.WhenContains("connect", MockAdbResponses.ConnectFailed));

        var result = await adb.ConnectAsync("192.168.1.200");

        Assert.True(result.Success); // adb really does exit 0 here
        Assert.Null(adb.Serial);
    }

    [Fact]
    public async Task GetDevicesAsync_parses_the_listing()
    {
        var (adb, _, _) = Build(r => r.WhenContains("devices", MockAdbResponses.DevicesConnected));

        var devices = await adb.GetDevicesAsync();

        Assert.Single(devices);
        Assert.Equal(AdbDeviceState.Online, devices[0].State);
    }

    [Fact]
    public async Task GetDeviceInfoAsync_reads_everything_in_a_single_invocation()
    {
        var (adb, runner, _) = Build(r => r.WhenContains("getprop", MockAdbResponses.GetPropDump));
        adb.Serial = "192.168.1.147:5555";

        var device = await adb.GetDeviceInfoAsync();

        Assert.NotNull(device);
        Assert.Equal("AFTALMO", device!.Model);
        Assert.Single(runner.Invocations);
        Assert.Equal("-s 192.168.1.147:5555 shell getprop", runner.Invocations[0]);
    }

    [Fact]
    public async Task GetDeviceInfoAsync_returns_null_when_the_read_fails()
    {
        var (adb, _, _) = Build(r => r.WhenContains("getprop", string.Empty, "error: device offline", 1));

        Assert.Null(await adb.GetDeviceInfoAsync());
    }

    [Fact]
    public async Task ShellBatchAsync_sends_one_process_for_many_reads()
    {
        var batchOutput = string.Join(
            "\n",
            "0",
            AdbArguments.BatchSeparator,
            "1",
            AdbArguments.BatchSeparator,
            "null");

        var (adb, runner, _) = Build(r => r.WhenContains("settings get", batchOutput));

        var outputs = await adb.ShellBatchAsync([
            ["settings", "get", "global", "window_animation_scale"],
            ["settings", "get", "secure", "amz_limit_ad_tracking"],
            ["settings", "get", "secure", "screensaver_enabled"],
        ]);

        Assert.Single(runner.Invocations);
        Assert.Equal(["0", "1", "null"], outputs);
    }

    [Fact]
    public async Task ShellAsync_rejects_a_token_that_is_not_shell_safe()
    {
        var (adb, _, _) = Build();

        await Assert.ThrowsAsync<ArgumentException>(() => adb.ShellAsync(["pm", "list; reboot"]));
    }

    [Fact]
    public async Task GetSettingAsync_normalises_the_unset_marker()
    {
        var (adb, _, _) = Build(r => r.WhenContains("settings get", "null"));

        Assert.Null(await adb.GetSettingAsync(SettingScope.Secure, "amz_limit_ad_tracking"));
    }

    [Fact]
    public async Task DryRun_blocks_writes_but_still_allows_reads()
    {
        var (adb, runner, log) = Build(r => r
            .WhenContains("getprop", MockAdbResponses.GetPropDump)
            .WhenContains("disable-user", MockAdbResponses.DisableUserSuccess));

        adb.DryRun = true;

        var write = await adb.DisablePackageAsync("com.amazon.gamehub");
        var read = await adb.GetDeviceInfoAsync();

        Assert.True(write.WasDryRun);
        Assert.True(write.Success);
        Assert.NotNull(read);
        Assert.DoesNotContain(runner.Invocations, i => i.Contains("disable-user", StringComparison.Ordinal));
        Assert.Contains(runner.Invocations, i => i.Contains("getprop", StringComparison.Ordinal));
        Assert.True(log.Contains(LogLevel.DryRun, "would execute"));
    }

    [Fact]
    public async Task DryRun_covers_every_mutating_operation()
    {
        var apk = Path.Combine(Path.GetTempPath(), $"firereplace-{Guid.NewGuid():N}.apk");
        File.WriteAllText(apk, "pretend apk");

        try
        {
            var (adb, runner, _) = Build();
            adb.DryRun = true;

            var results = new[]
            {
                await adb.DisablePackageAsync("com.amazon.gamehub"),
                await adb.EnablePackageAsync("com.amazon.gamehub"),
                await adb.PutSettingAsync(SettingScope.Global, "window_animation_scale", "0"),
                await adb.GrantPermissionAsync("io.github.toolicious.homeonfire", "android.permission.WRITE_SECURE_SETTINGS"),
                await adb.InstallApkAsync(apk),
                await adb.AllowNotificationListenerAsync("com.spocky.projengmenu/.services.notification.NotificationListener"),
                await adb.RebootAsync(),
            };

            Assert.All(results, r => Assert.True(r.WasDryRun));
            Assert.Empty(runner.Invocations);
        }
        finally
        {
            File.Delete(apk);
        }
    }

    [Fact]
    public async Task A_missing_adb_path_is_reported_instead_of_throwing()
    {
        var adb = new AdbService(new FakeProcessRunner(), new RecordingLog(), adbPath: null);

        var result = await adb.EnablePackageAsync("com.amazon.gamehub");

        Assert.False(result.Success);
        Assert.Equal(-1, result.ExitCode);
        Assert.Contains("adb.exe", result.Error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Every_invocation_raises_CommandCompleted()
    {
        var (adb, _, _) = Build(r => r.WhenContains("devices", MockAdbResponses.DevicesConnected));

        var seen = new List<AdbCommandResult>();
        adb.CommandCompleted += (_, result) => seen.Add(result);

        await adb.GetDevicesAsync();

        Assert.Single(seen);
        Assert.Equal("adb devices", seen[0].DisplayCommand);
    }

    [Fact]
    public async Task Failures_are_logged_with_the_exit_code_and_stderr()
    {
        var (adb, _, log) = Build(r => r.WhenContains("disable-user", string.Empty, MockAdbResponses.SecurityException, 1));

        var result = await adb.DisablePackageAsync("com.amazon.client.metrics");

        Assert.False(result.Success);
        Assert.Contains("SecurityException", result.CombinedOutput, StringComparison.Ordinal);
        Assert.True(log.Contains(LogLevel.Error, "failed with exit code 1"));
    }

    [Fact]
    public async Task ListPackagesAsync_returns_an_empty_list_when_the_command_fails()
    {
        var (adb, _, _) = Build(r => r.WhenContains("pm list", string.Empty, "error: closed", 1));

        Assert.Empty(await adb.ListPackagesAsync(PackageListFilter.All));
    }

    [Fact]
    public async Task DisconnectAsync_clears_the_serial()
    {
        var (adb, _, _) = Build(r => r.WhenContains("disconnect", "disconnected 192.168.1.147:5555"));
        adb.Serial = "192.168.1.147:5555";

        await adb.DisconnectAsync();

        Assert.Null(adb.Serial);
    }
}
