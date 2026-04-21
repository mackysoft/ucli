using MackySoft.Ucli.Contracts.Testing;
using MackySoft.Ucli.Contracts.Text;
using MackySoft.Ucli.Shared.Context.Project;
using MackySoft.Ucli.Shared.Execution.UnityExecutionMode.Decision;

namespace MackySoft.Ucli.Features.Testing.Run.Configuration;

/// <summary> Merges CLI values, profile values, and defaults into normalized test-run configuration. </summary>
internal static class TestRunConfigurationMerger
{
    /// <summary> Merges one command input and profile configuration into a normalized configuration. </summary>
    /// <param name="cli"> The interpreted CLI input values. </param>
    /// <param name="profile"> The optional loaded profile. </param>
    /// <param name="projectPath"> The resolved project path candidate selected before merge. </param>
    /// <returns> The merged normalized configuration. </returns>
    public static MergedTestRunConfiguration Merge (
        TestRunConfigurationRequest cli,
        TestRunProfile? profile,
        string projectPath)
    {
        ArgumentNullException.ThrowIfNull(cli);
        ArgumentException.ThrowIfNullOrWhiteSpace(projectPath);

        var mode = ResolveMode(cli);
        var (mergedRawTestPlatform, parsedTestPlatform) = ResolveTestPlatform(cli, profile);

        return new MergedTestRunConfiguration(
            ProjectPath: Path.GetFullPath(projectPath),
            Mode: mode,
            UnityVersion: StringValueNormalizer.TrimToNull(cli.UnityVersion ?? profile?.UnityVersion),
            UnityEditorPath: NormalizeOptionalPath(cli.UnityEditorPath ?? profile?.UnityEditorPath),
            TestPlatform: parsedTestPlatform,
            RawTestPlatform: mergedRawTestPlatform,
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

    private static UnityExecutionMode ResolveMode (
        TestRunConfigurationRequest cli)
    {
        ArgumentNullException.ThrowIfNull(cli);

        if (cli.Mode.HasValue)
        {
            return cli.Mode!.Value;
        }

        return UnityExecutionMode.Auto;
    }

    private static (string RawValue, TestRunPlatform? ParsedValue) ResolveTestPlatform (
        TestRunConfigurationRequest cli,
        TestRunProfile? profile)
    {
        ArgumentNullException.ThrowIfNull(cli);

        if (cli.TestPlatform.HasValue)
        {
            var parsedValue = cli.TestPlatform.Value;
            return (TestRunPlatformCodec.ToValue(parsedValue), parsedValue);
        }

        var rawValue = profile?.TestPlatform ?? TestRunPlatformCodec.EditMode;
        var hasParsedValue = TestRunPlatformCodec.TryParse(rawValue, out var parsedValueFromProfile);
        return (rawValue, hasParsedValue ? parsedValueFromProfile : null);
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