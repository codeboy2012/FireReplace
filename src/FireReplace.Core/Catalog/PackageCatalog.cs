using FireReplace.Core.Models;

namespace FireReplace.Core.Catalog;

/// <summary>An entry in the curated package catalog.</summary>
/// <param name="Name">Package name.</param>
/// <param name="Classification">Safety classification.</param>
/// <param name="Description">What the package appears to do, as observed on the reference device.</param>
public sealed record CatalogPackage(string Name, PackageClassification Classification, string Description);

/// <summary>
/// The curated package lists. These are the only packages FireReplace ever proposes in a bulk
/// action, and the core/Fire OS lists are hard guards that the UI cannot override.
/// </summary>
/// <remarks>
/// The verified list was tested on one Insignia Fire TV (AFTALMO / almond / Fire OS 7.01065.5585).
/// Other models can differ, which is why every package is shown with its live state before any change.
/// </remarks>
public static class PackageCatalog
{
    /// <summary>
    /// The 18 packages verified as safe to disable on the reference device.
    /// Order matches the original tested workflow.
    /// </summary>
    public static readonly IReadOnlyList<CatalogPackage> VerifiedDebloat =
    [
        new("com.amazon.bueller.photos", PackageClassification.VerifiedDebloat, "Amazon Photos screensaver and photo browsing."),
        new("com.amazon.shoptv.firetv.client", PackageClassification.VerifiedDebloat, "Shop-by-TV shopping client for Fire TV."),
        new("com.android.nfc", PackageClassification.VerifiedDebloat, "NFC stack. Fire TV hardware has no NFC radio."),
        new("com.amazon.perfc", PackageClassification.VerifiedDebloat, "Amazon performance data collection component."),
        new("com.amazon.tv.democontent.provider", PackageClassification.VerifiedDebloat, "Retail demo-mode content provider."),
        new("com.amazon.sneakpeek", PackageClassification.VerifiedDebloat, "Promotional 'sneak peek' content surface."),
        new("com.amazon.storm.lightning.tutorial", PackageClassification.VerifiedDebloat, "Remote-control tutorial overlay."),
        new("com.amazon.wirelessmetrics.service", PackageClassification.VerifiedDebloat, "Wireless metrics reporting service."),
        new("com.amazon.tmm.tutorial", PackageClassification.VerifiedDebloat, "First-run tutorial ('teach me more')."),
        new("com.amazon.audiohome", PackageClassification.VerifiedDebloat, "Audio home experience component."),
        new("com.amazon.tv.support", PackageClassification.VerifiedDebloat, "Amazon device support / help app."),
        new("com.amazon.kso.blackbird", PackageClassification.VerifiedDebloat, "Blackbird promotional content framework."),
        new("com.amazon.shoptv.client", PackageClassification.VerifiedDebloat, "Second Shop-by-TV client component."),
        new("com.amazon.tv.releasenotes", PackageClassification.VerifiedDebloat, "Software release-notes viewer."),
        new("com.amazon.gamehub", PackageClassification.VerifiedDebloat, "Game hub storefront surface."),
        new("com.amazon.perfcollection", PackageClassification.VerifiedDebloat, "Performance metrics collection."),
        new("com.amazon.bueller.music", PackageClassification.VerifiedDebloat, "Amazon Music integration for the stock launcher."),
        new("com.amazon.media.recommendations", PackageClassification.VerifiedDebloat, "Home-screen media recommendation rows."),
    ];

    /// <summary>
    /// Packages FireReplace treats as core. They are never offered by bulk actions and are
    /// blocked from individual disable operations as well.
    /// </summary>
    public static readonly IReadOnlyList<CatalogPackage> CoreProtected =
    [
        new("com.amazon.tv.launcher", PackageClassification.CoreProtected, "The stock Fire TV launcher. Disabling it can leave the TV with no usable home screen."),
        new("com.amazon.airplaydaemon", PackageClassification.CoreProtected, "AirPlay receiver."),
        new("com.amazon.connectivitycontroller", PackageClassification.CoreProtected, "Network connectivity management."),
        new("com.amazon.device.messaging", PackageClassification.CoreProtected, "Amazon Device Messaging, used for push delivery by many apps."),
        new("com.amazon.whisperlink.core.android", PackageClassification.CoreProtected, "Whisperlink device-to-device framework."),
        new("com.amazon.whisperjoin.middleware.np", PackageClassification.CoreProtected, "Whisperjoin provisioning middleware."),
        new("com.amazon.firehomestarter", PackageClassification.CoreProtected, "Home screen startup handler."),
        new("com.amazon.device.software.ota", PackageClassification.CoreProtected, "Over-the-air software updates."),
        new("com.amazon.vizzini", PackageClassification.CoreProtected, "Alexa voice service component."),
        new("com.amazon.aria", PackageClassification.CoreProtected, "Amazon metrics/identity framework relied on by system services."),
        new("com.amazon.hedwig", PackageClassification.CoreProtected, "System notification delivery."),
        new("com.amazon.dp.logger", PackageClassification.CoreProtected, "Platform logging component used by system services."),
    ];

    /// <summary>
    /// Packages that Fire OS protects at the platform level. Attempts to change them return a
    /// SecurityException. FireReplace shows them and explains the refusal; it never tries to bypass it.
    /// </summary>
    public static readonly IReadOnlyList<CatalogPackage> FireOsProtected =
    [
        new("com.amazon.ftvads.deeplinking", PackageClassification.FireOsProtected, "Fire TV ad deep-linking. Fire OS blocks changes to this package."),
        new("com.amazon.client.metrics", PackageClassification.FireOsProtected, "Amazon client metrics. Fire OS blocks changes to this package."),
        new("com.amazon.device.metrics", PackageClassification.FireOsProtected, "Amazon device metrics. Fire OS blocks changes to this package."),
    ];

    private static readonly Dictionary<string, CatalogPackage> Index = BuildIndex();

    /// <summary>Every catalog entry, in list order.</summary>
    public static IEnumerable<CatalogPackage> All =>
        VerifiedDebloat.Concat(CoreProtected).Concat(FireOsProtected);

    /// <summary>Package names on the verified debloat list.</summary>
    public static IReadOnlyList<string> VerifiedDebloatNames { get; } =
        VerifiedDebloat.Select(p => p.Name).ToArray();

    /// <summary>Looks up a catalog entry, or null when the package is not curated.</summary>
    public static CatalogPackage? Find(string packageName) =>
        Index.TryGetValue(packageName, out var entry) ? entry : null;

    /// <summary>Classifies any package name, defaulting to <see cref="PackageClassification.Other"/>.</summary>
    public static PackageClassification Classify(string packageName) =>
        Find(packageName)?.Classification ?? PackageClassification.Other;

    /// <summary>
    /// True when FireReplace must refuse to disable the package. Core and Fire OS protected
    /// packages are guarded; nothing else is guarded purely because of how its name reads.
    /// </summary>
    public static bool IsGuarded(string packageName) =>
        Classify(packageName) is PackageClassification.CoreProtected or PackageClassification.FireOsProtected;

    /// <summary>Known description for a package, or null.</summary>
    public static string? DescriptionFor(string packageName) => Find(packageName)?.Description;

    private static Dictionary<string, CatalogPackage> BuildIndex()
    {
        var index = new Dictionary<string, CatalogPackage>(StringComparer.Ordinal);
        foreach (var entry in VerifiedDebloat.Concat(CoreProtected).Concat(FireOsProtected))
        {
            index[entry.Name] = entry;
        }

        return index;
    }
}
