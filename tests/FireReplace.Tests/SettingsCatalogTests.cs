using FireReplace.Core.Catalog;
using FireReplace.Core.Services;
using FireReplace.Core.Validation;
using Xunit;

namespace FireReplace.Tests;

public class SettingsCatalogTests
{
    [Fact]
    public void The_animation_group_holds_the_three_tested_scales()
    {
        Assert.Equal(
            ["window_animation_scale", "transition_animation_scale", "animator_duration_scale"],
            SettingsCatalog.Animations.Settings.Select(s => s.Key));

        Assert.All(SettingsCatalog.Animations.Settings, s =>
        {
            Assert.Equal(SettingScope.Global, s.Scope);
            Assert.Equal("0", s.AppliedValue);
            Assert.Equal(SettingConfidence.Verified, s.Confidence);
        });
    }

    [Fact]
    public void The_privacy_group_holds_the_three_tested_keys_with_their_tested_values()
    {
        var byKey = SettingsCatalog.Privacy.Settings.ToDictionary(s => s.Key, s => s);

        Assert.Equal("0", byKey["USAGE_METRICS_UPLOAD_ENABLED"].AppliedValue);
        Assert.Equal("1", byKey["amz_limit_ad_tracking"].AppliedValue);
        Assert.Equal("0", byKey["advertisingIdApp:interestBasedAds:value"].AppliedValue);

        Assert.All(SettingsCatalog.Privacy.Settings, s => Assert.Equal(SettingScope.Secure, s.Scope));
    }

    [Fact]
    public void The_screen_saver_group_is_experimental()
    {
        Assert.Equal(SettingConfidence.Experimental, SettingsCatalog.ScreenSaver.Confidence);
        Assert.Equal(
            ["screensaver_enabled", "screensaver_activate_on_sleep", "screensaver_activate_on_dock"],
            SettingsCatalog.ScreenSaver.Settings.Select(s => s.Key));
        Assert.All(SettingsCatalog.ScreenSaver.Settings, s => Assert.Equal(SettingConfidence.Experimental, s.Confidence));
    }

    [Fact]
    public void The_usage_marketing_group_is_experimental()
    {
        Assert.Equal(SettingConfidence.Experimental, SettingsCatalog.UsageMarketing.Confidence);
        Assert.Equal(
            [
                "pact_usage_collection_enabled",
                "usage_metrics_marketing_enabled",
                "settingsApp:appUsageData:value",
                "settingsApp:deviceUsageData:value",
            ],
            SettingsCatalog.UsageMarketing.Settings.Select(s => s.Key));
    }

    [Fact]
    public void Verified_and_experimental_groups_never_overlap()
    {
        var verified = SettingsCatalog.VerifiedGroups.Select(g => g.Id).ToHashSet(StringComparer.Ordinal);
        var experimental = SettingsCatalog.ExperimentalGroups.Select(g => g.Id).ToHashSet(StringComparer.Ordinal);

        Assert.Empty(verified.Intersect(experimental, StringComparer.Ordinal));
        Assert.Equal(["animations", "privacy"], verified);
        Assert.Equal(["screensaver", "usage-marketing"], experimental);
    }

    [Fact]
    public void Every_verified_group_setting_is_marked_verified() =>
        Assert.All(
            SettingsCatalog.VerifiedGroups.SelectMany(g => g.Settings),
            s => Assert.Equal(SettingConfidence.Verified, s.Confidence));

    [Fact]
    public void Every_readable_key_passes_validation() =>
        Assert.All(SettingsCatalog.AllReadableSettings, s => Assert.True(Validate.IsSettingKey(s.Key), s.Key));

    [Fact]
    public void Every_readable_key_can_be_batched_safely()
    {
        // The status refresh sends all of these in one shell line, so each token must be safe to embed.
        Assert.All(SettingsCatalog.AllReadableSettings, s =>
        {
            Assert.True(Validate.IsShellSafeToken(s.Key), s.Key);
            Assert.True(Validate.IsShellSafeToken(s.ScopeToken), s.ScopeToken);
        });
    }

    [Fact]
    public void Scope_tokens_match_the_settings_command_names()
    {
        Assert.Equal("global", SettingsCatalog.Animations.Settings[0].ScopeToken);
        Assert.Equal("secure", SettingsCatalog.Privacy.Settings[0].ScopeToken);
    }

    [Fact]
    public void The_readable_set_includes_the_accessibility_and_listener_keys()
    {
        var keys = SettingsCatalog.AllReadableSettings.Select(s => s.Key).ToArray();

        Assert.Contains("enabled_accessibility_services", keys);
        Assert.Contains("enabled_notification_listeners", keys);
        Assert.Contains("accessibility_enabled", keys);
    }

    [Fact]
    public void Readable_keys_are_unique_after_scope_qualification()
    {
        var keys = DeviceStateService.ReadableSettings.Select(DeviceState.KeyOf).ToArray();
        Assert.Equal(keys.Length, keys.Distinct(StringComparer.Ordinal).Count());
    }

    [Fact]
    public void KeyOf_qualifies_with_the_scope()
    {
        Assert.Equal("global/window_animation_scale", DeviceState.KeyOf(SettingScope.Global, "window_animation_scale"));
        Assert.Equal("secure/amz_limit_ad_tracking", DeviceState.KeyOf(SettingScope.Secure, "amz_limit_ad_tracking"));
        Assert.Equal("system/anything", DeviceState.KeyOf(SettingScope.System, "anything"));
    }
}
