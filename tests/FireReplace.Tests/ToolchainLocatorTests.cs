using FireReplace.Core.Catalog;
using FireReplace.Core.Discovery;
using FireReplace.Tests.Fakes;
using Xunit;

namespace FireReplace.Tests;

public class ToolchainLocatorTests
{
    private static string Root => Path.GetFullPath(Path.Combine(Path.GetTempPath(), "FireReplaceLocator"));

    private static string AppDir => Path.Combine(Root, "FireReplace");

    [Fact]
    public void Adb_is_found_in_platform_tools_next_to_the_application()
    {
        var probe = new FakeFileProbe()
            .AddFile(Path.Combine(AppDir, "platform-tools", "adb.exe"))
            .AddFile(Path.Combine(AppDir, "platform-tools", "AdbWinApi.dll"))
            .AddFile(Path.Combine(AppDir, "platform-tools", "AdbWinUsbApi.dll"));

        var location = new ToolchainLocator(AppDir, probe).LocateAdb();

        Assert.True(location.Found);
        Assert.True(location.Complete);
        Assert.Equal(Path.Combine(AppDir, "platform-tools", "adb.exe"), location.AdbPath);
        Assert.Empty(location.MissingSupportLibraries);
    }

    [Fact]
    public void Missing_windows_usb_libraries_are_reported_separately()
    {
        var probe = new FakeFileProbe().AddFile(Path.Combine(AppDir, "platform-tools", "adb.exe"));

        var location = new ToolchainLocator(AppDir, probe).LocateAdb();

        Assert.True(location.Found);
        Assert.False(location.Complete);
        Assert.Equal(["AdbWinApi.dll", "AdbWinUsbApi.dll"], location.MissingSupportLibraries);
    }

    [Fact]
    public void Adb_is_found_in_an_ancestor_folder_during_development()
    {
        var deep = Path.Combine(AppDir, "src", "FireReplace", "bin", "Debug", "net8.0-windows");
        var probe = new FakeFileProbe().AddFile(Path.Combine(AppDir, "platform-tools", "adb.exe"));

        var location = new ToolchainLocator(deep, probe).LocateAdb();

        Assert.True(location.Found);
        Assert.Equal(Path.Combine(AppDir, "platform-tools", "adb.exe"), location.AdbPath);
    }

    [Fact]
    public void Adb_beside_the_executable_is_accepted_as_a_last_resort()
    {
        var probe = new FakeFileProbe().AddFile(Path.Combine(AppDir, "adb.exe"));

        var location = new ToolchainLocator(AppDir, probe).LocateAdb();

        Assert.True(location.Found);
        Assert.Equal(Path.Combine(AppDir, "adb.exe"), location.AdbPath);
    }

    [Fact]
    public void A_configured_folder_wins_over_discovery()
    {
        var custom = Path.Combine(Root, "sdk", "platform-tools");
        var probe = new FakeFileProbe()
            .AddFile(Path.Combine(AppDir, "platform-tools", "adb.exe"))
            .AddFile(Path.Combine(custom, "adb.exe"));

        var location = new ToolchainLocator(AppDir, probe).LocateAdb(custom);

        Assert.Equal(Path.Combine(custom, "adb.exe"), location.AdbPath);
    }

    [Fact]
    public void A_missing_adb_reports_every_place_that_was_searched()
    {
        var location = new ToolchainLocator(AppDir, new FakeFileProbe()).LocateAdb();

        Assert.False(location.Found);
        Assert.Null(location.AdbPath);
        Assert.NotEmpty(location.SearchedLocations);
        Assert.Contains(location.SearchedLocations, p => p.EndsWith("adb.exe", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void No_absolute_path_is_hard_coded_anywhere_in_discovery()
    {
        var elsewhere = Path.Combine(Root, "Portable", "FireReplace");
        var probe = new FakeFileProbe().AddFile(Path.Combine(elsewhere, "platform-tools", "adb.exe"));

        var location = new ToolchainLocator(elsewhere, probe).LocateAdb();

        Assert.True(location.Found);
        Assert.StartsWith(elsewhere, location.AdbPath!, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void The_apk_folder_is_found_next_to_the_application()
    {
        var probe = new FakeFileProbe().AddDirectory(Path.Combine(AppDir, "apks"));

        var directory = new ToolchainLocator(AppDir, probe).LocateApkDirectory();

        Assert.Equal(Path.Combine(AppDir, "apks"), directory);
    }

    [Fact]
    public void Known_apks_are_matched_by_their_file_patterns()
    {
        var apks = Path.Combine(AppDir, "apks");
        var probe = new FakeFileProbe()
            .AddFile(Path.Combine(apks, "home-on-fire.apk"))
            .AddFile(Path.Combine(apks, "ProjectivyLauncher-4.71-c95-xda-release.apk"));

        var locator = new ToolchainLocator(AppDir, probe);
        var located = locator.LocateApks(apks, LauncherCatalog.ApkDefinitions());

        Assert.Equal(2, located.Count);
        Assert.All(located, a => Assert.True(a.Found, a.DisplayName));
        Assert.Equal("home-on-fire.apk", located[0].FileName);
        Assert.Equal("ProjectivyLauncher-4.71-c95-xda-release.apk", located[1].FileName);
    }

    [Fact]
    public void A_missing_apk_is_reported_without_guessing_at_another_file()
    {
        var apks = Path.Combine(AppDir, "apks");
        var probe = new FakeFileProbe()
            .AddFile(Path.Combine(apks, "home-on-fire.apk"))
            .AddFile(Path.Combine(apks, "SomethingElse.apk"));

        var located = new ToolchainLocator(AppDir, probe).LocateApks(apks, LauncherCatalog.ApkDefinitions());

        Assert.True(located[0].Found);
        Assert.False(located[1].Found);
        Assert.Null(located[1].FileName);
    }

    [Fact]
    public void Apks_resolve_to_nothing_when_there_is_no_apk_folder()
    {
        var located = new ToolchainLocator(AppDir, new FakeFileProbe())
            .LocateApks(null, LauncherCatalog.ApkDefinitions());

        Assert.All(located, a => Assert.False(a.Found));
    }

    [Fact]
    public void Candidate_roots_start_at_the_application_folder_and_walk_upwards()
    {
        var deep = Path.Combine(AppDir, "a", "b", "c");
        var roots = new ToolchainLocator(deep, new FakeFileProbe()).CandidateRoots();

        Assert.Equal(deep, roots[0]);
        Assert.Contains(AppDir, roots);
        Assert.True(roots.Count <= 7);
    }
}
