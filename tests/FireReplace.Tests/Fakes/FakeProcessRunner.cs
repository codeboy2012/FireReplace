using FireReplace.Core.Adb;

namespace FireReplace.Tests.Fakes;

/// <summary>
/// A scripted <see cref="IProcessRunner"/>. Tests register canned responses keyed by a substring of
/// the rendered command line, so no real adb.exe and no real Fire TV are ever needed.
/// </summary>
public sealed class FakeProcessRunner : IProcessRunner
{
    private readonly List<(string Match, ProcessResult Result)> _responses = [];

    /// <summary>Every invocation, in order, as the rendered argument list.</summary>
    public List<string> Invocations { get; } = [];

    /// <summary>The raw argument arrays, for asserting on exact tokens.</summary>
    public List<IReadOnlyList<string>> ArgumentLists { get; } = [];

    /// <summary>Result returned when nothing matches.</summary>
    public ProcessResult Fallback { get; set; } = new(0, string.Empty, string.Empty, false, TimeSpan.Zero);

    /// <summary>Registers a canned successful response.</summary>
    public FakeProcessRunner WhenContains(string match, string stdout, string stderr = "", int exitCode = 0)
    {
        _responses.Add((match, new ProcessResult(exitCode, stdout, stderr, false, TimeSpan.FromMilliseconds(3))));
        return this;
    }

    /// <summary>Registers a canned response object.</summary>
    public FakeProcessRunner WhenContains(string match, ProcessResult result)
    {
        _responses.Add((match, result));
        return this;
    }

    /// <inheritdoc />
    public Task<ProcessResult> RunAsync(
        string executablePath,
        IReadOnlyList<string> arguments,
        TimeSpan timeout,
        CancellationToken cancellationToken = default)
    {
        var line = string.Join(' ', arguments);
        Invocations.Add(line);
        ArgumentLists.Add(arguments);

        foreach (var (match, result) in _responses)
        {
            if (line.Contains(match, StringComparison.Ordinal))
            {
                return Task.FromResult(result);
            }
        }

        return Task.FromResult(Fallback);
    }
}
