namespace FireReplace.Core.Models;

/// <summary>
/// Identity information read from the connected device. Every field comes from the device's own
/// <c>getprop</c> output; nothing is assumed about the model.
/// </summary>
public sealed record DeviceInfo
{
    /// <summary>ADB serial of the device, typically <c>ip:5555</c> for network devices.</summary>
    public string Serial { get; init; } = string.Empty;

    /// <summary><c>ro.product.model</c>, for example <c>AFTALMO</c>.</summary>
    public string? Model { get; init; }

    /// <summary><c>ro.build.product</c>, for example <c>almond</c>.</summary>
    public string? Product { get; init; }

    /// <summary><c>ro.product.manufacturer</c>.</summary>
    public string? Manufacturer { get; init; }

    /// <summary><c>ro.product.name</c>.</summary>
    public string? ProductName { get; init; }

    /// <summary><c>ro.build.version.release</c>, the Android release number.</summary>
    public string? AndroidRelease { get; init; }

    /// <summary><c>ro.build.version.sdk</c>.</summary>
    public string? SdkLevel { get; init; }

    /// <summary><c>ro.build.version.incremental</c>, the build number.</summary>
    public string? BuildIncremental { get; init; }

    /// <summary><c>ro.build.version.name</c> — the Fire OS version string on Amazon devices.</summary>
    public string? FireOsVersion { get; init; }

    /// <summary><c>ro.product.cpu.abi</c>.</summary>
    public string? Abi { get; init; }

    /// <summary>A friendly marketing-ish name assembled from manufacturer/brand information.</summary>
    public string? FriendlyName { get; init; }

    /// <summary>When the snapshot was taken, so the UI can show cache age.</summary>
    public DateTimeOffset ReadAt { get; init; } = DateTimeOffset.Now;

    /// <summary>Best available display name, never empty.</summary>
    public string DisplayName =>
        !string.IsNullOrWhiteSpace(FriendlyName) ? FriendlyName!
        : !string.IsNullOrWhiteSpace(Model) ? Model!
        : !string.IsNullOrWhiteSpace(Serial) ? Serial
        : "Unknown device";
}
