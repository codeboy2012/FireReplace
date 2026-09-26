using FireReplace.Core.Configuration;
using FireReplace.Core.Logging;
using Xunit;

namespace FireReplace.Tests;

public sealed class ConfigAndLoggingTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), $"firereplace-config-{Guid.NewGuid():N}");

    public void Dispose()
    {
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, recursive: true);
        }
    }

    private string ConfigPath => Path.Combine(_directory, "settings.json");

    [Fact]
    public void Defaults_are_safe_out_of_the_box()
    {
        var config = new AppConfig();

        Assert.False(config.FirstRunCompleted);
        Assert.False(config.DryRun);
        Assert.False(config.AutoConnectOnStartup);
        Assert.False(config.SaveLogsAutomatically);
        Assert.True(config.ConfirmDestructiveActions);
        Assert.True(config.ConfirmAdvancedActions);
        Assert.True(config.RememberLastIp);
        Assert.Equal(5555, config.AdbPort);
        Assert.Null(config.LastIpAddress);
        Assert.Null(config.PlatformToolsDirectory);
        Assert.Equal(AppTheme.Dark, config.Theme);
    }

    [Fact]
    public async Task Configuration_round_trips_through_the_store()
    {
        var store = new ConfigStore(ConfigPath);

        var config = new AppConfig
        {
            FirstRunCompleted = true,
            LastIpAddress = "192.168.1.147",
            DryRun = true,
            Theme = AppTheme.Midnight,
            ConnectTimeoutSeconds = 15,
            MaxLogEntries = 500,
        };

        await store.SaveAsync(config);
        var loaded = new ConfigStore(ConfigPath).Load();

        Assert.True(loaded.FirstRunCompleted);
        Assert.Equal("192.168.1.147", loaded.LastIpAddress);
        Assert.True(loaded.DryRun);
        Assert.Equal(AppTheme.Midnight, loaded.Theme);
        Assert.Equal(15, loaded.ConnectTimeoutSeconds);
        Assert.Equal(500, loaded.MaxLogEntries);
        Assert.Null(store.LastError);
    }

    [Fact]
    public void A_missing_file_yields_defaults_without_an_error()
    {
        var store = new ConfigStore(ConfigPath);
        var config = store.Load();

        Assert.False(config.FirstRunCompleted);
        Assert.Null(store.LastError);
    }

    [Fact]
    public void A_corrupt_file_yields_defaults_and_reports_the_problem()
    {
        Directory.CreateDirectory(_directory);
        File.WriteAllText(ConfigPath, "{ not json at all");

        var store = new ConfigStore(ConfigPath);
        var config = store.Load();

        Assert.False(config.FirstRunCompleted);
        Assert.NotNull(store.LastError);
    }

    [Fact]
    public async Task Saving_twice_does_not_leave_a_temporary_file_behind()
    {
        var store = new ConfigStore(ConfigPath);

        await store.SaveAsync(new AppConfig());
        await store.SaveAsync(new AppConfig { DryRun = true });

        Assert.False(File.Exists(ConfigPath + ".tmp"));
        Assert.True(new ConfigStore(ConfigPath).Load().DryRun);
    }

    [Fact]
    public void Clone_produces_an_independent_copy()
    {
        var config = new AppConfig { LastIpAddress = "10.0.0.5" };
        var clone = config.Clone();

        clone.LastIpAddress = "10.0.0.6";

        Assert.Equal("10.0.0.5", config.LastIpAddress);
        Assert.Equal("10.0.0.6", clone.LastIpAddress);
    }

    [Fact]
    public void AppPaths_places_backups_and_logs_next_to_the_executable()
    {
        var appDir = Path.Combine(_directory, "app");
        var paths = new AppPaths(appDir);

        Assert.Equal(Path.Combine(appDir, "backups"), paths.BackupDirectory);
        Assert.Equal(Path.Combine(appDir, "logs"), paths.LogDirectory);
        Assert.EndsWith(Path.Combine("FireReplace", "settings.json"), paths.ConfigFile);
    }

    [Fact]
    public void The_log_keeps_only_the_configured_number_of_entries()
    {
        var log = new LogService(maxEntries: 100);

        for (var i = 0; i < 250; i++)
        {
            log.Info($"entry {i}");
        }

        var entries = log.Snapshot();
        Assert.Equal(100, entries.Count);
        Assert.Equal("entry 249", entries[^1].Message);
    }

    [Fact]
    public void Lowering_the_limit_trims_immediately()
    {
        var log = new LogService(maxEntries: 1000);
        for (var i = 0; i < 500; i++)
        {
            log.Info($"entry {i}");
        }

        log.MaxEntries = 120;

        Assert.Equal(120, log.Snapshot().Count);
    }

    [Fact]
    public void Control_characters_in_device_output_are_neutralised()
    {
        var log = new LogService();
        log.Write(LogLevel.Error, "adb", "failure", "before\u0007after\r\nsecond");

        var entry = Assert.Single(log.Snapshot());
        Assert.DoesNotContain('\u0007', entry.Detail!);
        Assert.DoesNotContain('\r', entry.Detail!);
        Assert.Contains("second", entry.Detail!, StringComparison.Ordinal);
    }

    [Fact]
    public void Rendered_lines_carry_a_timestamp_and_a_status_glyph()
    {
        var log = new LogService();
        log.Success("Connected");

        var line = log.Render();

        Assert.Matches(@"^\[\d{2}:\d{2}:\d{2}\] \u2713 Connected", line.TrimEnd());
    }

    [Fact]
    public async Task Saving_the_log_writes_the_rendered_text()
    {
        var log = new LogService();
        log.Info("Reading device information");
        log.Success("AFTALMO detected");

        var path = Path.Combine(_directory, "logs", "export.log");
        await log.SaveAsync(path);

        var text = await File.ReadAllTextAsync(path);
        Assert.Contains("Reading device information", text, StringComparison.Ordinal);
        Assert.Contains("AFTALMO detected", text, StringComparison.Ordinal);
    }

    [Fact]
    public void Clearing_the_log_raises_the_event_and_empties_the_buffer()
    {
        var log = new LogService();
        var cleared = false;
        log.Cleared += (_, _) => cleared = true;

        log.Info("something");
        log.Clear();

        Assert.True(cleared);
        Assert.Empty(log.Snapshot());
    }

    [Fact]
    public void Automatic_saving_only_happens_when_it_is_switched_on()
    {
        var path = Path.Combine(_directory, "logs", "auto.log");

        var log = new LogService();
        log.Info("not saved");
        Assert.False(File.Exists(path));

        log.SetAutoSavePath(path);
        log.Info("saved");

        Assert.True(File.Exists(path));
        var text = File.ReadAllText(path);
        Assert.Contains("saved", text, StringComparison.Ordinal);
        Assert.DoesNotContain("not saved", text, StringComparison.Ordinal);
    }
}
