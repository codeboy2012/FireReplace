namespace FireReplace.Core.Models;

/// <summary>Installed/enabled state of a package on the device.</summary>
public enum PackageState
{
    /// <summary>The package was not reported by the device at all.</summary>
    NotInstalled = 0,

    /// <summary>Installed and enabled.</summary>
    Enabled,

    /// <summary>Installed but disabled (either <c>disabled</c> or <c>disabled-user</c>).</summary>
    Disabled,

    /// <summary>State could not be determined, for example because the read failed.</summary>
    Unknown,
}

/// <summary>How FireReplace classifies a package for safety purposes.</summary>
public enum PackageClassification
{
    /// <summary>Not in any curated list. Shown, but never bulk-modified.</summary>
    Other = 0,

    /// <summary>On the verified debloat list that was tested on the reference device.</summary>
    VerifiedDebloat,

    /// <summary>On the core list. Never offered by bulk actions and guarded individually.</summary>
    CoreProtected,

    /// <summary>Known to be protected by Fire OS itself; modification attempts return SecurityException.</summary>
    FireOsProtected,
}

/// <summary>A package plus everything the UI needs to show and guard it.</summary>
public sealed record PackageInfo
{
    /// <summary>Android package name.</summary>
    public required string Name { get; init; }

    /// <summary>Current state on the device.</summary>
    public PackageState State { get; init; } = PackageState.Unknown;

    /// <summary>Safety classification from the curated catalog.</summary>
    public PackageClassification Classification { get; init; } = PackageClassification.Other;

    /// <summary>Short human explanation, when the package is in the catalog.</summary>
    public string? Description { get; init; }

    /// <summary>True when FireReplace refuses to disable this package.</summary>
    public bool IsGuarded =>
        Classification is PackageClassification.CoreProtected or PackageClassification.FireOsProtected;

    /// <summary>Label shown next to guarded packages.</summary>
    public string? GuardLabel => Classification switch
    {
        PackageClassification.CoreProtected => "CORE — PROTECTED",
        PackageClassification.FireOsProtected => "FIRE OS — PROTECTED",
        _ => null,
    };
}
