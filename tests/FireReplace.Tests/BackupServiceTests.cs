using System.Text.Json;
using FireReplace.Core.Adb;
using FireReplace.Core.Catalog;
using FireReplace.Core.Models;
using FireReplace.Core.Services;
using FireReplace.Tests.Fakes;
using Xunit;

namespace FireReplace.Tests;

public sealed class BackupServiceTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), $"firereplace-backups-{Guid.NewGuid():N}");

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

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

    private BackupService CreateService(out AdbService adb)
    {
        adb = new AdbService(new FakeProcessRunner(), new RecordingLog(), FakeAdbPath())
        {
            Serial = "192.168.1.147:5555",
        };

        return new BackupService(_root, adb, new RecordingLog());
    }

    private static DeviceState SampleState() => new()
    {
        Device = new DeviceInfo
        {
            Serial = "192.168.1.147:5555",
            Model = "AFTALMO",
            Product = "almond",
            Manufacturer = "Amazon",
            AndroidRelease = "9",
            BuildIncremental = "0035736899972",
            FireOsVersion = "Fire OS 7.01065.5585",
            Abi = "armeabi-v7a",
            FriendlyName = "Amazon Insignia Fire TV",
        },
        Settings = new Dictionary<string, string?>(StringComparer.Ordinal)
        {
            ["global/window_animation_scale"] = "1",
            ["global/transition_animation_scale"] = "0",
            ["global/animator_duration_scale"] = null,
            ["secure/amz_limit_ad_tracking"] = "0",
            ["secure/enabled_accessibility_services"] = "io.github.toolicious.homeonfire/.HijackService",
        },
        AccessibilityServices = ["io.github.toolicious.homeonfire/.HijackService"],
        NotificationListeners = [],
        InstalledPackages = new HashSet<string>(
            ["com.amazon.gamehub", "com.android.nfc", "com.amazon.tv.launcher"], StringComparer.Ordinal),
        DisabledPackages = new HashSet<string>(["com.android.nfc"], StringComparer.Ordinal),
    };

    [Fact]
    public async Task CreateAsync_writes_the_three_json_files()
    {
        var service = CreateService(out _);

        var entry = await service.CreateAsync(SampleState());

        Assert.True(Directory.Exists(entry.Directory));
        foreach (var fileName in BackupService.FileNames)
        {
            Assert.True(File.Exists(Path.Combine(entry.Directory, fileName)), fileName);
        }
    }

    [Fact]
    public async Task The_backup_folder_is_named_with_a_sortable_timestamp()
    {
        var service = CreateService(out _);
        var entry = await service.CreateAsync(SampleState());

        Assert.Matches(@"^\d{4}-\d{2}-\d{2}_\d{6}$", entry.Id);
    }

    [Fact]
    public async Task Device_json_round_trips_the_device_identity()
    {
        var service = CreateService(out _);
        var entry = await service.CreateAsync(SampleState());

        var json = await File.ReadAllTextAsync(Path.Combine(entry.Directory, "device.json"));
        using var document = JsonDocument.Parse(json);
        var device = document.RootElement.GetProperty("Device");

        Assert.Equal("AFTALMO", device.GetProperty("Model").GetString());
        Assert.Equal("almond", device.GetProperty("Product").GetString());
        Assert.Equal("9", device.GetProperty("AndroidRelease").GetString());
        Assert.Equal("armeabi-v7a", device.GetProperty("Abi").GetString());
    }

    [Fact]
    public async Task Settings_json_records_values_including_unset_keys()
    {
        var service = CreateService(out _);
        var entry = await service.CreateAsync(SampleState());

        var (_, settings, _) = service.Read(entry.Directory);

        Assert.NotNull(settings);
        Assert.Equal("1", settings!.Settings["global/window_animation_scale"]);
        Assert.Null(settings.Settings["global/animator_duration_scale"]);
        Assert.Equal(["io.github.toolicious.homeonfire/.HijackService"], settings.AccessibilityServices);
        Assert.Empty(settings.NotificationListeners);
    }

    [Fact]
    public async Task Packages_json_records_the_state_of_every_catalog_package()
    {
        var service = CreateService(out _);
        var entry = await service.CreateAsync(SampleState());

        var (_, _, packages) = service.Read(entry.Directory);

        Assert.NotNull(packages);
        Assert.Equal(PackageCatalog.All.Count(), packages!.Packages.Count);
        Assert.Equal(PackageState.Enabled, packages.Packages["com.amazon.gamehub"]);
        Assert.Equal(PackageState.Disabled, packages.Packages["com.android.nfc"]);
        Assert.Equal(PackageState.NotInstalled, packages.Packages["com.amazon.sneakpeek"]);
    }

    [Fact]
    public async Task List_returns_backups_newest_first()
    {
        var service = CreateService(out _);

        var first = await service.CreateAsync(SampleState());
        await Task.Delay(1100); // the folder name has one-second resolution
        var second = await service.CreateAsync(SampleState());

        var entries = service.List();

        Assert.Equal(2, entries.Count);
        Assert.Equal(second.Id, entries[0].Id);
        Assert.Equal(first.Id, entries[1].Id);
        Assert.Equal("Amazon Insignia Fire TV", entries[0].DeviceName);
    }

    [Fact]
    public void List_returns_nothing_when_the_folder_does_not_exist()
    {
        var service = new BackupService(
            Path.Combine(_root, "missing"),
            new AdbService(new FakeProcessRunner(), new RecordingLog(), FakeAdbPath()),
            new RecordingLog());

        Assert.Empty(service.List());
    }

    [Fact]
    public async Task An_unreadable_backup_folder_is_skipped_rather_than_crashing()
    {
        var service = CreateService(out _);
        await service.CreateAsync(SampleState());

        var broken = Path.Combine(_root, "2020-01-01_000000");
        Directory.CreateDirectory(broken);
        await File.WriteAllTextAsync(Path.Combine(broken, "device.json"), "{ this is not json");

        var entries = service.List();

        Assert.Single(entries);
    }

    [Fact]
    public async Task The_restore_plan_lists_only_the_differences()
    {
        var service = CreateService(out _);
        var entry = await service.CreateAsync(SampleState());

        // The device has since changed: animations are off and gamehub was disabled.
        var current = new DeviceState
        {
            Settings = new Dictionary<string, string?>(StringComparer.Ordinal)
            {
                ["global/window_animation_scale"] = "0",
                ["global/transition_animation_scale"] = "0",
                ["secure/amz_limit_ad_tracking"] = "1",
            },
            InstalledPackages = new HashSet<string>(
                ["com.amazon.gamehub", "com.android.nfc", "com.amazon.tv.launcher"], StringComparer.Ordinal),
            DisabledPackages = new HashSet<string>(["com.amazon.gamehub", "com.android.nfc"], StringComparer.Ordinal),
        };

        var plan = service.BuildRestorePlan(entry, current);
        var targets = plan.Changes.Select(c => c.Target).ToArray();

        Assert.Contains("global window_animation_scale", targets);
        Assert.Contains("secure amz_limit_ad_tracking", targets);
        Assert.Contains("com.amazon.gamehub", targets);
        Assert.DoesNotContain("global transition_animation_scale", targets);
        Assert.DoesNotContain("com.android.nfc", targets);
    }

    [Fact]
    public async Task The_restore_plan_never_disables_a_guarded_package()
    {
        var service = CreateService(out _);

        var state = SampleState();
        var disabled = new HashSet<string>(state.DisabledPackages, StringComparer.Ordinal) { "com.amazon.tv.launcher" };
        var backedUp = new DeviceState
        {
            Device = state.Device,
            Settings = state.Settings,
            InstalledPackages = state.InstalledPackages,
            DisabledPackages = disabled,
        };

        var entry = await service.CreateAsync(backedUp);

        var current = new DeviceState
        {
            InstalledPackages = state.InstalledPackages,
            DisabledPackages = new HashSet<string>(StringComparer.Ordinal),
        };

        var plan = service.BuildRestorePlan(entry, current);

        Assert.DoesNotContain(plan.Changes, c =>
            c.Kind == Core.Actions.ChangeKind.PackageDisable && c.Target == "com.amazon.tv.launcher");
    }

    [Fact]
    public async Task A_restore_plan_against_an_identical_device_is_empty()
    {
        var service = CreateService(out _);
        var state = SampleState();
        var entry = await service.CreateAsync(state);

        var plan = service.BuildRestorePlan(entry, state);

        Assert.True(plan.IsEmpty);
    }
}
