using FireReplace.Core.Catalog;
using FireReplace.Core.Logging;
using FireReplace.Core.Models;

namespace FireReplace.Core.Adb;

/// <summary>
/// Default <see cref="IAdbService"/>.
/// </summary>
/// <remarks>
/// <para>
/// Safety properties held by this type:
/// </para>
/// <list type="bullet">
/// <item><description>All commands are argument arrays produced by <see cref="AdbArguments"/>.</description></item>
/// <item><description>No shell interpreter is ever used to launch adb.</description></item>
/// <item><description>Mutating calls are short-circuited when <see cref="DryRun"/> is on.</description></item>
/// <item><description>Every invocation is logged with its exit code; failures are never swallowed.</description></item>
/// </list>
/// </remarks>
public sealed class AdbService : IAdbService
{
    private readonly IProcessRunner _runner;
    private readonly ILogService _log;

    /// <summary>Creates the service.</summary>
    /// <param name="runner">Process launcher.</param>
    /// <param name="log">Log sink used for command tracing.</param>
    /// <param name="adbPath">Initial adb.exe path, when already known.</param>
    public AdbService(IProcessRunner runner, ILogService log, string? adbPath = null)
    {
        _runner = runner ?? throw new ArgumentNullException(nameof(runner));
        _log = log ?? throw new ArgumentNullException(nameof(log));
        AdbPath = adbPath;
    }

    /// <inheritdoc />
    public string? AdbPath { get; set; }

    /// <inheritdoc />
    public string? Serial { get; set; }

    /// <inheritdoc />
    public bool DryRun { get; set; }

    /// <inheritdoc />
    public TimeSpan ConnectTimeout { get; set; } = TimeSpan.FromSeconds(8);

    /// <inheritdoc />
    public TimeSpan CommandTimeout { get; set; } = TimeSpan.FromSeconds(30);

    /// <inheritdoc />
    public event EventHandler<AdbCommandResult>? CommandCompleted;

    /// <inheritdoc />
    public Task<AdbCommandResult> GetVersionAsync(CancellationToken cancellationToken = default) =>
        ExecuteAsync(AdbArguments.Version(), mutating: false, CommandTimeout, cancellationToken);

    /// <inheritdoc />
    public async Task<AdbCommandResult> ConnectAsync(
        string ipAddress,
        int port = 5555,
        CancellationToken cancellationToken = default)
    {
        var args = AdbArguments.Connect(ipAddress, port);

        // Connecting is not a device modification, so it runs even in dry-run mode: the user still
        // needs to see device state before deciding what to change.
        var result = await ExecuteAsync(args, mutating: false, ConnectTimeout, cancellationToken).ConfigureAwait(false);

        if (result.Success && !ConnectionOutcome.IsFailureText(result.CombinedOutput))
        {
            Serial = AdbArguments.FormatEndpoint(ipAddress, port);
        }

        return result;
    }

    /// <inheritdoc />
    public async Task<AdbCommandResult> DisconnectAsync(
        string? serial = null,
        CancellationToken cancellationToken = default)
    {
        var target = serial ?? Serial;
        if (string.IsNullOrWhiteSpace(target))
        {
            var noop = AdbCommandResult.DryRun(["disconnect"]);
            return noop;
        }

        var result = await ExecuteAsync(
            AdbArguments.Disconnect(target),
            mutating: false,
            ConnectTimeout,
            cancellationToken).ConfigureAwait(false);

        if (string.Equals(target, Serial, StringComparison.Ordinal))
        {
            Serial = null;
        }

        return result;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<AdbDeviceEntry>> GetDevicesAsync(CancellationToken cancellationToken = default)
    {
        var result = await ExecuteAsync(AdbArguments.Devices(), mutating: false, ConnectTimeout, cancellationToken)
            .ConfigureAwait(false);

        return result.Success
            ? AdbOutputParser.ParseDevices(result.Output)
            : Array.Empty<AdbDeviceEntry>();
    }

    /// <inheritdoc />
    public async Task<DeviceInfo?> GetDeviceInfoAsync(CancellationToken cancellationToken = default)
    {
        var result = await ExecuteAsync(
            AdbArguments.GetPropDump(Serial),
            mutating: false,
            CommandTimeout,
            cancellationToken).ConfigureAwait(false);

        if (!result.Success || string.IsNullOrWhiteSpace(result.Output))
        {
            return null;
        }

        var properties = AdbOutputParser.ParseGetPropDump(result.Output);
        return properties.Count == 0 ? null : AdbOutputParser.BuildDeviceInfo(Serial ?? string.Empty, properties);
    }

    /// <inheritdoc />
    public Task<AdbCommandResult> ShellAsync(
        IReadOnlyList<string> tokens,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(tokens);
        if (tokens.Count == 0)
        {
            throw new ArgumentException("A shell command needs at least one token.", nameof(tokens));
        }

        foreach (var token in tokens)
        {
            if (!Validation.Validate.IsShellSafeToken(token))
            {
                throw new ArgumentException($"Shell token '{token}' failed validation.", nameof(tokens));
            }
        }

        var args = new List<string> { "shell" };
        args.AddRange(tokens);
        return ExecuteAsync(
            AdbArguments.WithTarget(Serial, args.ToArray()),
            mutating: false,
            CommandTimeout,
            cancellationToken);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<string>> ShellBatchAsync(
        IReadOnlyList<IReadOnlyList<string>> commands,
        CancellationToken cancellationToken = default)
    {
        var args = AdbArguments.ShellBatch(Serial, commands);
        var result = await ExecuteAsync(args, mutating: false, CommandTimeout, cancellationToken).ConfigureAwait(false);

        if (!result.Success)
        {
            return Array.Empty<string>();
        }

        return SplitBatchOutput(result.Output, commands.Count);
    }

    /// <summary>Splits batched shell output on the echoed separator, padding to the expected count.</summary>
    public static IReadOnlyList<string> SplitBatchOutput(string output, int expectedCount)
    {
        var sections = new List<string>(expectedCount);
        var current = new List<string>();

        foreach (var line in (output ?? string.Empty).Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n'))
        {
            if (string.Equals(line.Trim(), AdbArguments.BatchSeparator, StringComparison.Ordinal))
            {
                sections.Add(string.Join('\n', current).Trim());
                current.Clear();
            }
            else
            {
                current.Add(line);
            }
        }

        sections.Add(string.Join('\n', current).Trim());

        while (sections.Count < expectedCount)
        {
            sections.Add(string.Empty);
        }

        return sections.Count > expectedCount ? sections.Take(expectedCount).ToArray() : sections;
    }

    /// <inheritdoc />
    public async Task<string?> GetSettingAsync(
        SettingScope scope,
        string key,
        CancellationToken cancellationToken = default)
    {
        var result = await ExecuteAsync(
            AdbArguments.GetSetting(Serial, scope, key),
            mutating: false,
            CommandTimeout,
            cancellationToken).ConfigureAwait(false);

        return result.Success ? AdbOutputParser.ParseSettingValue(result.Output) : null;
    }

    /// <inheritdoc />
    public Task<AdbCommandResult> PutSettingAsync(
        SettingScope scope,
        string key,
        string value,
        CancellationToken cancellationToken = default) =>
        ExecuteAsync(
            AdbArguments.PutSetting(Serial, scope, key, value),
            mutating: true,
            CommandTimeout,
            cancellationToken);

    /// <inheritdoc />
    public async Task<IReadOnlyList<string>> ListPackagesAsync(
        PackageListFilter filter,
        CancellationToken cancellationToken = default)
    {
        var result = await ExecuteAsync(
            AdbArguments.ListPackages(Serial, filter),
            mutating: false,
            CommandTimeout,
            cancellationToken).ConfigureAwait(false);

        return result.Success ? AdbOutputParser.ParsePackageList(result.Output) : Array.Empty<string>();
    }

    /// <inheritdoc />
    public Task<AdbCommandResult> DisablePackageAsync(
        string packageName,
        CancellationToken cancellationToken = default) =>
        ExecuteAsync(
            AdbArguments.DisablePackage(Serial, packageName),
            mutating: true,
            CommandTimeout,
            cancellationToken);

    /// <inheritdoc />
    public Task<AdbCommandResult> EnablePackageAsync(
        string packageName,
        CancellationToken cancellationToken = default) =>
        ExecuteAsync(
            AdbArguments.EnablePackage(Serial, packageName),
            mutating: true,
            CommandTimeout,
            cancellationToken);

    /// <inheritdoc />
    public Task<AdbCommandResult> GrantPermissionAsync(
        string packageName,
        string permission,
        CancellationToken cancellationToken = default) =>
        ExecuteAsync(
            AdbArguments.GrantPermission(Serial, packageName, permission),
            mutating: true,
            CommandTimeout,
            cancellationToken);

    /// <inheritdoc />
    public Task<AdbCommandResult> InstallApkAsync(
        string apkPath,
        bool grantRuntimePermissions = false,
        CancellationToken cancellationToken = default) =>
        ExecuteAsync(
            AdbArguments.Install(Serial, apkPath, grantRuntimePermissions),
            mutating: true,
            TimeSpan.FromMinutes(5),
            cancellationToken);

    /// <inheritdoc />
    public Task<AdbCommandResult> AllowNotificationListenerAsync(
        string component,
        CancellationToken cancellationToken = default) =>
        ExecuteAsync(
            AdbArguments.AllowNotificationListener(Serial, component),
            mutating: true,
            CommandTimeout,
            cancellationToken);

    /// <inheritdoc />
    public Task<AdbCommandResult> OpenSettingsScreenAsync(
        string action,
        CancellationToken cancellationToken = default) =>
        ExecuteAsync(
            AdbArguments.StartSettingsActivity(Serial, action),
            mutating: false,
            CommandTimeout,
            cancellationToken);

    /// <inheritdoc />
    public Task<AdbCommandResult> RebootAsync(CancellationToken cancellationToken = default) =>
        ExecuteAsync(AdbArguments.Reboot(Serial), mutating: true, ConnectTimeout, cancellationToken);

    private async Task<AdbCommandResult> ExecuteAsync(
        IReadOnlyList<string> arguments,
        bool mutating,
        TimeSpan timeout,
        CancellationToken cancellationToken)
    {
        if (mutating && DryRun)
        {
            var dry = AdbCommandResult.DryRun(arguments);
            _log.Write(LogLevel.DryRun, "adb", $"Dry run — would execute: {dry.DisplayCommand}");
            CommandCompleted?.Invoke(this, dry);
            return dry;
        }

        if (string.IsNullOrWhiteSpace(AdbPath))
        {
            var missing = new AdbCommandResult(
                arguments,
                ProcessResult.NotRun("adb.exe has not been located. Open Settings to set the platform-tools folder."),
                false);
            _log.Write(LogLevel.Error, "adb", "adb.exe is not available.", missing.Error);
            CommandCompleted?.Invoke(this, missing);
            return missing;
        }

        var process = await _runner.RunAsync(AdbPath, arguments, timeout, cancellationToken).ConfigureAwait(false);
        var result = new AdbCommandResult(arguments, process, false);

        _log.Write(
            result.Success ? LogLevel.Debug : LogLevel.Error,
            "adb",
            result.Success
                ? $"{result.DisplayCommand} ({process.Duration.TotalMilliseconds:0} ms)"
                : $"{result.DisplayCommand} failed with exit code {result.ExitCode}",
            result.Success ? null : result.Process.BestErrorText);

        CommandCompleted?.Invoke(this, result);
        return result;
    }
}
