using MackySoft.Ucli.UnityProject;

namespace MackySoft.Ucli.TestRun.Configuration;

/// <summary> Merges CLI values, profile values, and defaults into normalized test-run configuration. </summary>
internal static class TestRunConfigurationMerger
{
    private const string DefaultProjectPath = ".";

    private const string DefaultMode = "auto";

    private const string DefaultTestPlatform = "editmode";

    private const int DefaultTimeoutSeconds = 1800;

    /// <summary> Merges one command input and profile configuration into a normalized configuration. </summary>
    /// <param name="cli"> The raw CLI input values. </param>
    /// <param name="profile"> The optional loaded profile. </param>
    /// <returns> The merged normalized configuration. </returns>
    public static MergedTestRunConfiguration Merge (
        TestRunCommandInput cli,
        TestRunProfile? profile)
    {
        ArgumentNullException.ThrowIfNull(cli);

        var projectPath = cli.ProjectPath ?? profile?.ProjectPath ?? DefaultProjectPath;
        var mode = NormalizeMode(cli.Mode ?? DefaultMode);
        var mergedRawTestPlatform = cli.TestPlatform ?? profile?.TestPlatform ?? DefaultTestPlatform;

        return new MergedTestRunConfiguration(
            ProjectPath: Path.GetFullPath(projectPath),
            Mode: mode,
            UnityVersion: NormalizeOptionalString(cli.UnityVersion ?? profile?.UnityVersion),
            UnityEditorPath: NormalizeOptionalPath(cli.UnityEditorPath ?? profile?.UnityEditorPath),
            TestPlatform: ParseTestPlatform(mergedRawTestPlatform),
            RawTestPlatform: mergedRawTestPlatform,
            BuildTarget: NormalizeOptionalString(cli.BuildTarget ?? profile?.BuildTarget),
            TestFilter: NormalizeOptionalString(cli.TestFilter ?? profile?.TestFilter),
            TestCategories: NormalizeValues(cli.TestCategory, profile?.TestCategories),
            AssemblyNames: NormalizeValues(cli.AssemblyName, profile?.AssemblyNames),
            TestSettingsPath: NormalizeOptionalPath(cli.TestSettingsPath ?? profile?.TestSettingsPath),
            TimeoutSeconds: cli.TimeoutSeconds ?? profile?.TimeoutSeconds ?? DefaultTimeoutSeconds);
    }

    /// <summary> Normalizes an optional string value by trimming whitespace. </summary>
    /// <param name="value"> The optional value. </param>
    /// <returns> The normalized value; otherwise <see langword="null" /> when empty. </returns>
    private static string? NormalizeOptionalString (string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return value.Trim();
    }

    /// <summary> Normalizes optional path values into absolute paths. </summary>
    /// <param name="pathValue"> The optional path value. </param>
    /// <returns> The absolute path when provided; otherwise <see langword="null" />. </returns>
    private static string? NormalizeOptionalPath (string? pathValue)
    {
        var normalizedPathValue = NormalizeOptionalString(pathValue);
        if (normalizedPathValue is null)
        {
            return null;
        }

        return Path.GetFullPath(normalizedPathValue);
    }

    /// <summary> Normalizes mode option value into one trimmed token. </summary>
    /// <param name="modeValue"> The raw mode option value. </param>
    /// <returns> The normalized mode value. </returns>
    private static string NormalizeMode (string modeValue)
    {
        if (string.IsNullOrWhiteSpace(modeValue))
        {
            return modeValue;
        }

        return modeValue.Trim();
    }

    /// <summary> Normalizes list values by splitting comma-separated tokens and removing duplicates. </summary>
    /// <param name="cliValues"> The optional CLI values. </param>
    /// <param name="profileValues"> The optional profile values. </param>
    /// <returns> The normalized distinct values. </returns>
    private static string[] NormalizeValues (
        string[]? cliValues,
        string[]? profileValues)
    {
        var source = cliValues ?? profileValues;
        if (source is null || source.Length == 0)
        {
            return Array.Empty<string>();
        }

        return source
            .Where(static value => !string.IsNullOrWhiteSpace(value))
            .SelectMany(static value => value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            .Distinct(StringComparer.Ordinal)
            .ToArray();
    }

    /// <summary> Parses one raw test-platform literal or returns unknown value when unsupported. </summary>
    /// <param name="testPlatformValue"> The raw test-platform literal. </param>
    /// <returns> The parsed test-platform value. </returns>
    private static TestRunPlatform ParseTestPlatform (string testPlatformValue)
    {
        if (TestRunPlatformCodec.TryParse(testPlatformValue, out var parsedTestPlatform))
        {
            return parsedTestPlatform;
        }

        return TestRunPlatform.Unknown;
    }
}