namespace FireReplace.Core.Discovery;

/// <summary>
/// Minimal file-system surface used by the toolchain locator so that discovery logic
/// can be unit tested without touching a real disk.
/// </summary>
public interface IFileProbe
{
    /// <summary>Returns true when a file exists at <paramref name="path"/>.</summary>
    bool FileExists(string path);

    /// <summary>Returns true when a directory exists at <paramref name="path"/>.</summary>
    bool DirectoryExists(string path);

    /// <summary>Enumerates files in <paramref name="directory"/> matching <paramref name="searchPattern"/>.</summary>
    IReadOnlyList<string> EnumerateFiles(string directory, string searchPattern);
}

/// <summary>Default <see cref="IFileProbe"/> backed by <see cref="System.IO"/>.</summary>
public sealed class FileProbe : IFileProbe
{
    /// <summary>A shared instance; the type is stateless.</summary>
    public static readonly FileProbe Instance = new();

    /// <inheritdoc />
    public bool FileExists(string path) => File.Exists(path);

    /// <inheritdoc />
    public bool DirectoryExists(string path) => Directory.Exists(path);

    /// <inheritdoc />
    public IReadOnlyList<string> EnumerateFiles(string directory, string searchPattern)
    {
        if (!Directory.Exists(directory))
        {
            return Array.Empty<string>();
        }

        try
        {
            var files = Directory.GetFiles(directory, searchPattern, SearchOption.TopDirectoryOnly);
            Array.Sort(files, StringComparer.OrdinalIgnoreCase);
            return files;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            return Array.Empty<string>();
        }
    }
}
