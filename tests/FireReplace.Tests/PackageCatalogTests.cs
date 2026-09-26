using FireReplace.Core.Catalog;
using FireReplace.Core.Models;
using Xunit;

namespace FireReplace.Tests;

public class PackageCatalogTests
{
    [Fact]
    public void The_verified_list_holds_the_eighteen_tested_packages()
    {
        Assert.Equal(18, PackageCatalog.VerifiedDebloat.Count);
        Assert.Equal(18, PackageCatalog.VerifiedDebloatNames.Distinct(StringComparer.Ordinal).Count());
    }

    [Fact]
    public void The_verified_list_matches_the_tested_workflow_exactly()
    {
        string[] expected =
        [
            "com.amazon.bueller.photos",
            "com.amazon.shoptv.firetv.client",
            "com.android.nfc",
            "com.amazon.perfc",
            "com.amazon.tv.democontent.provider",
            "com.amazon.sneakpeek",
            "com.amazon.storm.lightning.tutorial",
            "com.amazon.wirelessmetrics.service",
            "com.amazon.tmm.tutorial",
            "com.amazon.audiohome",
            "com.amazon.tv.support",
            "com.amazon.kso.blackbird",
            "com.amazon.shoptv.client",
            "com.amazon.tv.releasenotes",
            "com.amazon.gamehub",
            "com.amazon.perfcollection",
            "com.amazon.bueller.music",
            "com.amazon.media.recommendations",
        ];

        Assert.Equal(expected, PackageCatalog.VerifiedDebloatNames);
    }

    [Fact]
    public void The_core_list_holds_the_twelve_preserved_packages()
    {
        string[] expected =
        [
            "com.amazon.tv.launcher",
            "com.amazon.airplaydaemon",
            "com.amazon.connectivitycontroller",
            "com.amazon.device.messaging",
            "com.amazon.whisperlink.core.android",
            "com.amazon.whisperjoin.middleware.np",
            "com.amazon.firehomestarter",
            "com.amazon.device.software.ota",
            "com.amazon.vizzini",
            "com.amazon.aria",
            "com.amazon.hedwig",
            "com.amazon.dp.logger",
        ];

        Assert.Equal(expected, PackageCatalog.CoreProtected.Select(p => p.Name));
    }

    [Fact]
    public void The_fire_os_protected_list_holds_the_three_observed_packages()
    {
        string[] expected =
        [
            "com.amazon.ftvads.deeplinking",
            "com.amazon.client.metrics",
            "com.amazon.device.metrics",
        ];

        Assert.Equal(expected, PackageCatalog.FireOsProtected.Select(p => p.Name));
    }

    [Fact]
    public void No_package_appears_in_more_than_one_list()
    {
        var all = PackageCatalog.All.Select(p => p.Name).ToArray();
        Assert.Equal(all.Length, all.Distinct(StringComparer.Ordinal).Count());
    }

    [Fact]
    public void Every_catalog_entry_carries_a_description() =>
        Assert.All(PackageCatalog.All, entry => Assert.False(string.IsNullOrWhiteSpace(entry.Description)));

    [Theory]
    [InlineData("com.amazon.tv.launcher")]
    [InlineData("com.amazon.airplaydaemon")]
    [InlineData("com.amazon.device.software.ota")]
    [InlineData("com.amazon.client.metrics")]
    [InlineData("com.amazon.device.metrics")]
    [InlineData("com.amazon.ftvads.deeplinking")]
    public void Guarded_packages_are_protected(string packageName) =>
        Assert.True(PackageCatalog.IsGuarded(packageName));

    [Theory]
    [InlineData("com.amazon.gamehub")]
    [InlineData("com.android.nfc")]
    [InlineData("com.amazon.media.recommendations")]
    [InlineData("com.example.unknown")]
    public void Non_core_packages_are_not_guarded(string packageName) =>
        Assert.False(PackageCatalog.IsGuarded(packageName));

    [Theory]
    [InlineData("com.example.advertising")]
    [InlineData("com.example.metrics")]
    [InlineData("com.example.recommendations")]
    [InlineData("com.vendor.ads.service")]
    public void A_suggestive_name_alone_never_changes_the_classification(string packageName)
    {
        // Guarding must come from the curated lists, never from pattern matching on the name.
        Assert.Equal(PackageClassification.Other, PackageCatalog.Classify(packageName));
        Assert.False(PackageCatalog.IsGuarded(packageName));
        Assert.Null(PackageCatalog.DescriptionFor(packageName));
    }

    [Fact]
    public void Classify_returns_the_list_a_package_belongs_to()
    {
        Assert.Equal(PackageClassification.VerifiedDebloat, PackageCatalog.Classify("com.amazon.gamehub"));
        Assert.Equal(PackageClassification.CoreProtected, PackageCatalog.Classify("com.amazon.tv.launcher"));
        Assert.Equal(PackageClassification.FireOsProtected, PackageCatalog.Classify("com.amazon.client.metrics"));
        Assert.Equal(PackageClassification.Other, PackageCatalog.Classify("com.netflix.ninja"));
    }

    [Fact]
    public void Guard_labels_are_surfaced_for_protected_packages()
    {
        var core = new PackageInfo { Name = "com.amazon.tv.launcher", Classification = PackageClassification.CoreProtected };
        var fireOs = new PackageInfo { Name = "com.amazon.client.metrics", Classification = PackageClassification.FireOsProtected };
        var ordinary = new PackageInfo { Name = "com.amazon.gamehub", Classification = PackageClassification.VerifiedDebloat };

        Assert.Equal("CORE — PROTECTED", core.GuardLabel);
        Assert.Equal("FIRE OS — PROTECTED", fireOs.GuardLabel);
        Assert.Null(ordinary.GuardLabel);
        Assert.True(core.IsGuarded);
        Assert.True(fireOs.IsGuarded);
        Assert.False(ordinary.IsGuarded);
    }

    [Fact]
    public void Every_catalog_package_name_is_a_valid_android_package_name() =>
        Assert.All(PackageCatalog.All, entry =>
            Assert.True(Core.Validation.Validate.IsPackageName(entry.Name), entry.Name));
}
