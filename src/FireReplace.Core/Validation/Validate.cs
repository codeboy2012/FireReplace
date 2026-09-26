using System.Diagnostics.CodeAnalysis;
using System.Net;
using System.Text.RegularExpressions;

namespace FireReplace.Core.Validation;

/// <summary>
/// Input validation helpers. Every value that ever reaches an ADB argument array
/// passes through one of these checks first, so that nothing user-supplied can
/// turn into an unexpected command, option or shell metacharacter.
/// </summary>
public static partial class Validate
{
    /// <summary>Android package names: dot separated segments starting with a letter.</summary>
    [GeneratedRegex(@"^[A-Za-z][A-Za-z0-9_]*(\.[A-Za-z0-9_]+)*$", RegexOptions.CultureInvariant)]
    private static partial Regex PackageNameRegex();

    /// <summary>
    /// Component names in the <c>package/.Class</c> or <c>package/fully.Qualified.Class</c> form
    /// used by accessibility and notification-listener settings.
    /// </summary>
    [GeneratedRegex(@"^[A-Za-z][A-Za-z0-9_.]*\/\.?[A-Za-z][A-Za-z0-9_.$]*$", RegexOptions.CultureInvariant)]
    private static partial Regex ComponentNameRegex();

    /// <summary>
    /// Keys accepted by <c>settings put</c>. Fire OS uses colons in a few of its own keys
    /// (for example <c>advertisingIdApp:interestBasedAds:value</c>), so colons are allowed.
    /// </summary>
    [GeneratedRegex(@"^[A-Za-z0-9_][A-Za-z0-9_:.\-]*$", RegexOptions.CultureInvariant)]
    private static partial Regex SettingKeyRegex();

    /// <summary>
    /// Tokens that may be placed inside a batched read-only shell line. Deliberately excludes
    /// whitespace, quotes, <c>;</c>, <c>&amp;</c>, <c>|</c>, <c>$</c>, backticks, redirection and
    /// parentheses so a batched line can never gain an extra command. A single leading dash is
    /// allowed so flag tokens such as <c>-d</c> and <c>--user</c> can be batched; without whitespace
    /// being possible there is no way for a flag token to introduce a second command.
    /// </summary>
    [GeneratedRegex(@"^-?[A-Za-z0-9_\-][A-Za-z0-9_:.,=@/\-]*$", RegexOptions.CultureInvariant)]
    private static partial Regex ShellSafeTokenRegex();

    /// <summary>Returns true when <paramref name="value"/> is a usable IPv4 or IPv6 literal.</summary>
    public static bool IsIpAddress([NotNullWhen(true)] string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        var trimmed = value.Trim();

        // Reject shortened forms such as "192.168.1" that IPAddress.Parse would silently accept.
        if (trimmed.Contains('.') && !trimmed.Contains(':'))
        {
            var parts = trimmed.Split('.');
            if (parts.Length != 4)
            {
                return false;
            }

            foreach (var part in parts)
            {
                if (part.Length is 0 or > 3)
                {
                    return false;
                }

                foreach (var c in part)
                {
                    if (!char.IsAsciiDigit(c))
                    {
                        return false;
                    }
                }

                if (!byte.TryParse(part, out _))
                {
                    return false;
                }
            }

            return true;
        }

        return IPAddress.TryParse(trimmed, out _);
    }

    /// <summary>Returns true for a TCP port in the usable 1-65535 range.</summary>
    public static bool IsPort(int port) => port is >= 1 and <= 65535;

    /// <summary>Returns true when <paramref name="value"/> is a well formed Android package name.</summary>
    public static bool IsPackageName([NotNullWhen(true)] string? value) =>
        !string.IsNullOrWhiteSpace(value) && value.Length <= 255 && PackageNameRegex().IsMatch(value);

    /// <summary>Returns true when <paramref name="value"/> is a well formed component name.</summary>
    public static bool IsComponentName([NotNullWhen(true)] string? value) =>
        !string.IsNullOrWhiteSpace(value) && value.Length <= 512 && ComponentNameRegex().IsMatch(value);

    /// <summary>Returns true when <paramref name="value"/> may be used as a settings key.</summary>
    public static bool IsSettingKey([NotNullWhen(true)] string? value) =>
        !string.IsNullOrWhiteSpace(value) && value.Length <= 255 && SettingKeyRegex().IsMatch(value);

    /// <summary>
    /// Returns true when <paramref name="value"/> is safe to embed in a batched read-only shell line.
    /// </summary>
    public static bool IsShellSafeToken([NotNullWhen(true)] string? value) =>
        !string.IsNullOrWhiteSpace(value) && value.Length <= 512 && ShellSafeTokenRegex().IsMatch(value);

    /// <summary>Returns true when the path points at an existing file whose extension is .apk.</summary>
    public static bool IsExistingApkPath([NotNullWhen(true)] string? path) =>
        IsExistingFileWithExtension(path, ".apk");

    /// <summary>Returns true when the path points at an existing file with the given extension.</summary>
    public static bool IsExistingFileWithExtension([NotNullWhen(true)] string? path, string extension)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return false;
        }

        if (path.IndexOfAny(Path.GetInvalidPathChars()) >= 0)
        {
            return false;
        }

        string full;
        try
        {
            full = Path.GetFullPath(path);
        }
        catch (Exception e) when (e is ArgumentException or NotSupportedException or PathTooLongException)
        {
            return false;
        }

        return string.Equals(Path.GetExtension(full), extension, StringComparison.OrdinalIgnoreCase)
               && File.Exists(full);
    }

    /// <summary>Throws when <paramref name="value"/> is not a valid package name.</summary>
    public static string PackageName(string? value) =>
        IsPackageName(value)
            ? value
            : throw new ArgumentException($"'{value}' is not a valid Android package name.", nameof(value));

    /// <summary>Throws when <paramref name="value"/> is not a valid component name.</summary>
    public static string ComponentName(string? value) =>
        IsComponentName(value)
            ? value
            : throw new ArgumentException($"'{value}' is not a valid component name.", nameof(value));

    /// <summary>Throws when <paramref name="value"/> is not a valid settings key.</summary>
    public static string SettingKey(string? value) =>
        IsSettingKey(value)
            ? value
            : throw new ArgumentException($"'{value}' is not a valid settings key.", nameof(value));

    /// <summary>Normalises and validates an IP address, throwing when it is unusable.</summary>
    public static string IpAddress(string? value) =>
        IsIpAddress(value)
            ? value.Trim()
            : throw new ArgumentException($"'{value}' is not a valid IP address.", nameof(value));
}
