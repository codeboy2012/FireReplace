namespace FireReplace.Core.Discovery;

/// <summary>Where the platform-tools copy of adb.exe was found, if anywhere.</summary>
/// <param name="AdbPath">Full path to adb.exe, or null when it could not be located.</param>
/// <param name="PlatformToolsDirectory">Directory that contained adb.exe, or null.</param>
/// <param name="MissingSupportLibraries">
/// Windows USB helper DLLs that ship with platform-tools but were not found next to adb.exe.
/// adb refuses to start on Windows without AdbWinApi.dll.
/// </param>
/// <param name="SearchedLocations">Every candidate path that was probed, for the setup screen.</param>
public sealed record AdbLocation(
    string? AdbPath,
    string? PlatformToolsDirectory,
    IReadOnlyList<string> MissingSupportLibraries,
    IReadOnlyList<string> SearchedLocations)
{
    /// <summary>True when adb.exe was found.</summary>
    public bool Found => AdbPath is not null;

    /// <summary>True when adb.exe was found and its Windows support DLLs are present.</summary>
    public bool Complete => Found && MissingSupportLibraries.Count == 0;
}

/// <summary>One APK file that FireReplace knows how to install.</summary>
/// <param name="DisplayName">Human readable name shown in the UI.</param>
/// <param name="FileNamePattern">Glob used to find the file inside the APK directory.</param>
/// <param name="PackageName">Package the APK is expected to install.</param>
/// <param name="Path">Resolved full path, or null when the file is missing.</param>
public sealed record ApkLocation(string DisplayName, string FileNamePattern, string PackageName, string? Path)
{
    /// <summary>True when the APK file was found on disk.</summary>
    public bool Found => Path is not null;

    /// <summary>File name of the resolved APK, or null.</summary>
    public string? FileName => Path is null ? null : System.IO.Path.GetFileName(Path);
}

/// <summary>
/// Locates adb.exe and the bundled APKs <em>relative to the running executable</em>.
/// No absolute paths are hard coded anywhere. During development the locator also walks a
/// small number of parent directories so the repository layout works without copying binaries
/// into every build output folder.
/// </summary>
public sealed class ToolchainLocator
{
    private const int MaxParentWalk = 6;

    private static readonly string[] SupportLibraries = ["AdbWinApi.dll", "AdbWinUsbApi.dll"];

    private readonly IFileProbe _probe;
    private readonly string _baseDirectory;

    /// <summary>Creates a locator rooted at <paramref name="baseDirectory"/>.</summary>
    public ToolchainLocator(string baseDirectory, IFileProbe? probe = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(baseDirectory);
        _baseDirectory = baseDirectory;
        _probe = probe ?? FileProbe.Instance;
    }

    /// <summary>Candidate roots, nearest first: the app directory then its ancestors.</summary>
    public IReadOnlyList<string> CandidateRoots()
    {
        var roots = new List<string> { _baseDirectory };
        var current = _baseDirectory;

        for (var i = 0; i < MaxParentWalk; i++)
        {
            var parent = Path.GetDirectoryName(current.TrimEnd(Path.DirectorySeparatorChar));
            if (string.IsNullOrEmpty(parent) || parent == current)
            {
                break;
            }

            roots.Add(parent);
            current = parent;
        }

        return roots;
    }

    /// <summary>
    /// Finds adb.exe. <paramref name="configuredDirectory"/> (from Settings) wins when set.
    /// </summary>
    public AdbLocation LocateAdb(string? configuredDirectory = null)
    {
        var searched = new List<string>();

        if (!string.IsNullOrWhiteSpace(configuredDirectory))
        {
            var configured = Path.Combine(configuredDirectory, "adb.exe");
            searched.Add(configured);
            if (_probe.FileExists(configured))
            {
                return Build(configured, searched);
            }
        }

        foreach (var root in CandidateRoots())
        {
            var candidate = Path.Combine(root, "platform-tools", "adb.exe");
            searched.Add(candidate);
            if (_probe.FileExists(candidate))
            {
                return Build(candidate, searched);
            }
        }

        // Last resort: adb.exe sitting directly beside the application (the old script layout).
        var beside = Path.Combine(_baseDirectory, "adb.exe");
        searched.Add(beside);
        if (_probe.FileExists(beside))
        {
            return Build(beside, searched);
        }

        return new AdbLocation(null, null, Array.Empty<string>(), searched);
    }

    /// <summary>
    /// Finds the APK directory: <c>./apks</c> beside the application, or in an ancestor during development.
    /// </summary>
    public string? LocateApkDirectory(string? configuredDirectory = null)
    {
        if (!string.IsNullOrWhiteSpace(configuredDirectory) && _probe.DirectoryExists(configuredDirectory))
        {
            return configuredDirectory;
        }

        foreach (var root in CandidateRoots())
        {
            var candidate = Path.Combine(root, "apks");
            if (_probe.DirectoryExists(candidate))
            {
                return candidate;
            }
        }

        return null;
    }

    /// <summary>
    /// Resolves the known APK definitions against <paramref name="apkDirectory"/>.
    /// Only files inside that directory are ever considered.
    /// </summary>
    public IReadOnlyList<ApkLocation> LocateApks(string? apkDirectory, IEnumerable<ApkLocation> definitions)
    {
        var result = new List<ApkLocation>();

        foreach (var definition in definitions)
        {
            if (apkDirectory is null)
            {
                result.Add(definition with { Path = null });
                continue;
            }

            var matches = _probe.EnumerateFiles(apkDirectory, definition.FileNamePattern);
            result.Add(definition with { Path = matches.Count > 0 ? matches[0] : null });
        }

        return result;
    }

    private AdbLocation Build(string adbPath, List<string> searched)
    {
        var directory = Path.GetDirectoryName(adbPath)!;
        var missing = SupportLibraries
            .Where(library => !_probe.FileExists(Path.Combine(directory, library)))
            .ToArray();

        return new AdbLocation(adbPath, directory, missing, searched);
    }
}
