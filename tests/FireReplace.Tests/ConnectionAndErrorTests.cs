using FireReplace.Core.Adb;
using FireReplace.Core.Services;
using FireReplace.Tests.Fakes;
using Xunit;

namespace FireReplace.Tests;

public class ConnectionAndErrorTests
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

    private static (ConnectionService Connection, AdbService Adb) Build(Action<FakeProcessRunner> configure)
    {
        var runner = new FakeProcessRunner();
        configure(runner);

        var log = new RecordingLog();
        var adb = new AdbService(runner, log, FakeAdbPath());
        return (new ConnectionService(adb, log), adb);
    }

    [Fact]
    public async Task A_successful_connection_is_verified_against_adb_devices()
    {
        var (connection, _) = Build(r => r
            .WhenContains("connect", MockAdbResponses.ConnectSucceeded)
            .WhenContains("devices", MockAdbResponses.DevicesConnected));

        var outcome = await connection.ConnectAsync("192.168.1.147");

        Assert.Equal(ConnectionStatus.Connected, outcome.Status);
        Assert.True(outcome.IsUsable);
        Assert.Equal("192.168.1.147:5555", connection.Serial);
        Assert.Equal("Connected to 192.168.1.147:5555", outcome.Headline);
    }

    [Fact]
    public async Task An_unauthorized_device_produces_the_check_your_TV_message()
    {
        var (connection, _) = Build(r => r
            .WhenContains("connect", MockAdbResponses.ConnectSucceeded)
            .WhenContains("devices", MockAdbResponses.DevicesUnauthorized));

        var outcome = await connection.ConnectAsync("192.168.1.147");

        Assert.Equal(ConnectionStatus.Unauthorized, outcome.Status);
        Assert.Equal("Check your TV", outcome.Headline);
        Assert.Contains("asking whether to allow this computer", outcome.Detail!, StringComparison.Ordinal);
        Assert.Contains(outcome.Causes, c => c.Contains("Allow", StringComparison.Ordinal));
        Assert.False(outcome.IsUsable);
    }

    [Fact]
    public async Task A_refused_connection_explains_the_likely_causes()
    {
        var (connection, _) = Build(r => r.WhenContains("connect", MockAdbResponses.ConnectFailed));

        var outcome = await connection.ConnectAsync("192.168.1.200");

        Assert.Equal(ConnectionStatus.Failed, outcome.Status);
        Assert.Equal("ADB could not connect to the Fire TV.", outcome.Headline);
        Assert.Equal(ConnectionOutcome.ConnectionFailureCauses, outcome.Causes);
        Assert.DoesNotContain("Exit code", outcome.Headline, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task An_invalid_address_is_rejected_before_adb_is_launched()
    {
        var runner = new FakeProcessRunner();
        var log = new RecordingLog();
        var connection = new ConnectionService(new AdbService(runner, log, FakeAdbPath()), log);

        var outcome = await connection.ConnectAsync("not-an-ip");

        Assert.Equal(ConnectionStatus.Failed, outcome.Status);
        Assert.Empty(runner.Invocations);
    }

    [Fact]
    public async Task A_missing_adb_reports_the_toolchain_rather_than_the_network()
    {
        var runner = new FakeProcessRunner();
        var log = new RecordingLog();
        var connection = new ConnectionService(new AdbService(runner, log, adbPath: null), log);

        var outcome = await connection.ConnectAsync("192.168.1.147");

        Assert.Equal(ConnectionStatus.ToolchainMissing, outcome.Status);
        Assert.Contains("platform-tools", string.Join(' ', outcome.Causes), StringComparison.Ordinal);
        Assert.Empty(runner.Invocations);
    }

    [Fact]
    public async Task A_device_absent_from_the_listing_is_reported_as_a_failure()
    {
        var (connection, _) = Build(r => r
            .WhenContains("connect", MockAdbResponses.ConnectSucceeded)
            .WhenContains("devices", "List of devices attached"));

        var outcome = await connection.ConnectAsync("192.168.1.147");

        Assert.Equal(ConnectionStatus.Failed, outcome.Status);
        Assert.Contains("did not appear", outcome.Headline, StringComparison.Ordinal);
    }

    [Fact]
    public async Task DisconnectAsync_returns_to_the_idle_state()
    {
        var (connection, adb) = Build(r => r
            .WhenContains("connect", MockAdbResponses.ConnectSucceeded)
            .WhenContains("devices", MockAdbResponses.DevicesConnected)
            .WhenContains("disconnect", "disconnected"));

        await connection.ConnectAsync("192.168.1.147");
        await connection.DisconnectAsync();

        Assert.Equal(ConnectionStatus.Idle, connection.Status);
        Assert.Null(connection.Serial);
        Assert.Null(adb.Serial);
    }

    [Theory]
    [InlineData("failed to connect to '192.168.1.5:5555'")]
    [InlineData("cannot connect to daemon")]
    [InlineData("unable to connect to 192.168.1.5:5555")]
    [InlineData("No route to host")]
    [InlineData("Connection refused")]
    public void IsFailureText_recognises_the_known_connection_failures(string text) =>
        Assert.True(ConnectionOutcome.IsFailureText(text));

    [Fact]
    public void IsFailureText_does_not_misread_a_success_message()
    {
        Assert.False(ConnectionOutcome.IsFailureText(MockAdbResponses.ConnectSucceeded));
        Assert.False(ConnectionOutcome.IsFailureText(null));
    }

    private static AdbCommandResult Result(string stdout, string stderr = "", int exitCode = 0, bool timedOut = false) =>
        new(["shell", "pm", "disable-user"], new ProcessResult(exitCode, stdout, stderr, timedOut, TimeSpan.Zero), false);

    [Fact]
    public void A_timeout_is_explained_as_the_TV_not_responding()
    {
        var failure = AdbErrorInterpreter.Interpret(
            Result(string.Empty, "timed out", -1, timedOut: true),
            "disable com.amazon.gamehub");

        Assert.Contains("stopped responding", failure.Headline, StringComparison.Ordinal);
        Assert.False(failure.IsProtectedByFireOs);
    }

    [Fact]
    public void A_security_exception_is_explained_without_suggesting_a_bypass()
    {
        var failure = AdbErrorInterpreter.Interpret(
            Result(MockAdbResponses.SecurityException),
            "disable com.amazon.client.metrics");

        Assert.Equal("Fire OS protected this package.", failure.Headline);
        Assert.True(failure.IsProtectedByFireOs);

        var advice = string.Join(' ', failure.Causes).ToLowerInvariant();
        Assert.DoesNotContain("root", advice);
        Assert.DoesNotContain("unlock", advice);
        Assert.DoesNotContain("flash", advice);
        Assert.DoesNotContain("exploit", advice);
    }

    [Fact]
    public void A_missing_executable_is_explained_as_a_setup_problem()
    {
        var failure = AdbErrorInterpreter.Interpret(
            new AdbCommandResult(["devices"], ProcessResult.NotRun("Executable not found: adb.exe"), false),
            "check the device");

        Assert.Contains("could not start adb.exe", failure.Headline, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("AdbWinApi.dll", string.Join(' ', failure.Causes), StringComparison.Ordinal);
    }

    [Fact]
    public void An_offline_device_is_explained_as_a_lost_connection()
    {
        var failure = AdbErrorInterpreter.Interpret(
            Result(string.Empty, "error: device offline", 1),
            "read the package list");

        Assert.Contains("no longer connected", failure.Headline, StringComparison.Ordinal);
    }

    [Fact]
    public void An_unknown_package_is_explained_as_a_model_difference()
    {
        var failure = AdbErrorInterpreter.Interpret(
            Result("Unknown package: com.amazon.sneakpeek"),
            "disable com.amazon.sneakpeek");

        Assert.Contains("not installed", failure.Headline, StringComparison.Ordinal);
        Assert.Contains("differ between Fire OS builds", string.Join(' ', failure.Causes), StringComparison.Ordinal);
    }

    [Fact]
    public void An_abi_mismatch_explains_what_to_download()
    {
        var failure = AdbErrorInterpreter.Interpret(
            Result(MockAdbResponses.InstallAbiFailure),
            "install Projectivy");

        Assert.Contains("NO_MATCHING_ABIS", failure.Headline, StringComparison.Ordinal);
        Assert.Contains("CPU architecture", string.Join(' ', failure.Causes), StringComparison.Ordinal);
    }

    [Fact]
    public void An_unrecognised_failure_still_shows_the_raw_output()
    {
        var failure = AdbErrorInterpreter.Interpret(
            Result(string.Empty, "something nobody has seen before", 42),
            "do the thing");

        Assert.Contains("could not do the thing", failure.Headline, StringComparison.Ordinal);
        Assert.Contains("something nobody has seen before", failure.RawDetail!, StringComparison.Ordinal);
    }

    [Fact]
    public void An_empty_failure_still_reports_the_exit_code_somewhere()
    {
        var failure = AdbErrorInterpreter.Interpret(Result(string.Empty, string.Empty, 7), "do the thing");

        Assert.Contains("7", failure.RawDetail!, StringComparison.Ordinal);
    }
}
