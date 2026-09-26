namespace FireReplace.Core.Catalog;

/// <summary>The <c>settings</c> namespace a key lives in.</summary>
public enum SettingScope
{
    /// <summary><c>settings get|put global</c>.</summary>
    Global,

    /// <summary><c>settings get|put secure</c>.</summary>
    Secure,

    /// <summary><c>settings get|put system</c>.</summary>
    System,
}

/// <summary>Confidence level attached to a setting definition.</summary>
public enum SettingConfidence
{
    /// <summary>Verified on the reference device as part of the tested core configuration.</summary>
    Verified,

    /// <summary>Plausible but not part of the verified configuration. Kept under Advanced.</summary>
    Experimental,
}

/// <summary>A single device setting FireReplace knows how to read and write.</summary>
/// <param name="Key">Settings key.</param>
/// <param name="Scope">Settings namespace.</param>
/// <param name="DisplayName">Short label for the UI.</param>
/// <param name="Description">What the setting does and why it is offered.</param>
/// <param name="AppliedValue">The value FireReplace writes when the toggle is switched on.</param>
/// <param name="DefaultValue">The value used to revert the setting.</param>
/// <param name="Confidence">Whether the setting is part of the verified configuration.</param>
/// <param name="OnLabel">Label to show when the setting currently equals <paramref name="AppliedValue"/>.</param>
/// <param name="OffLabel">Label to show otherwise.</param>
public sealed record SettingDefinition(
    string Key,
    SettingScope Scope,
    string DisplayName,
    string Description,
    string AppliedValue,
    string DefaultValue,
    SettingConfidence Confidence,
    string OnLabel = "On",
    string OffLabel = "Off")
{
    /// <summary>The <c>global</c>/<c>secure</c>/<c>system</c> token used on the command line.</summary>
    public string ScopeToken => Scope switch
    {
        SettingScope.Global => "global",
        SettingScope.Secure => "secure",
        SettingScope.System => "system",
        _ => throw new InvalidOperationException($"Unhandled scope {Scope}."),
    };
}

/// <summary>A named group of settings presented together in the UI.</summary>
/// <param name="Id">Stable identifier used in backups and settings.</param>
/// <param name="Title">Group heading.</param>
/// <param name="Description">Explanatory text shown above the group.</param>
/// <param name="Settings">Settings in the group.</param>
/// <param name="Confidence">Group-level confidence.</param>
public sealed record SettingGroup(
    string Id,
    string Title,
    string Description,
    IReadOnlyList<SettingDefinition> Settings,
    SettingConfidence Confidence);

/// <summary>
/// All settings FireReplace can change, split into verified groups and clearly separated
/// experimental groups. Nothing experimental is ever included in a recommended action.
/// </summary>
public static class SettingsCatalog
{
    /// <summary>Animation scales. Setting all three to 0 removes system transition delays.</summary>
    public static readonly SettingGroup Animations = new(
        "animations",
        "Animation speed",
        "Fire OS animates every transition. Setting all three scales to 0 makes the interface respond immediately.",
        [
            new("window_animation_scale", SettingScope.Global, "Window animation scale",
                "Scale applied to window open/close animations.", "0", "1", SettingConfidence.Verified, "Instant", "Default"),
            new("transition_animation_scale", SettingScope.Global, "Transition animation scale",
                "Scale applied to activity transition animations.", "0", "1", SettingConfidence.Verified, "Instant", "Default"),
            new("animator_duration_scale", SettingScope.Global, "Animator duration scale",
                "Scale applied to in-app animator durations.", "0", "1", SettingConfidence.Verified, "Instant", "Default"),
        ],
        SettingConfidence.Verified);

    /// <summary>The privacy/advertising settings verified on the reference device.</summary>
    public static readonly SettingGroup Privacy = new(
        "privacy",
        "Privacy and advertising",
        "These three settings were verified on the reference device. They reduce on-device metrics upload and ad personalisation. They do not stop all network traffic to Amazon.",
        [
            new("USAGE_METRICS_UPLOAD_ENABLED", SettingScope.Secure, "Usage metrics upload",
                "Controls whether collected usage metrics are uploaded from the device.", "0", "1", SettingConfidence.Verified, "Off", "On"),
            new("amz_limit_ad_tracking", SettingScope.Secure, "Ad tracking",
                "Amazon's limit-ad-tracking flag. When set, apps are asked not to use the advertising ID for tracking.", "1", "0", SettingConfidence.Verified, "Limited", "Allowed"),
            new("advertisingIdApp:interestBasedAds:value", SettingScope.Secure, "Interest-based advertising",
                "Disables interest-based ad personalisation tied to the device advertising ID.", "0", "1", SettingConfidence.Verified, "Off", "On"),
        ],
        SettingConfidence.Verified);

    /// <summary>Screen-saver behaviour. Not part of the verified configuration.</summary>
    public static readonly SettingGroup ScreenSaver = new(
        "screensaver",
        "Screen saver",
        "Stops Fire OS from starting its own screen saver. These keys were suggested during the original setup but were never confirmed on the reference device.",
        [
            new("screensaver_enabled", SettingScope.Secure, "Screen saver enabled",
                "Master switch for the system screen saver.", "0", "1", SettingConfidence.Experimental, "Off", "On"),
            new("screensaver_activate_on_sleep", SettingScope.Secure, "Activate on sleep",
                "Starts the screen saver when the device would sleep.", "0", "1", SettingConfidence.Experimental, "Off", "On"),
            new("screensaver_activate_on_dock", SettingScope.Secure, "Activate on dock",
                "Starts the screen saver when the device is docked.", "0", "1", SettingConfidence.Experimental, "Off", "On"),
        ],
        SettingConfidence.Experimental);

    /// <summary>Additional usage/marketing keys. Not part of the verified configuration.</summary>
    public static readonly SettingGroup UsageMarketing = new(
        "usage-marketing",
        "Additional usage and marketing data",
        "Additional keys related to usage collection and marketing. These were not part of the verified core configuration and may not exist on every Fire OS build.",
        [
            new("pact_usage_collection_enabled", SettingScope.Secure, "PACT usage collection",
                "Amazon usage collection flag.", "0", "1", SettingConfidence.Experimental, "Off", "On"),
            new("usage_metrics_marketing_enabled", SettingScope.Secure, "Marketing usage metrics",
                "Usage metrics collected for marketing purposes.", "0", "1", SettingConfidence.Experimental, "Off", "On"),
            new("settingsApp:appUsageData:value", SettingScope.Secure, "App usage data",
                "App usage data sharing toggle exposed by the Settings app.", "0", "1", SettingConfidence.Experimental, "Off", "On"),
            new("settingsApp:deviceUsageData:value", SettingScope.Secure, "Device usage data",
                "Device usage data sharing toggle exposed by the Settings app.", "0", "1", SettingConfidence.Experimental, "Off", "On"),
        ],
        SettingConfidence.Experimental);

    /// <summary>Settings keys read for status display but never written by FireReplace.</summary>
    public static readonly IReadOnlyList<SettingDefinition> ReadOnlyStatus =
    [
        new("enabled_accessibility_services", SettingScope.Secure, "Accessibility services",
            "The colon-separated list of enabled accessibility services.", string.Empty, string.Empty, SettingConfidence.Verified),
        new("accessibility_enabled", SettingScope.Secure, "Accessibility master switch",
            "Whether accessibility services are enabled at all.", "1", "0", SettingConfidence.Verified),
        new("enabled_notification_listeners", SettingScope.Secure, "Notification listeners",
            "The colon-separated list of enabled notification listener components.", string.Empty, string.Empty, SettingConfidence.Verified),
    ];

    /// <summary>Groups that make up the verified configuration.</summary>
    public static readonly IReadOnlyList<SettingGroup> VerifiedGroups = [Animations, Privacy];

    /// <summary>Groups that are experimental and kept out of recommended actions.</summary>
    public static readonly IReadOnlyList<SettingGroup> ExperimentalGroups = [ScreenSaver, UsageMarketing];

    /// <summary>Every group.</summary>
    public static readonly IReadOnlyList<SettingGroup> AllGroups =
        [Animations, Privacy, ScreenSaver, UsageMarketing];

    /// <summary>Every definition FireReplace reads, including read-only status keys.</summary>
    public static IEnumerable<SettingDefinition> AllReadableSettings =>
        AllGroups.SelectMany(g => g.Settings).Concat(ReadOnlyStatus);
}
