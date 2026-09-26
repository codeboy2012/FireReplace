namespace FireReplace.Core.Configuration;

/// <summary>
/// Resolves the folders FireReplace writes to. Configuration lives in the user's roaming profile;
/// backups and exported logs live next to the executable so they travel with a portable copy.
/// </summary>
public sealed class AppPaths
{
    /// <summary>Creates paths rooted at the application directory.</summary>
    /// <param name="applicationDirectory">Directory containing the executable.</param>
    public AppPaths(string applicationDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(applicationDirectory);
        ApplicationDirectory = applicationDirectory;

        ConfigDirectory = Path.Combine(
            System.Environment.GetFolderPath(System.Environment.SpecialFolder.ApplicationData),
            "FireReplace");

        ConfigFile = Path.Combine(ConfigDirectory, "settings.json");
        BackupDirectory = Path.Combine(applicationDirectory, "backups");
        LogDirectory = Path.Combine(applicationDirectory, "logs");
    }

    /// <summary>Directory containing FireReplace.exe.</summary>
    public string ApplicationDirectory { get; }

    /// <summary>Roaming configuration directory.</summary>
    public string ConfigDirectory { get; }

    /// <summary>Path of settings.json.</summary>
    public string ConfigFile { get; }

    /// <summary>Root of the local backup store.</summary>
    public string BackupDirectory { get; }

    /// <summary>Root for exported and auto-saved logs.</summary>
    public string LogDirectory { get; }

    /// <summary>Default file name for an automatically saved log.</summary>
    public string AutoLogFile => Path.Combine(LogDirectory, $"firereplace-{DateTime.Now:yyyy-MM-dd}.log");

    /// <summary>Creates the writable directories, ignoring failures on read-only media.</summary>
    public void EnsureDirectories()
    {
        foreach (var directory in new[] { ConfigDirectory, BackupDirectory, LogDirectory })
        {
            try
            {
                Directory.CreateDirectory(directory);
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException)
            {
                // A portable copy on read-only media still works; only backups and logs are affected.
            }
        }
    }
}
