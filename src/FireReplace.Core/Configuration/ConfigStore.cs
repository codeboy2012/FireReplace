using System.Text.Json;
using System.Text.Json.Serialization;

namespace FireReplace.Core.Configuration;

/// <summary>
/// Loads and saves <see cref="AppConfig"/>. A corrupt or unreadable file never blocks startup:
/// defaults are used instead and the problem is reported to the caller.
/// </summary>
public sealed class ConfigStore
{
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.Never,
        Converters = { new JsonStringEnumConverter() },
    };

    private readonly string _path;

    /// <summary>Creates a store for the given file path.</summary>
    public ConfigStore(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        _path = path;
    }

    /// <summary>Path of the backing file.</summary>
    public string Path => _path;

    /// <summary>The last load error, when the file could not be read.</summary>
    public string? LastError { get; private set; }

    /// <summary>Reads the configuration, returning defaults when the file is missing or invalid.</summary>
    public AppConfig Load()
    {
        LastError = null;

        try
        {
            if (!File.Exists(_path))
            {
                return new AppConfig();
            }

            var json = File.ReadAllText(_path);
            return JsonSerializer.Deserialize<AppConfig>(json, Options) ?? new AppConfig();
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or JsonException)
        {
            LastError = e.Message;
            return new AppConfig();
        }
    }

    /// <summary>Writes the configuration, creating the directory when needed.</summary>
    public async Task SaveAsync(AppConfig config, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(config);

        var directory = System.IO.Path.GetDirectoryName(System.IO.Path.GetFullPath(_path));
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var json = JsonSerializer.Serialize(config, Options);

        // Write to a temporary file first so an interrupted save cannot truncate the real one.
        var temp = _path + ".tmp";
        await File.WriteAllTextAsync(temp, json, cancellationToken).ConfigureAwait(false);
        File.Move(temp, _path, overwrite: true);
    }

    /// <summary>Synchronous save, used on shutdown where awaiting is not possible.</summary>
    public void Save(AppConfig config)
    {
        ArgumentNullException.ThrowIfNull(config);

        try
        {
            var directory = System.IO.Path.GetDirectoryName(System.IO.Path.GetFullPath(_path));
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            File.WriteAllText(_path, JsonSerializer.Serialize(config, Options));
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            LastError = e.Message;
        }
    }
}
