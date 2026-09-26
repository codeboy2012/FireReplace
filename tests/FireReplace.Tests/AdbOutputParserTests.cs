using FireReplace.Core.Adb;
using FireReplace.Core.Models;
using FireReplace.Tests.Fakes;
using Xunit;

namespace FireReplace.Tests;

public class AdbOutputParserTests
{
    [Fact]
    public void ParseDevices_reads_an_authorized_device()
    {
        var devices = AdbOutputParser.ParseDevices(MockAdbResponses.DevicesConnected);

        var device = Assert.Single(devices);
        Assert.Equal("192.168.1.147:5555", device.Serial);
        Assert.Equal(AdbDeviceState.Online, device.State);
        Assert.True(device.IsUsable);
    }

    [Fact]
    public void ParseDevices_reads_an_unauthorized_device()
    {
        var device = Assert.Single(AdbOutputParser.ParseDevices(MockAdbResponses.DevicesUnauthorized));

        Assert.Equal(AdbDeviceState.Unauthorized, device.State);
        Assert.False(device.IsUsable);
        Assert.Equal("unauthorized", device.RawState);
    }

    [Fact]
    public void ParseDevices_skips_daemon_chatter_and_keeps_every_device()
    {
        var devices = AdbOutputParser.ParseDevices(MockAdbResponses.DevicesWithDaemonNoise);

        Assert.Equal(2, devices.Count);
        Assert.Equal(AdbDeviceState.Online, devices[0].State);
        Assert.Equal("1A2B3C4D", devices[1].Serial);
        Assert.Equal(AdbDeviceState.Offline, devices[1].State);
    }

    [Fact]
    public void ParseDevices_returns_nothing_for_empty_output()
    {
        Assert.Empty(AdbOutputParser.ParseDevices(null));
        Assert.Empty(AdbOutputParser.ParseDevices(string.Empty));
        Assert.Empty(AdbOutputParser.ParseDevices("List of devices attached"));
    }

    [Fact]
    public void ParseGetPropDump_reads_key_value_pairs_and_ignores_junk()
    {
        var properties = AdbOutputParser.ParseGetPropDump(MockAdbResponses.GetPropDump);

        Assert.Equal("AFTALMO", properties["ro.product.model"]);
        Assert.Equal("almond", properties["ro.build.product"]);
        Assert.Equal("9", properties["ro.build.version.release"]);
        Assert.Equal("0035736899972", properties["ro.build.version.incremental"]);
        Assert.False(properties.ContainsKey("malformed line without brackets"));
    }

    [Fact]
    public void BuildDeviceInfo_uses_the_values_the_device_reported()
    {
        var properties = AdbOutputParser.ParseGetPropDump(MockAdbResponses.GetPropDump);
        var device = AdbOutputParser.BuildDeviceInfo("192.168.1.147:5555", properties);

        Assert.Equal("192.168.1.147:5555", device.Serial);
        Assert.Equal("AFTALMO", device.Model);
        Assert.Equal("almond", device.Product);
        Assert.Equal("Amazon", device.Manufacturer);
        Assert.Equal("9", device.AndroidRelease);
        Assert.Equal("28", device.SdkLevel);
        Assert.Equal("0035736899972", device.BuildIncremental);
        Assert.Equal("Fire OS 7.01065.5585", device.FireOsVersion);
        Assert.Equal("armeabi-v7a", device.Abi);
        Assert.Equal("Amazon Insignia Fire TV", device.FriendlyName);
    }

    [Fact]
    public void BuildDeviceInfo_falls_back_to_the_serial_when_nothing_was_reported()
    {
        var device = AdbOutputParser.BuildDeviceInfo("1A2B3C4D", new Dictionary<string, string>());

        Assert.Null(device.Model);
        Assert.Equal("1A2B3C4D", device.DisplayName);
    }

    [Fact]
    public void BuildDeviceInfo_does_not_duplicate_the_manufacturer_in_the_friendly_name()
    {
        var properties = new Dictionary<string, string>
        {
            ["ro.product.manufacturer"] = "amazon",
            ["ro.product.model"] = "Amazon Fire TV Stick",
        };

        var device = AdbOutputParser.BuildDeviceInfo("serial", properties);
        Assert.Equal("Amazon Fire TV Stick", device.FriendlyName);
    }

    [Fact]
    public void ParsePackageList_reads_plain_output()
    {
        var packages = AdbOutputParser.ParsePackageList(MockAdbResponses.PackagesAll);

        Assert.Equal(7, packages.Count);
        Assert.Contains("com.amazon.gamehub", packages);
        Assert.Contains("com.spocky.projengmenu", packages);
    }

    [Fact]
    public void ParsePackageList_reads_output_that_includes_apk_paths()
    {
        var packages = AdbOutputParser.ParsePackageList(MockAdbResponses.PackagesWithPaths);

        Assert.Equal(["com.amazon.tv.launcher", "com.amazon.gamehub"], packages);
    }

    [Fact]
    public void ParsePackageList_ignores_lines_without_the_package_prefix() =>
        Assert.Empty(AdbOutputParser.ParsePackageList("error: device offline"));

    [Theory]
    [InlineData("0", "0")]
    [InlineData(" 1 ", "1")]
    [InlineData("1.0", "1.0")]
    public void ParseSettingValue_trims_real_values(string raw, string expected) =>
        Assert.Equal(expected, AdbOutputParser.ParseSettingValue(raw));

    [Theory]
    [InlineData("null")]
    [InlineData("NULL")]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void ParseSettingValue_treats_unset_keys_as_null(string? raw) =>
        Assert.Null(AdbOutputParser.ParseSettingValue(raw));

    [Fact]
    public void ParseComponentList_splits_on_colons()
    {
        var components = AdbOutputParser.ParseComponentList(MockAdbResponses.AccessibilityWithThirdParty);

        Assert.Equal(
            ["com.example.reader/.ReaderService", "io.github.toolicious.homeonfire/.HijackService"],
            components);
    }

    [Fact]
    public void ParseComponentList_returns_empty_for_an_unset_value()
    {
        Assert.Empty(AdbOutputParser.ParseComponentList("null"));
        Assert.Empty(AdbOutputParser.ParseComponentList(null));
        Assert.Empty(AdbOutputParser.ParseComponentList(":::"));
    }

    [Fact]
    public void IndicatesSecurityException_detects_the_fire_os_refusal()
    {
        Assert.True(AdbOutputParser.IndicatesSecurityException(MockAdbResponses.SecurityException));
        Assert.True(AdbOutputParser.IndicatesSecurityException("Permission Denial: attempt to change component state"));
        Assert.False(AdbOutputParser.IndicatesSecurityException(MockAdbResponses.DisableUserSuccess));
        Assert.False(AdbOutputParser.IndicatesSecurityException(null));
    }

    [Fact]
    public void IndicatesInstallSuccess_reads_the_pm_install_result()
    {
        Assert.True(AdbOutputParser.IndicatesInstallSuccess(MockAdbResponses.InstallSuccess));
        Assert.False(AdbOutputParser.IndicatesInstallSuccess(MockAdbResponses.InstallAbiFailure));
    }

    [Fact]
    public void ExtractInstallFailure_returns_the_failure_code()
    {
        var failure = AdbOutputParser.ExtractInstallFailure(MockAdbResponses.InstallAbiFailure);

        Assert.NotNull(failure);
        Assert.Contains("INSTALL_FAILED_NO_MATCHING_ABIS", failure);
        Assert.Null(AdbOutputParser.ExtractInstallFailure(MockAdbResponses.InstallSuccess));
    }

    [Fact]
    public void ParseNewStateReport_reads_the_state_pm_reported()
    {
        Assert.Equal(PackageState.Disabled, AdbOutputParser.ParseNewStateReport(MockAdbResponses.DisableUserSuccess));
        Assert.Equal(PackageState.Enabled, AdbOutputParser.ParseNewStateReport(MockAdbResponses.EnableSuccess));
        Assert.Null(AdbOutputParser.ParseNewStateReport("Unknown package: com.example"));
        Assert.Null(AdbOutputParser.ParseNewStateReport(null));
    }

    [Fact]
    public void SplitBatchOutput_separates_each_command_result()
    {
        var output = string.Join(
            System.Environment.NewLine,
            "0",
            AdbArguments.BatchSeparator,
            "package:com.amazon.bueller.photos",
            "package:com.amazon.tv.releasenotes",
            AdbArguments.BatchSeparator,
            "null");

        var sections = AdbService.SplitBatchOutput(output, 3);

        Assert.Equal(3, sections.Count);
        Assert.Equal("0", sections[0]);
        Assert.Contains("com.amazon.tv.releasenotes", sections[1]);
        Assert.Equal("null", sections[2]);
    }

    [Fact]
    public void SplitBatchOutput_pads_when_the_device_returned_fewer_sections()
    {
        var sections = AdbService.SplitBatchOutput("0", 3);

        Assert.Equal(3, sections.Count);
        Assert.Equal("0", sections[0]);
        Assert.Equal(string.Empty, sections[1]);
        Assert.Equal(string.Empty, sections[2]);
    }
}
