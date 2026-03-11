using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Contracts.Text;
using MackySoft.Ucli.UnityProject;

namespace MackySoft.Ucli.TestRun.Configuration;

/// <summary> Merges CLI values, profile values, and defaults into normalized test-run configuration. </summary>
internal static class TestRunConfigurationMerger
{
    private const string DefaultMode = "auto";

    /// <summary> Merges one command input and profile configuration into a normalized configuration. </summary>
    /// <param name="cli"> The raw CLI input values. </param>
    /// <param name="profile"> The optional loaded profile. </param>
    /// <param name="projectPath"> The resolved project path candidate selected before merge. </param>
    /// <returns> The merged normalized configuration. </returns>
    public static MergedTestRunConfiguration Merge (
        TestRunCommandInput cli,
        TestRunProfile? profile,
        string projectPath)
    {
        ArgumentNullException.ThrowIfNull(cli);
        ArgumentException.ThrowIfNullOrWhiteSpace(projectPath);

        var mode = NormalizeMode(cli.Mode ?? DefaultMode);
        var mergedRawTestPlatform = cli.TestPlatform ?? profile?.TestPlatform ?? IpcTestRunPlatformCodec.EditMode;
        var hasParsedTestPlatform = IpcTestRunPlatformCodec.TryParse(mergedRawTestPlatform, out var parsedTestPlatform);

        return new MergedTestRunConfiguration(
            ProjectPath: Path.GetFullPath(projectPath),
            Mode: mode,
            UnityVersion: StringValueNormalizer.TrimToNull(cli.UnityVersion ?? profile?.UnityVersion),
            UnityEditorPath: NormalizeOptionalPath(cli.UnityEditorPath ?? profile?.UnityEditorPath),
            TestPlatform: hasParsedTestPlatform ? parsedTestPlatform : null,
            RawTestPlatform: mergedRawTestPlatform,
            BuildTarget: StringValueNormalizer.TrimToNull(cli.BuildTarget ?? profile?.BuildTarget),
            TestFilter: StringValueNormalizer.TrimToNull(cli.TestFilter ?? profile?.TestFilter),
            TestCategories: NormalizeValues(cli.TestCategory, profile?.TestCategories),
            AssemblyNames: NormalizeValues(cli.AssemblyName, profile?.AssemblyNames),
            TestSettingsPath: NormalizeOptionalPath(cli.TestSettingsPath ?? profile?.TestSettingsPath),
            TimeoutMilliseconds: cli.TimeoutMilliseconds ?? profile?.Timeout);
    }

    /// <summary> Normalizes optional path values into absolute paths. </summary>
    /// <param name="pathValue"> The optional path value. </param>
    /// <returns> The absolute path when provided; otherwise <see langword="null" />. </returns>
    private static string? NormalizeOptionalPath (string? pathValue)
    {
        if (!StringValueNormalizer.TryTrimToNonEmpty(pathValue, out var normalizedPathValue))
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
        if (StringValueNormalizer.TryTrimToNonEmpty(modeValue, out var normalizedModeValue))
        {
            return normalizedModeValue;
        }

        return modeValue;
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

}