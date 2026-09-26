using FireReplace.Core.Validation;
using Xunit;

namespace FireReplace.Tests;

public class ValidationTests
{
    [Theory]
    [InlineData("192.168.1.147")]
    [InlineData("10.0.0.1")]
    [InlineData("255.255.255.255")]
    [InlineData("0.0.0.0")]
    [InlineData("  192.168.1.10  ")]
    [InlineData("fe80::1")]
    [InlineData("2001:db8::8a2e:370:7334")]
    public void IsIpAddress_accepts_valid_addresses(string value) =>
        Assert.True(Validate.IsIpAddress(value));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("192.168.1")]
    [InlineData("192.168.1.256")]
    [InlineData("192.168.1.1.1")]
    [InlineData("192.168.1.")]
    [InlineData("192.168.01.1x")]
    [InlineData("localhost")]
    [InlineData("192.168.1.147; rm -rf /")]
    [InlineData("$(whoami)")]
    [InlineData("192.168.1.147 && shutdown")]
    public void IsIpAddress_rejects_invalid_addresses(string? value) =>
        Assert.False(Validate.IsIpAddress(value));

    [Fact]
    public void IpAddress_trims_and_returns_the_address() =>
        Assert.Equal("192.168.1.147", Validate.IpAddress("  192.168.1.147 "));

    [Fact]
    public void IpAddress_throws_for_an_invalid_address() =>
        Assert.Throws<ArgumentException>(() => Validate.IpAddress("not-an-ip"));

    [Theory]
    [InlineData("com.amazon.gamehub")]
    [InlineData("com.android.nfc")]
    [InlineData("io.github.toolicious.homeonfire")]
    [InlineData("com.spocky.projengmenu")]
    [InlineData("android.permission.WRITE_SECURE_SETTINGS")]
    [InlineData("a")]
    [InlineData("com.amazon.bueller.photos")]
    [InlineData("com.example.app_2")]
    public void IsPackageName_accepts_valid_names(string value) =>
        Assert.True(Validate.IsPackageName(value));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(".com.example")]
    [InlineData("com.example.")]
    [InlineData("com..example")]
    [InlineData("2com.example")]
    [InlineData("com.example app")]
    [InlineData("com.example;rm")]
    [InlineData("com.example/../etc")]
    [InlineData("com.example`id`")]
    [InlineData("com.example$(id)")]
    [InlineData("com.example|cat")]
    public void IsPackageName_rejects_invalid_names(string? value) =>
        Assert.False(Validate.IsPackageName(value));

    [Theory]
    [InlineData("io.github.toolicious.homeonfire/.HijackService")]
    [InlineData("com.spocky.projengmenu/.services.ProjectivyAccessibilityService")]
    [InlineData("com.spocky.projengmenu/.services.notification.NotificationListener")]
    [InlineData("com.example/com.example.FullyQualified")]
    [InlineData("com.example/.Outer$Inner")]
    public void IsComponentName_accepts_valid_components(string value) =>
        Assert.True(Validate.IsComponentName(value));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("com.example")]
    [InlineData("com.example/")]
    [InlineData("/.Service")]
    [InlineData("com.example/.Service;reboot")]
    [InlineData("com.example/.Service two")]
    [InlineData("com.example/.Service:other/.Service")]
    public void IsComponentName_rejects_invalid_components(string? value) =>
        Assert.False(Validate.IsComponentName(value));

    [Theory]
    [InlineData("window_animation_scale")]
    [InlineData("USAGE_METRICS_UPLOAD_ENABLED")]
    [InlineData("amz_limit_ad_tracking")]
    [InlineData("advertisingIdApp:interestBasedAds:value")]
    [InlineData("settingsApp:appUsageData:value")]
    [InlineData("screensaver_activate_on_dock")]
    public void IsSettingKey_accepts_catalog_keys(string value) =>
        Assert.True(Validate.IsSettingKey(value));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("key with space")]
    [InlineData("key;other")]
    [InlineData("$key")]
    [InlineData("key`")]
    [InlineData("key|pipe")]
    [InlineData(":leading")]
    public void IsSettingKey_rejects_unsafe_keys(string? value) =>
        Assert.False(Validate.IsSettingKey(value));

    [Theory]
    [InlineData("settings")]
    [InlineData("pm")]
    [InlineData("advertisingIdApp:interestBasedAds:value")]
    [InlineData("--user")]
    [InlineData("0")]
    [InlineData("com.amazon.gamehub")]
    public void IsShellSafeToken_accepts_batchable_tokens(string value) =>
        Assert.True(Validate.IsShellSafeToken(value));

    [Theory]
    [InlineData("two words")]
    [InlineData("a;b")]
    [InlineData("a&&b")]
    [InlineData("a|b")]
    [InlineData("$(id)")]
    [InlineData("`id`")]
    [InlineData("a>file")]
    [InlineData("a\nb")]
    [InlineData("\"quoted\"")]
    public void IsShellSafeToken_rejects_shell_metacharacters(string value) =>
        Assert.False(Validate.IsShellSafeToken(value));

    [Theory]
    [InlineData(1)]
    [InlineData(5555)]
    [InlineData(65535)]
    public void IsPort_accepts_usable_ports(int port) => Assert.True(Validate.IsPort(port));

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(65536)]
    public void IsPort_rejects_out_of_range_ports(int port) => Assert.False(Validate.IsPort(port));

    [Fact]
    public void IsExistingApkPath_rejects_a_missing_file() =>
        Assert.False(Validate.IsExistingApkPath(Path.Combine(Path.GetTempPath(), "does-not-exist.apk")));

    [Fact]
    public void IsExistingApkPath_rejects_a_real_file_with_the_wrong_extension()
    {
        var path = Path.Combine(Path.GetTempPath(), $"firereplace-{Guid.NewGuid():N}.txt");
        File.WriteAllText(path, "not an apk");

        try
        {
            Assert.False(Validate.IsExistingApkPath(path));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void IsExistingApkPath_accepts_an_existing_apk()
    {
        var path = Path.Combine(Path.GetTempPath(), $"firereplace-{Guid.NewGuid():N}.apk");
        File.WriteAllText(path, "pretend apk");

        try
        {
            Assert.True(Validate.IsExistingApkPath(path));
        }
        finally
        {
            File.Delete(path);
        }
    }
}
