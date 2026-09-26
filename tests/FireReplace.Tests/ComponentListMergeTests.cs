using FireReplace.Core.Catalog;
using FireReplace.Core.Services;
using FireReplace.Tests.Fakes;
using Xunit;

namespace FireReplace.Tests;

/// <summary>
/// The accessibility list is the one place where a careless write would silently break the Home
/// button redirect, so the merge behaviour is pinned down here in detail.
/// </summary>
public class ComponentListMergeTests
{
    private const string HomeOnFire = "io.github.toolicious.homeonfire/.HijackService";
    private const string Projectivy = "com.spocky.projengmenu/.services.ProjectivyAccessibilityService";

    [Fact]
    public void Adding_projectivy_preserves_home_on_fire()
    {
        var merge = ComponentListMerge.Add(MockAdbResponses.AccessibilityWithHomeOnFire, [HomeOnFire, Projectivy]);

        Assert.Equal([HomeOnFire], merge.Current);
        Assert.Equal([HomeOnFire, Projectivy], merge.Proposed);
        Assert.Equal([Projectivy], merge.Added);
        Assert.True(merge.HasChanges);
        Assert.Equal($"{HomeOnFire}:{Projectivy}", merge.ProposedValue);
    }

    [Fact]
    public void An_unrelated_third_party_service_is_never_dropped()
    {
        var merge = ComponentListMerge.Add(MockAdbResponses.AccessibilityWithThirdParty, [HomeOnFire, Projectivy]);

        Assert.Contains("com.example.reader/.ReaderService", merge.Proposed);
        Assert.Equal(3, merge.Proposed.Count);
        Assert.Equal([Projectivy], merge.Added);
    }

    [Fact]
    public void Existing_order_is_preserved()
    {
        var merge = ComponentListMerge.Add($"{Projectivy}:{HomeOnFire}", [HomeOnFire, Projectivy]);

        Assert.Equal([Projectivy, HomeOnFire], merge.Proposed);
        Assert.Empty(merge.Added);
        Assert.False(merge.HasChanges);
    }

    [Fact]
    public void Adding_to_an_empty_list_produces_both_services()
    {
        var merge = ComponentListMerge.Add(null, [HomeOnFire, Projectivy]);

        Assert.Empty(merge.Current);
        Assert.Equal([HomeOnFire, Projectivy], merge.Proposed);
        Assert.Equal(2, merge.Added.Count);
    }

    [Fact]
    public void The_literal_string_null_is_treated_as_an_empty_list()
    {
        var merge = ComponentListMerge.Add("null", [Projectivy]);

        Assert.Empty(merge.Current);
        Assert.Equal([Projectivy], merge.Proposed);
    }

    [Fact]
    public void Nothing_is_added_twice()
    {
        var merge = ComponentListMerge.Add($"{HomeOnFire}:{Projectivy}", [HomeOnFire, Projectivy]);

        Assert.Empty(merge.Added);
        Assert.False(merge.HasChanges);
        Assert.Equal(merge.CurrentValue, merge.ProposedValue);
    }

    [Fact]
    public void A_malformed_component_is_rejected_rather_than_written()
    {
        var merge = ComponentListMerge.Add(MockAdbResponses.AccessibilityWithHomeOnFire,
            ["com.evil/.Service; reboot", Projectivy]);

        Assert.Single(merge.Rejected);
        Assert.Equal([Projectivy], merge.Added);
        Assert.DoesNotContain(merge.Proposed, c => c.Contains("reboot", StringComparison.Ordinal));
    }

    [Fact]
    public void Remove_takes_out_only_the_requested_component()
    {
        var merge = ComponentListMerge.Remove($"{HomeOnFire}:{Projectivy}", [Projectivy]);

        Assert.Equal([HomeOnFire], merge.Proposed);
        Assert.Equal([Projectivy], merge.Added);
    }

    [Fact]
    public void Remove_is_a_no_op_when_the_component_is_absent()
    {
        var merge = ComponentListMerge.Remove(MockAdbResponses.AccessibilityWithHomeOnFire, [Projectivy]);

        Assert.Equal([HomeOnFire], merge.Proposed);
        Assert.Empty(merge.Added);
    }

    [Fact]
    public void The_catalog_exposes_both_known_accessibility_services() =>
        Assert.Equal([HomeOnFire, Projectivy], LauncherCatalog.KnownAccessibilityServices);

    [Fact]
    public void FriendlyComponentName_maps_known_packages_and_passes_others_through()
    {
        Assert.Equal("Home on Fire", LauncherCatalog.FriendlyComponentName(HomeOnFire));
        Assert.Equal("Projectivy Launcher", LauncherCatalog.FriendlyComponentName(Projectivy));
        Assert.Equal("com.example.reader/.ReaderService",
            LauncherCatalog.FriendlyComponentName("com.example.reader/.ReaderService"));
    }
}
